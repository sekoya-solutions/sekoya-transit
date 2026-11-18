#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Plays one game on the real Core as a contract-only player (<c>--player contracts</c>): it builds routes only to
    /// serve contracts and takes every contract it can, within the game's limits. Once per game month, after the
    /// month's first tick (so the month's new proposal is visible), it claims completed contracts and removes failed
    /// ones, serves the accepted contracts that have no route yet and accepts proposals while an accepted slot is free,
    /// then, when no contract waits for money, buys the oldest available vehicle unlock if cash covers it plus three
    /// months of upkeep and a full finished-goods chain of the best vehicles for next month (routes before unlocks). A
    /// contract route
    /// comes from the industry nearest to the town that makes the cargo; a processing industry first gets feeder routes
    /// from the nearest producers of its input. Each new route takes the mode with fewer routes so far (a coin flip
    /// when even), so about half are road and half rail. Routes get vehicle sets (3 trucks, 4 on Medium, 5 on Large, or 1 train with all its
    /// wagons, of the best unlocked type): 1 set on the leg to the town and twice the next leg's sets on each feeder
    /// (2 inputs make 1 output), so 4/2/1 for a finished good; every new route out of a processing industry adds
    /// its own sets to the whole chain behind it, even when those feeders already exist; the extra feeder sets go to a
    /// producer with spare output (a new feeder from another producer of the cargo when the current ones are drained).
    /// Vehicles are never replaced. A contract that cannot be served is declined (or cancelled if already accepted) with the reason
    /// recorded; one that only lacks cash waits.
    /// </summary>
    internal sealed class ContractPlayer
    {
        private const int _ticksPerYear = 36000;
        private readonly int _trucksPerRoute;
        private const int _reserveMonths = 3;

        // Before Issue #332 an industry loaded at most one unit per tick in total (about 3,000 units a month); since
        // then each waiting vehicle loads one unit per tick. Months from 2,700 units loaded are counted for comparison.
        private const long _nearLoadCapPerMonth = 2700L;

        // A producer whose vehicles loaded 90 % or more of last month's output gets no more feeder vehicles while another
        // producer of the same cargo has spare output.
        private const double _spareOutputLoadedShare = 0.9;

        private readonly ContentCatalog _catalog;
        private readonly BalanceOptions _options;
        private readonly GeneratedWorld _world;
        private readonly SimulationCalendar _calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
        private readonly EventBus _events = new EventBus();
        private readonly RouteBuilder _builder;
        private readonly Random _modeRandom;
        private readonly List<Leg> _routes = new List<Leg>();
        private readonly Dictionary<(int Source, int Target, string Cargo), bool> _modeChoice = new Dictionary<(int, int, string), bool>();
        private readonly Dictionary<int, int> _monthsWaitedForCash = new Dictionary<int, int>();
        private readonly ContractGameResult _result;
        private ContractYearRow _year = new ContractYearRow();
        private long _cashAtYearStart;
        private bool _startingContractsCounted;
        private bool _waitingForCashThisMonth;
        private readonly Dictionary<string, (long Town, long Industry)> _deliveredAtYearStart = new Dictionary<string, (long, long)>();
        private long _tick;

        public ContractPlayer(ContentCatalog catalog, BalanceOptions options, ulong seed)
        {
            _catalog = catalog;
            _options = options;
            _world = NewGame.Create(new NewGameRequest(seed, options.Size), catalog);
            _builder = new RouteBuilder(_world, catalog, () => _tick, Log, keepSiteEdgesFree: true);
            _modeRandom = new Random(unchecked((int)(seed * 2654435761UL)));
            // Owner rule: road routes get 4 trucks per set on Medium and 5 on Large (Small keeps 3); chain sets scale with it.
            _trucksPerRoute = options.Size == MapSize.Medium ? 4 : options.Size == MapSize.Large ? 5 : 3;
            _result = new ContractGameResult(seed, _world.Towns.Count, _world.Industries.Count);
            _cashAtYearStart = _world.CompanyCash;
            _result.IndustryLevelCount = catalog.Economy.IndustryGrowth.MaxLevel + 1;
            foreach (IndustryDefinition industry in catalog.Industries)
            {
                if (industry.Inputs.Count > 0 &&
                    catalog.TryGetCargo(industry.Outputs[0].CargoId, out CargoDefinition? output) &&
                    output != null &&
                    output.ManufacturingLevel == 3)
                {
                    _result.FinishedChains.Add((output.Id, industry.Inputs[0].CargoId));
                }
            }
        }

        private GameDate Date => _calendar.GetDate(new Tick(_tick));

        /// <summary>
        /// Plays the configured number of years and returns what happened.
        /// </summary>
        public ContractGameResult Play()
        {
            long endTick = (long)_options.Years * _ticksPerYear;
            int monthKey = -1;
            int year = GameDate.FirstYear;
            _year.Year = year;
            for (_tick = 1; _tick < endTick; _tick++)
            {
                GameDate date = Date;
                int key = (date.Year * 12) + date.Month;
                bool newMonth = key != monthKey;
                if (newMonth)
                {
                    monthKey = key;

                    // Before the new year's first tick, which rolls the finance ledger's current year.
                    if (date.Year != year)
                    {
                        RecordYear();
                        year = date.Year;
                        _year = new ContractYearRow { Year = year };
                    }
                }

                WorldSimulationStep.Advance(_world, _catalog, _calendar, new Tick(_tick), _events);
                if (_world.IsBankrupt)
                {
                    _result.BankruptOn = date;
                    break;
                }

                if (newMonth)
                {
                    PlayMonth(date);
                }
            }

            RecordYear();
            foreach (Leg route in _routes)
            {
                _result.Routes.Add(
                    $"{route.Built}: {(route.IsRail ? "rail" : "road")} {route.Cargo} {_builder.Name(route.Source)} -> " +
                    $"{_builder.Name(route.Target)}, {route.PathLength} tiles, {route.Sets} set(s) of {route.VehicleType} (latest), for {route.Purpose}");
            }

            return _result;
        }

        // ---------------------------------------------------------------- monthly turn

        private void PlayMonth(GameDate date)
        {
            // The starting contracts are accepted by Core on the first tick; they count as taken in the first year.
            if (!_startingContractsCounted)
            {
                _startingContractsCounted = true;
                int starting = _world.ContractList.Count(c => c.Status == ContractStatus.Accepted);
                _year.Taken += starting;
                _result.Taken += starting;
            }

            RecordPrimaryFlow(date);
            Settle(date);
            _waitingForCashThisMonth = false;

            // Contracts already accepted first (the starting ones, or ones whose route failed for lack of cash).
            foreach (Contract contract in _world.ContractList.Where(c => c.Status == ContractStatus.Accepted).ToList())
            {
                if (!IsSupplied(contract.CargoId, contract.TownIndex))
                {
                    TryServe(contract, date, accept: false);
                }
            }

            foreach (Contract proposal in _world.ContractList.Where(c => c.Status == ContractStatus.Proposal).OrderBy(c => c.Id).ToList())
            {
                if (!Contracts.HasAcceptSlot(_world, _catalog))
                {
                    break;
                }

                TryServe(proposal, date, accept: true);
            }

            // Routes before unlocks: an unlock only uses the cash left once this month's contract routes are built, and
            // none is bought while a contract still waits for money.
            if (!_waitingForCashThisMonth)
            {
                BuyUnlocks(date);
            }
        }

        // Claims completed contracts and removes failed ones, counting them for the year.
        private void Settle(GameDate date)
        {
            foreach (Contract contract in _world.ContractList.ToList())
            {
                if (contract.Status == ContractStatus.Completed &&
                    _builder.Execute(new ClaimContractRewardCommand(contract.Id, _catalog)))
                {
                    _year.Fulfilled++;
                    _result.Fulfilled++;
                    _result.Events.Add(NewEvent(contract, date, Describe(contract), "fulfilled", "completed " + contract.SettledDate.ToString()));
                }
                else if (contract.Status == ContractStatus.Failed &&
                         _builder.Execute(new DismissContractCommand(contract.Id, ContractStatus.Failed)))
                {
                    _year.Failed++;
                    _result.Failed++;
                    _result.Events.Add(NewEvent(contract, date,
                        Describe(contract),
                        "failed",
                        $"delivered {contract.Delivered.ToString(CultureInfo.InvariantCulture)}/" +
                        $"{contract.Amount.ToString(CultureInfo.InvariantCulture)} by {contract.Deadline}" +
                        (IsSupplied(contract.CargoId, contract.TownIndex) ? ", route running" : ", no route was built")));
                }
            }
        }

        // Buys every available locked vehicle type, oldest first, while cash covers it plus the reserve.
        private void BuyUnlocks(GameDate date)
        {
            while (true)
            {
                VehicleDefinition? target = _catalog.Vehicles
                    .Where(vehicle => vehicle.UnlockCost > 0 &&
                                      !_world.IsVehicleTypeUnlocked(vehicle.Id) &&
                                      VehicleAvailability.IsYearReached(vehicle, date.Year))
                    .OrderBy(vehicle => vehicle.AvailableYear)
                    .ThenBy(vehicle => vehicle.UnlockCost)
                    .FirstOrDefault();
                if (target == null ||
                    _world.CompanyCash < target.UnlockCost + Reserve() + NextRouteBuffer(date.Year) ||
                    !_builder.Execute(new UnlockVehicleTypeCommand(target.Id, date.Year, _catalog)))
                {
                    return;
                }

                _year.Unlocks.Add($"{target.Id} (available {target.AvailableYear.ToString(CultureInfo.InvariantCulture)})");
                _result.UnlocksBought.Add((target.Id, target.AvailableYear, target.UnlockCost, date));
                Log($"{date}: unlock {target.Id}");
            }
        }

        // Accepts (for a proposal) and builds the routes a contract needs; declines or cancels it with the reason when
        // it cannot be served, and leaves it waiting when only cash is missing.
        private void TryServe(Contract contract, GameDate date, bool accept)
        {
            var legs = new List<Leg>();
            string? failure = PlanSupply(contract.CargoId, contract.TownIndex, legs, null, 1, newDemand: false);
            if (failure != null)
            {
                Drop(contract, date, accept, failure);
                return;
            }

            long cost = legs.Sum(leg => leg.Sets * LegCost(leg.IsRail, date.Year));
            long needed = cost + Reserve();
            if (legs.Count > 0 && _world.CompanyCash < needed)
            {
                _monthsWaitedForCash[contract.Id] = _monthsWaitedForCash.GetValueOrDefault(contract.Id) + 1;
                _waitingForCashThisMonth = true;
                return;
            }

            if (accept)
            {
                if (!_builder.Execute(new AcceptContractCommand(contract.Id, date, _catalog)))
                {
                    return;
                }

                _year.Taken++;
                _result.Taken++;
            }

            string purpose = Describe(contract);
            foreach (Leg leg in legs)
            {
                string? buildFailure = Build(leg, date, purpose);
                if (buildFailure != null)
                {
                    Drop(contract, date, proposal: false, "build failed: " + buildFailure);
                    return;
                }
            }

            int newRoutes = legs.Count(leg => leg.TopUpOf == null);
            int topUps = legs.Where(leg => leg.TopUpOf != null).Sum(leg => leg.Sets);
            string note = legs.Count == 0
                ? "route already running"
                : $"{newRoutes.ToString(CultureInfo.InvariantCulture)} new route(s) ({string.Join(", ", legs.Where(leg => leg.TopUpOf == null).Select(leg => leg.Sets.ToString(CultureInfo.InvariantCulture) + " set(s) " + leg.Cargo))}), {topUps.ToString(CultureInfo.InvariantCulture)} set(s) added to existing routes";
            if (_monthsWaitedForCash.TryGetValue(contract.Id, out int waited))
            {
                _monthsWaitedForCash.Remove(contract.Id);
                _year.CashWaits.Add(waited);
                note += $", after waiting {waited.ToString(CultureInfo.InvariantCulture)} month(s) for cash";
            }

            _result.Events.Add(NewEvent(contract, date, purpose, accept ? "accepted" : "served", note));
        }

        private void Drop(Contract contract, GameDate date, bool proposal, string reason)
        {
            ContractStatus status = proposal ? ContractStatus.Proposal : ContractStatus.Accepted;
            if (!_builder.Execute(new DismissContractCommand(contract.Id, status)))
            {
                return;
            }

            if (proposal)
            {
                _year.Declined++;
                _result.Declined++;
            }
            else
            {
                _year.Cancelled++;
                _result.Cancelled++;
            }

            _result.Events.Add(NewEvent(contract, date, Describe(contract), proposal ? "declined" : "cancelled", reason));
        }

        // ---------------------------------------------------------------- planning

        // Adds to legs the routes needed to bring cargo to the target (a town or a processing industry), feeders
        // first; returns null when it can be served, else the reason. A forced mode plans without drawing a mode.
        // A leg's size is counted in vehicle sets (3 trucks, 4 on Medium, 5 on Large, or 1 full train): the leg to the town gets 1, and each
        // feeder twice the leg it feeds, since every recipe turns 2 inputs into 1 output (4/2/1 for a finished good).
        // Top-ups are additive: every new route out of a processing industry adds its own share to the whole chain
        // behind it, so a feeder already used by other routes gets the new route's sets on top of its own. A contract
        // whose town route already runs (newDemand false) adds nothing.
        private string? PlanSupply(string cargo, int target, List<Leg> legs, bool? forcedRail, int sets, bool newDemand)
        {
            Leg? planned = legs.FirstOrDefault(leg => leg.TopUpOf == null && leg.Cargo == cargo && leg.Target == target);
            if (planned != null)
            {
                planned.Sets += sets;
                return null;
            }

            List<Leg> existingFeeds = _routes.Where(route => route.Cargo == cargo && route.Target == target).ToList();
            if (existingFeeds.Count > 0)
            {
                if (!newDemand)
                {
                    return null;
                }

                // Spread the extra supply over the producers: top up a feed whose source still has spare output, else
                // open a feed from another producer with spare output, else top up the feed whose source is least
                // drained.
                Leg? existing = existingFeeds
                    .Where(route => HasSpareOutput(route.Source))
                    .OrderBy(route => RouteBuilder.Manhattan(_builder.Coordinate(route.Source), _builder.Coordinate(target)))
                    .ThenBy(route => route.Source)
                    .FirstOrDefault();
                if (existing == null)
                {
                    var feedSources = new HashSet<int>(existingFeeds.Select(route => route.Source));
                    var attempt = new List<Leg>(legs);
                    if (PlanNewLeg(cargo, target, attempt, forcedRail, sets, source => feedSources.Contains(source) || !HasSpareOutput(source)) == null)
                    {
                        legs.Clear();
                        legs.AddRange(attempt);
                        return null;
                    }

                    existing = existingFeeds.OrderBy(route => LoadedShare(route.Source)).ThenBy(route => route.Source).First();
                }

                legs.Add(new Leg(existing.IsRail, existing.Source, existing.Target, existing.Cargo, existing.PathLength) { TopUpOf = existing, Sets = sets });

                // The feeders of an existing route exist already; topping them up is best effort.
                if (_catalog.TryGetIndustry(IndustryIdOf(existing.Source), out IndustryDefinition? sourceDefinition) && sourceDefinition != null)
                {
                    foreach (CargoAmountDefinition input in sourceDefinition.Inputs)
                    {
                        var topUps = new List<Leg>(legs);
                        if (PlanSupply(input.CargoId, existing.Source, topUps, forcedRail, sets * 2, newDemand: true) == null)
                        {
                            legs.Clear();
                            legs.AddRange(topUps);
                        }
                    }
                }

                return null;
            }

            return PlanNewLeg(cargo, target, legs, forcedRail, sets, null);
        }

        // Share of last month's output that vehicles loaded at an industry (0 for a town or no output).
        private double LoadedShare(int objectId)
        {
            if (objectId < _builder.TownCount)
            {
                return 0d;
            }

            PlacedIndustry industry = _world.Industries[objectId - _builder.TownCount];
            return industry.LastMonthProduced <= 0 ? 1d : industry.LastMonthTransported / (double)industry.LastMonthProduced;
        }

        // An industry has spare output when vehicles loaded less than 90 % of what it made last month.
        private bool HasSpareOutput(int objectId)
        {
            return LoadedShare(objectId) < _spareOutputLoadedShare;
        }

        // Plans a new leg bringing cargo to the target from the nearest producer that skip does not exclude, with the
        // feeders that producer needs; returns null when planned, else the reason.
        private string? PlanNewLeg(string cargo, int target, List<Leg> legs, bool? forcedRail, int sets, Func<int, bool>? skip)
        {
            TileCoordinate targetTile = _builder.Coordinate(target);
            var producers = new List<(int ObjectId, IndustryDefinition Definition, int Distance)>();
            for (int index = 0; index < _world.Industries.Count; index++)
            {
                PlacedIndustry industry = _world.Industries[index];
                if (_catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                    definition != null &&
                    definition.Outputs.Any(output => output.CargoId == cargo) &&
                    (skip == null || !skip(_builder.TownCount + index)))
                {
                    producers.Add((_builder.TownCount + index, definition, RouteBuilder.Manhattan(industry.Coordinate, targetTile)));
                }
            }

            if (producers.Count == 0)
            {
                return $"no industry on the map makes {cargo}";
            }

            var reasons = new List<string>();
            foreach ((int source, IndustryDefinition definition, int distance) in producers.OrderBy(p => p.Distance).ThenBy(p => p.ObjectId))
            {
                bool rail = forcedRail ?? ModeFor(source, target, cargo);
                string mode = rail ? "rail" : "road";
                TileCoordinate? from = _builder.PeekStationTile(source, rail, target);
                TileCoordinate? to = _builder.PeekStationTile(target, rail, source);
                if (from == null || to == null)
                {
                    reasons.Add($"no free {mode} station tile at {_builder.Name(from == null ? source : target)}");
                    Log($"no free {mode} station tile at {_builder.Name(from == null ? source : target)}:" +
                        Environment.NewLine + _builder.DrawSurroundings(from == null ? source : target));
                    continue;
                }

                List<TileCoordinate>? path = _builder.FindPath(from.Value, to.Value, rail);
                if (path == null)
                {
                    reasons.Add($"no {mode} path {_builder.Name(source)} -> {_builder.Name(target)} ({distance.ToString(CultureInfo.InvariantCulture)} tiles apart)");
                    continue;
                }

                var feeders = new List<Leg>(legs);
                string? feederFailure = null;
                foreach (CargoAmountDefinition input in definition.Inputs)
                {
                    feederFailure = PlanSupply(input.CargoId, source, feeders, forcedRail, sets * 2, newDemand: true);
                    if (feederFailure != null)
                    {
                        break;
                    }
                }

                if (feederFailure != null)
                {
                    reasons.Add($"{_builder.Name(source)} cannot be fed ({feederFailure})");
                    continue;
                }

                legs.Clear();
                legs.AddRange(feeders);
                legs.Add(new Leg(rail, source, target, cargo, path.Count + 1) { Sets = sets });
                return null;
            }

            return string.Join("; ", reasons);
        }

        // The route mode is chosen once per (source, target, cargo), so waiting and re-planning do not change it: the
        // mode with fewer routes built so far, or a coin flip when even, which keeps the split near 50/50 while the
        // order stays random.
        private bool ModeFor(int source, int target, string cargo)
        {
            if (!_modeChoice.TryGetValue((source, target, cargo), out bool rail))
            {
                int railBuilt = _routes.Count(route => route.IsRail);
                int roadBuilt = _routes.Count - railBuilt;
                rail = railBuilt == roadBuilt ? _modeRandom.Next(2) == 1 : railBuilt < roadBuilt;
                _modeChoice[(source, target, cargo)] = rail;
            }

            return rail;
        }

        private string IndustryIdOf(int objectId)
        {
            return objectId < _builder.TownCount ? string.Empty : _world.Industries[objectId - _builder.TownCount].IndustryId;
        }

        private bool IsSupplied(string cargo, int target)
        {
            return _routes.Any(route => route.Cargo == cargo && route.Target == target);
        }

        // ---------------------------------------------------------------- building

        // Buys sets of vehicles (3 trucks, 4 on Medium, 5 on Large, or 1 full train each, of the best unlocked type) for a built route.
        private string? BuyVehicles(Leg route, int sets, GameDate date)
        {
            VehicleDefinition type = _builder.BestType(route.IsRail, date.Year);
            for (int set = 0; set < sets; set++)
            {
                if (route.IsRail)
                {
                    if (!_builder.Execute(new BuyVehicleForRouteCommand(route.CoreRouteId, _catalog, type.Id, date.Year, type.MaxWagons)))
                    {
                        return "buying a train";
                    }

                    route.VehicleIds.Add(_world.RailVehicles.Max(vehicle => vehicle.Id));
                    continue;
                }

                for (int truck = 0; truck < _trucksPerRoute; truck++)
                {
                    if (!_builder.Execute(new BuyVehicleForRouteCommand(route.CoreRouteId, _catalog, type.Id, date.Year)))
                    {
                        return "buying trucks";
                    }

                    route.VehicleIds.Add(_world.RoadVehicles.Max(vehicle => vehicle.Id));
                }
            }

            route.VehicleType = type.Id;
            return null;
        }

        private string? Build(Leg leg, GameDate date, string purpose)
        {
            if (leg.TopUpOf != null)
            {
                string? topUpFailure = BuyVehicles(leg.TopUpOf, leg.Sets, date);
                if (topUpFailure != null)
                {
                    return topUpFailure;
                }

                leg.TopUpOf.Sets += leg.Sets;
                _year.TopUpSets += leg.Sets;
                Log($"{date}: top-up +{leg.Sets} {leg.Cargo} {_builder.Name(leg.Source)} -> {_builder.Name(leg.Target)}");
                return null;
            }

            int? from = _builder.EnsureStation(leg.Source, leg.IsRail, leg.Target);
            int? to = _builder.EnsureStation(leg.Target, leg.IsRail, leg.Source);
            if (from == null || to == null)
            {
                return $"station at {_builder.Name(from == null ? leg.Source : leg.Target)}";
            }

            List<TileCoordinate>? path = _builder.FindPath(_builder.StationTile(from.Value, leg.IsRail), _builder.StationTile(to.Value, leg.IsRail), leg.IsRail);
            if (path == null)
            {
                return $"path {_builder.Name(leg.Source)} -> {_builder.Name(leg.Target)}";
            }

            _builder.BuildPath(path, leg.IsRail);
            leg.CoreRouteId = _builder.CreateCoreRoute(from.Value, to.Value, leg.IsRail, leg.Cargo);
            string? vehicleFailure = BuyVehicles(leg, leg.Sets, date);
            if (vehicleFailure != null)
            {
                return vehicleFailure;
            }

            leg.PathLength = path.Count + 1;
            leg.Built = date;
            leg.Purpose = purpose;
            _routes.Add(leg);
            if (leg.IsRail)
            {
                _year.RailBuilt++;
            }
            else
            {
                _year.RoadBuilt++;
            }

            Log($"{date}: {(leg.IsRail ? "rail" : "road")} {leg.Cargo} {_builder.Name(leg.Source)} -> {_builder.Name(leg.Target)}");
            return null;
        }

        // A road route's trucks, or a rail route's locomotive with all its wagons, of the best type this year.
        private long LegCost(bool rail, int year)
        {
            VehicleDefinition type = _builder.BestType(rail, year);
            return rail
                ? BuyVehicleForRouteCommand.GetPrice(_catalog, type, type.MaxWagons)
                : BuyVehicleForRouteCommand.GetPrice(_catalog, type) * _trucksPerRoute;
        }

        // Cash kept after an unlock for next month's contract: a full finished-goods chain (4/2/1 = 7 vehicle sets) of
        // the dearer mode's best type, so an unlock never leaves the next contract route waiting for money.
        private long NextRouteBuffer(int year)
        {
            return 7L * Math.Max(LegCost(rail: true, year), LegCost(rail: false, year));
        }

        private long Reserve()
        {
            return _builder.MonthlyUpkeep() * _reserveMonths;
        }

        // ---------------------------------------------------------------- results

        private ContractEvent NewEvent(Contract contract, GameDate date, string description, string kind, string detail)
        {
            int level = _catalog.TryGetCargo(contract.CargoId, out CargoDefinition? cargo) && cargo != null ? cargo.ManufacturingLevel : 0;
            return new ContractEvent(date, description, kind, detail) { Cargo = contract.CargoId, Level = level, Amount = contract.Amount, Delivered = contract.Delivered };
        }

        private string Describe(Contract contract)
        {
            return $"#{contract.Id.ToString(CultureInfo.InvariantCulture)} {contract.Amount.ToString(CultureInfo.InvariantCulture)} " +
                   $"{contract.CargoId} to {_world.Towns[contract.TownIndex].Name}";
        }

        // Town needs with no route yet that a route plan could serve (planned with road paths, nothing built).
        private int CountPossibleRoutes()
        {
            int possible = 0;
            for (int town = 0; town < _world.Towns.Count; town++)
            {
                foreach (TownNeed need in _world.Towns[town].Needs)
                {
                    if (!IsSupplied(need.CargoId, town) && PlanSupply(need.CargoId, town, new List<Leg>(), forcedRail: false, 1, newDemand: false) == null)
                    {
                        possible++;
                    }
                }
            }

            return possible;
        }

        // On a month's first turn the industries hold the closed month's figures; they count for that month's year.
        private void RecordPrimaryFlow(GameDate date)
        {
            int closedYear = date.Month == 1 ? date.Year - 1 : date.Year;
            if (closedYear < GameDate.FirstYear)
            {
                return;
            }

            long produced = 0L;
            long transported = 0L;
            foreach (PlacedIndustry industry in _world.Industries)
            {
                if (_catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                    definition != null &&
                    definition.Inputs.Count == 0)
                {
                    produced += industry.LastMonthProduced;
                    transported += industry.LastMonthTransported;
                    (long Max, int AtCap, int Months) cap = _result.LoadCap.GetValueOrDefault(closedYear);
                    _result.LoadCap[closedYear] = (Math.Max(cap.Max, industry.LastMonthTransported), cap.AtCap + (industry.LastMonthTransported >= _nearLoadCapPerMonth ? 1 : 0), cap.Months + 1);
                    string cargo = definition.Outputs[0].CargoId;
                    (long Produced, long Transported) byCargo = _result.RawFlow.GetValueOrDefault((closedYear, cargo));
                    _result.RawFlow[(closedYear, cargo)] = (byCargo.Produced + industry.LastMonthProduced, byCargo.Transported + industry.LastMonthTransported);
                }
                else if (definition != null)
                {
                    // Processing industries: output made from delivered inputs and output loaded, by output cargo.
                    string output = definition.Outputs[0].CargoId;
                    (long Produced, long Transported) byOutput = _result.RawFlow.GetValueOrDefault((closedYear, output));
                    _result.RawFlow[(closedYear, output)] = (byOutput.Produced + industry.LastMonthProduced, byOutput.Transported + industry.LastMonthTransported);
                }
            }

            (long Produced, long Transported) flow = _result.PrimaryFlow.GetValueOrDefault(closedYear);
            _result.PrimaryFlow[closedYear] = (flow.Produced + produced, flow.Transported + transported);
        }

        private long RouteIncome(Leg route)
        {
            long income = 0L;
            foreach (int vehicleId in route.VehicleIds)
            {
                if (route.IsRail && _world.TryGetRailVehicle(vehicleId, out RailVehicle? train) && train != null)
                {
                    income += train.LifetimeDeliveryIncome;
                }
                else if (!route.IsRail && _world.TryGetRoadVehicle(vehicleId, out RoadVehicle? truck) && truck != null)
                {
                    income += truck.LifetimeDeliveryIncome;
                }
            }

            return income;
        }

        private void RecordFlow()
        {
            _year.DeliveryIncome = _world.Finance.Current.Get(FinanceCategory.DeliveryIncome);
            _year.ContractBonus = _world.Finance.Current.Get(FinanceCategory.ContractBonus);
            foreach (CargoDefinition cargo in _catalog.Cargo.Where(c => c.Class == CargoClass.Cargo))
            {
                long town = _world.GetCargoDelivered(cargo.Id, DeliveryDestinationKind.Town);
                long industry = _world.GetCargoDelivered(cargo.Id, DeliveryDestinationKind.Industry);
                (long Town, long Industry) before = _deliveredAtYearStart.GetValueOrDefault(cargo.Id);
                _deliveredAtYearStart[cargo.Id] = (town, industry);
                int level = Math.Clamp(cargo.ManufacturingLevel, 1, 3);
                _year.DeliveredToTowns[level] = _year.DeliveredToTowns.GetValueOrDefault(level) + (town - before.Town);
                _year.DeliveredToIndustries += industry - before.Industry;
                _year.DeliveredToIndustriesByCargo[cargo.Id] = industry - before.Industry;
            }

            foreach (Leg route in _routes)
            {
                long lifetime = RouteIncome(route);
                int decade = route.Built.Year / 10 * 10;
                _year.IncomeByDecadeBuilt[decade] = _year.IncomeByDecadeBuilt.GetValueOrDefault(decade) + (lifetime - route.IncomeAtYearStart);
                _year.RoutesByDecadeBuilt[decade] = _year.RoutesByDecadeBuilt.GetValueOrDefault(decade) + 1;
                route.IncomeAtYearStart = lifetime;
            }
        }

        // Year-end stock at the primary industries per raw cargo, and how well the town raw needs that have a route were
        // met in the last closed month (November, since December closes on the next year's first tick).
        private void RecordRawSupply()
        {
            for (int index = 0; index < _world.Industries.Count; index++)
            {
                PlacedIndustry industry = _world.Industries[index];
                if (_catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                    definition != null &&
                    definition.Inputs.Count == 0)
                {
                    string cargo = definition.Outputs[0].CargoId;
                    (long Stock, long Capacity) stock = _year.RawStock.GetValueOrDefault(cargo);
                    _year.RawStock[cargo] = (stock.Stock + industry.Cargo.GetAmount(cargo), stock.Capacity + industry.CargoCapacity);
                    int source = _builder.TownCount + index;
                    _year.RawRoutes[cargo] = _year.RawRoutes.GetValueOrDefault(cargo) + _routes.Count(route => route.Source == source);
                }
            }

            int demand = _catalog.TownNeeds.DemandPerNeedPerMonth.Get(1);
            int partly = _catalog.TownNeeds.PartlyMetMinDemandPercent;
            for (int town = 0; town < _world.Towns.Count; town++)
            {
                foreach (TownNeed need in _world.Towns[town].Needs)
                {
                    if (!_catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo) || cargo == null ||
                        cargo.ManufacturingLevel != 1 || !IsSupplied(need.CargoId, town))
                    {
                        continue;
                    }

                    if (need.LastMonthSupply >= demand)
                    {
                        _year.RawNeedsFull++;
                    }
                    else if ((long)need.LastMonthSupply * 100 >= (long)demand * partly)
                    {
                        _year.RawNeedsPartly++;
                    }
                    else
                    {
                        _year.RawNeedsUnmet++;
                    }
                }
            }
        }

        private void RecordYear()
        {
            RecordFlow();
            RecordRawSupply();
            _year.Cash = _world.CompanyCash;
            _year.Income = _world.Finance.Current.Get(FinanceCategory.DeliveryIncome) + _world.Finance.Current.Get(FinanceCategory.ContractBonus);
            _year.CashChange = _world.CompanyCash - _cashAtYearStart;
            _year.RoadRoutes = _routes.Count(route => !route.IsRail);
            _year.RailRoutes = _routes.Count(route => route.IsRail);
            _year.PossibleRoutes = CountPossibleRoutes();
            _year.TotalTaken = _result.Taken;
            _year.TotalFulfilled = _result.Fulfilled;
            _year.OpenContracts = _world.ContractList.Count(c => c.Status == ContractStatus.Accepted);
            _year.WaitingProposals = _world.ContractList.Count(c => c.Status == ContractStatus.Proposal);
            var openIds = new HashSet<int>(_world.ContractList.Select(c => c.Id));
            _year.OpenCashWait = _monthsWaitedForCash.Where(pair => openIds.Contains(pair.Key)).Select(pair => pair.Value).DefaultIfEmpty(0).Max();
            TownNeedsSettings towns = _catalog.TownNeeds;
            foreach (Town town in _world.Towns)
            {
                int level = towns.GetTierIndexForPopulation(town.Population) + 1;
                _year.TownLevels[level] = _year.TownLevels.GetValueOrDefault(level) + 1;
            }

            foreach (PlacedIndustry industry in _world.Industries)
            {
                if (_catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                    definition != null &&
                    definition.Inputs.Count == 0)
                {
                    int level = industry.ProductionLevel + 1;
                    _year.IndustryLevels[level] = _year.IndustryLevels.GetValueOrDefault(level) + 1;
                }
            }

            _result.Years.Add(_year);
            _cashAtYearStart = _world.CompanyCash;
        }

        private void Log(string message)
        {
            if (_options.Verbose)
            {
                Console.WriteLine("  " + message);
            }
        }

        private sealed class Leg
        {
            public Leg(bool isRail, int source, int target, string cargo, int pathLength)
            {
                IsRail = isRail;
                Source = source;
                Target = target;
                Cargo = cargo;
                PathLength = pathLength;
            }

            public bool IsRail { get; }

            public int Source { get; }

            public int Target { get; }

            public string Cargo { get; }

            public int PathLength { get; set; }

            public string VehicleType { get; set; } = string.Empty;

            public GameDate Built { get; set; }

            public string Purpose { get; set; } = string.Empty;

            public int Sets { get; set; } = 1;

            public Leg? TopUpOf { get; set; }

            public int CoreRouteId { get; set; }

            public List<int> VehicleIds { get; } = new List<int>();

            public long IncomeAtYearStart { get; set; }
        }
    }
}
