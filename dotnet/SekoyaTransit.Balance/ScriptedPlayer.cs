#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Plays one game on the real Core like a steady human player: once per game month it buys vehicle unlocks it can
    /// afford, adds vehicles until its share of each source's output is transported, and opens at most its pace of new routes (stations plus a road or rail path found by A*). Every
    /// action goes through the same commands as the game UI, so rejected actions cost nothing.
    /// </summary>
    internal sealed class ScriptedPlayer
    {
        private const int _ticksPerYear = 36000;
        private const int _ticksPerMonth = _ticksPerYear / 12;
        private const int _newRouteTransportPercent = 50;
        private const int _maxRoutesPerSource = 5;

        // A player likes to grow towns: a needed delivery is worth a little more than its pay.
        private const double _neededGoodsBonus = 1.3;

        private readonly ContentCatalog _catalog;
        private readonly BalanceOptions _options;
        private readonly PlayerProfile _profile;
        private readonly GeneratedWorld _world;
        private readonly SimulationCalendar _calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
        private readonly SimulationCommandProcessor<GeneratedWorld> _processor;
        private readonly EventBus _events = new EventBus();
        private readonly List<Route> _routes = new List<Route>();
        private readonly Dictionary<int, List<TileCoordinate>> _footprints = new Dictionary<int, List<TileCoordinate>>();
        private readonly Dictionary<(int ObjectId, bool Rail), int> _stations = new Dictionary<(int, bool), int>();
        private readonly HashSet<(int Source, int Target, string Cargo)> _failed = new HashSet<(int, int, string)>();
        private readonly GameResult _result;
        private readonly ulong _seed;
        private readonly IncrementalHash? _stateHash;
        private readonly HashSet<string> _contractCargo = new HashSet<string>();
        private long _tick;
        private int _monthsSinceRoute = int.MaxValue / 2;
        private int _routesOpenedThisYear;
        private int _vehiclesAddedThisYear;

        public ScriptedPlayer(ContentCatalog catalog, BalanceOptions options, PlayerProfile profile, MapSize size, ulong seed)
        {
            _catalog = catalog;
            _options = options;
            _profile = profile;
            _seed = seed;
            _stateHash = options.StateHash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
            _world = NewGame.Create(new NewGameRequest(seed, size), catalog);
            _processor = new SimulationCommandProcessor<GeneratedWorld>(_world);
            _result = new GameResult(profile.Name, size.ToString(), seed, _world.Towns.Count, _world.Industries.Count);
            for (int tileId = 0; tileId < _world.Map.TileCount; tileId++)
            {
                TileCoordinate tile = _world.Map.GetCoordinate(tileId);
                int objectId = _world.Map.GetObjectId(tile);
                if (objectId == TileMap.NoObjectId)
                {
                    continue;
                }

                if (!_footprints.TryGetValue(objectId, out List<TileCoordinate>? footprint))
                {
                    footprint = new List<TileCoordinate>();
                    _footprints[objectId] = footprint;
                }

                footprint.Add(tile);
            }
        }

        private int TownCount => _world.TownAnchors.Count;

        // Processing chains are placed further apart on bigger maps, so a player builds longer routes there.
        private int MaxRouteDistance => _options.MaxRouteDistance > 0
            ? _options.MaxRouteDistance
            : _world.Map.Size switch { MapSize.Small => 70, MapSize.Medium => 100, _ => 130 };

        private GameDate Date => _calendar.GetDate(new Tick(_tick));

        /// <summary>
        /// Plays the configured number of years and returns what happened.
        /// </summary>
        public GameResult Play()
        {
            long endTick = (long)_options.Years * _ticksPerYear;
            int monthKey = -1;
            int year = GameDate.FirstYear;
            for (_tick = 1; _tick < endTick; _tick++)
            {
                GameDate date = Date;
                int key = (date.Year * 12) + date.Month;
                if (key != monthKey)
                {
                    monthKey = key;
                    if (date.Year != year)
                    {
                        RecordYear(year);
                        year = date.Year;
                    }

                    HashState();
                    TrackMonth(date);
                    PlayMonth(date);
                }

                WorldSimulationStep.Advance(_world, _catalog, _calendar, new Tick(_tick), _events);
                TrackMilestones();
                if (_world.IsBankrupt)
                {
                    _result.BankruptOn = date;
                    break;
                }
            }

            RecordYear(year);
            foreach (Route route in _routes.OrderByDescending(RouteIncome))
            {
                _result.Routes.Add(
                    $"{(route.IsRail ? "rail" : "road")} {route.Label}, {route.PathLength} tiles, " +
                    $"{route.Vehicles.Count} vehicles, lifetime {RouteIncome(route).ToString("N0", CultureInfo.InvariantCulture)}");
            }

            foreach (VehicleDefinition vehicle in _catalog.Vehicles.Where(v => v.UnlockCost > 0))
            {
                if (!_result.Unlocks.ContainsKey(vehicle.Id))
                {
                    _result.Unlocks[vehicle.Id] = new UnlockRecord(vehicle.Id, vehicle.AvailableYear, vehicle.UnlockCost);
                }
            }

            return _result;
        }

        // ---------------------------------------------------------------- monthly decisions

        private void PlayMonth(GameDate date)
        {
            PlayContracts(date);
            long upkeep = MonthlyUpkeep();
            long reserve = Math.Max(15000L, upkeep * 3L);
            BuyUnlock(date, reserve);

            long spendable = _world.CompanyCash - reserve;

            // One vehicle per source and cargo a month: the next month's figures show whether another is needed.
            var checkedSources = new HashSet<(int Source, string Cargo)>();
            foreach (Route route in _routes.OrderBy(route => route.Vehicles.Count).ToList())
            {
                if (spendable <= 0L)
                {
                    break;
                }

                if (!checkedSources.Add((route.SourceObject, route.Cargo)) || !NeedsVehicle(route, date.Year))
                {
                    continue;
                }

                long cost = VehicleSetCost(route.IsRail, date.Year);
                if (cost <= spendable && AddVehicle(route, date))
                {
                    spendable -= cost;
                    _vehiclesAddedThisYear++;
                }
            }

            _monthsSinceRoute++;
            if (_monthsSinceRoute < (_options.MonthsPerRoute > 0 ? _options.MonthsPerRoute : _profile.MonthsPerRoute))
            {
                return;
            }

            // A route that money or the terrain delays stays due the next month.
            while (true)
            {
                Candidate? candidate = BestCandidate();
                if (candidate == null)
                {
                    _result.MonthsWithoutRoute[(date.Year - GameDate.FirstYear) / 10]++;
                    _result.FirstWithoutRoute ??= date;
                    return;
                }

                bool rail = _options.UseRail &&
                            candidate.Distance >= _options.RailMinDistance &&
                            !PassengerCargo.IsPassengerClass(_catalog, candidate.Cargo);
                int fleet = StartingFleet(candidate, rail, date.Year);
                long routeCost = VehicleSetCost(rail, date.Year) * fleet;
                if (routeCost > _world.CompanyCash - reserve)
                {
                    _result.MonthsShortOfMoney[(date.Year - GameDate.FirstYear) / 10]++;
                    return;
                }

                if (OpenRoute(candidate, rail, fleet, date))
                {
                    _routesOpenedThisYear++;
                    _monthsSinceRoute = 0;
                    return;
                }

                _failed.Add((candidate.Source, candidate.Target, candidate.Cargo));
            }
        }

        // Claims finished rewards, drops failed contracts, and answers proposals by the player's contract policy.
        // A player who only takes served contracts drops the oldest proposal from a full list, to make room for new ones.
        private void PlayContracts(GameDate date)
        {
            foreach (Contract contract in _world.ContractList.ToList())
            {
                _result.ContractsSeen.Add(contract.Id);
                if (contract.Status == ContractStatus.Completed)
                {
                    if (Execute(new ClaimContractRewardCommand(contract.Id, _catalog)))
                    {
                        _result.ContractsClaimed++;
                        _result.ClaimedByLevel[LevelIndex(contract.CargoId)]++;
                    }
                }
                else if (contract.Status == ContractStatus.Failed)
                {
                    Execute(new DismissContractCommand(contract.Id, ContractStatus.Failed));
                    _result.ContractsFailed++;
                    _result.FailedByLevel[LevelIndex(contract.CargoId)]++;
                    _result.FailedDeliveredPercentSum += 100.0 * contract.Delivered / contract.Amount;
                    if (!_routes.Any(route => route.TargetObject == contract.TownIndex && route.Cargo == contract.CargoId))
                    {
                        _result.FailedWithoutRoute++;
                    }
                }
                else if (contract.Status == ContractStatus.Proposal)
                {
                    PlayProposal(contract, date);
                }
            }

            if (_profile.ChasesContracts)
            {
                RefreshContractCargo();
            }
            else if (_world.ContractList.Count >= _catalog.Contracts.GetLimits(_world.Map.Size).MaxContracts)
            {
                Contract? stale = _world.ContractList.FirstOrDefault(contract => contract.Status == ContractStatus.Proposal);
                if (stale != null && Execute(new DismissContractCommand(stale.Id, ContractStatus.Proposal)))
                {
                    _result.ContractsDropped++;
                }
            }
        }

        private void PlayProposal(Contract contract, GameDate date)
        {
            if (_profile.Contracts == ContractPolicy.EverySecond && contract.Id % 2 != 0)
            {
                if (Execute(new DismissContractCommand(contract.Id, ContractStatus.Proposal)))
                {
                    _result.ContractsDropped++;
                }

                return;
            }

            if (Contracts.HasAcceptSlot(_world, _catalog) &&
                (_profile.ChasesContracts || _routes.Any(route => route.TargetObject == contract.TownIndex && route.Cargo == contract.CargoId)))
            {
                Execute(new AcceptContractCommand(contract.Id, date, _catalog));
            }
        }

        private int LevelIndex(string cargoId)
        {
            return _catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) && cargo != null
                ? Math.Clamp(cargo.ManufacturingLevel, 1, 3) - 1
                : 0;
        }

        // The cargo of every accepted contract no route serves yet, and everything its chain consumes.
        private void RefreshContractCargo()
        {
            _contractCargo.Clear();
            var pending = new Stack<string>(_world.ContractList
                .Where(contract => IsUnservedContract(contract.TownIndex, contract.CargoId))
                .Select(contract => contract.CargoId));
            while (pending.Count > 0)
            {
                string cargo = pending.Pop();
                if (!_contractCargo.Add(cargo))
                {
                    continue;
                }

                foreach (IndustryDefinition industry in _catalog.Industries.Where(item => item.Outputs.Any(output => output.CargoId == cargo)))
                {
                    foreach (CargoAmountDefinition input in industry.Inputs)
                    {
                        pending.Push(input.CargoId);
                    }
                }
            }
        }

        private bool IsUnservedContract(int town, string cargo)
        {
            return _world.ContractList.Any(contract => contract.Status == ContractStatus.Accepted && contract.TownIndex == town && contract.CargoId == cargo) &&
                   !_routes.Any(route => route.TargetObject == town && route.Cargo == cargo);
        }

        // Buys the oldest available locked vehicle once cash covers its price and the reserve. Nothing is held back for it.
        private void BuyUnlock(GameDate date, long reserve)
        {
            VehicleDefinition? target = null;
            foreach (VehicleDefinition vehicle in _catalog.Vehicles)
            {
                if (vehicle.UnlockCost <= 0 ||
                    _world.IsVehicleTypeUnlocked(vehicle.Id) ||
                    !VehicleAvailability.IsYearReached(vehicle, date.Year) ||
                    (vehicle.Mode == VehicleMode.Rail && !_options.UseRail))
                {
                    continue;
                }

                if (!_result.Unlocks.ContainsKey(vehicle.Id))
                {
                    _result.Unlocks[vehicle.Id] = new UnlockRecord(vehicle.Id, vehicle.AvailableYear, vehicle.UnlockCost)
                    {
                        CashWhenAvailable = _world.CompanyCash,
                    };
                }

                if (target == null ||
                    vehicle.AvailableYear < target.AvailableYear ||
                    (vehicle.AvailableYear == target.AvailableYear && vehicle.UnlockCost < target.UnlockCost))
                {
                    target = vehicle;
                }
            }

            if (target != null &&
                _world.CompanyCash >= target.UnlockCost + reserve &&
                Execute(new UnlockVehicleTypeCommand(target.Id, date.Year, _catalog)))
            {
                _result.Unlocks[target.Id].UnlockedOn = date;
                Log($"{date}: unlock {target.Id} for {target.UnlockCost}");
            }
        }

        // Vehicles for a new route to carry what is left of the first half of the source's monthly output, from the
        // vehicle's capacity and its round trip over the straight-line distance, loading time not counted. At least one.
        private int StartingFleet(Candidate candidate, bool rail, int year)
        {
            long wanted = (MonthlyOutput(candidate.Source, candidate.Cargo) * _newRouteTransportPercent / 100L) - MonthTransported(candidate.Source, candidate.Cargo);
            if (wanted <= 0L)
            {
                return 1;
            }

            VehicleDefinition type = BestType(rail, year);
            int capacity = rail ? type.MaxWagons * type.CapacityPerWagon : type.CargoCapacity;
            double tilesPerTick = type.SpeedPerTick.RawValue / (double)FixedPoint.OneRawValue;
            double tripsPerMonth = _ticksPerMonth / (2.0 * Math.Max(1, candidate.Distance) / tilesPerTick);
            return Math.Max(1, (int)Math.Ceiling(wanted / (capacity * tripsPerMonth)));
        }

        // What the source makes in a month at full rate; a processing industry's output follows its inputs, so last month's.
        private long MonthlyOutput(int source, string cargo)
        {
            long pulses = _ticksPerMonth / IndustryProduction.ProductionIntervalTicks;
            if (source < TownCount)
            {
                TownNeedsSettings settings = _catalog.TownNeeds;
                int population = _world.Towns[source].Population;
                return pulses * (cargo == "mail" ? population / settings.MailPerPopulationUnit : Math.Max(1, population / settings.PassengersPerPopulationUnit));
            }

            PlacedIndustry industry = _world.Industries[source - TownCount];
            if (_catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) && definition != null && definition.Inputs.Count == 0)
            {
                int amount = definition.Outputs.Where(output => output.CargoId == cargo).Sum(output => output.Amount);
                return pulses * amount * _catalog.Economy.IndustryGrowth.GetFactor(industry.ProductionLevel);
            }

            return industry.MonthProduced;
        }

        private long MonthTransported(int source, string cargo)
        {
            if (source >= TownCount)
            {
                return _world.Industries[source - TownCount].MonthTransported;
            }

            Town town = _world.Towns[source];
            return cargo == "mail" ? town.MonthMailTransported : town.MonthPassengersTransported;
        }

        // Whether last month's transported share of the source's output is below the player's target, with cargo for one
        // more vehicle waiting. Runs before the month rolls, so the Month counters still hold the finished month. Vehicles
        // waiting for a load leave no stock, so a source that is short of cargo gets no more of them.
        private bool NeedsVehicle(Route route, int year)
        {
            long produced;
            long transported;
            int stock;
            int stockCapacity;
            if (route.SourceObject < TownCount)
            {
                Town town = _world.Towns[route.SourceObject];
                bool mail = route.Cargo == "mail";
                produced = mail ? town.MonthMailGenerated : town.MonthPassengersGenerated;
                transported = mail ? town.MonthMailTransported : town.MonthPassengersTransported;
                stock = town.WaitingCargo.GetAmount(route.Cargo);
                stockCapacity = _catalog.TownNeeds.PassengerWaitingCapacity;
            }
            else
            {
                PlacedIndustry industry = _world.Industries[route.SourceObject - TownCount];
                produced = industry.MonthProduced;
                transported = industry.MonthTransported;
                stock = industry.Cargo.GetAmount(route.Cargo);
                stockCapacity = industry.CargoCapacity;
            }

            if (produced <= 0L || transported * 100L >= produced * _profile.TransportTargetPercent)
            {
                return false;
            }

            VehicleDefinition type = BestType(route.IsRail, year);
            int capacity = route.IsRail ? type.MaxWagons * type.CapacityPerWagon : type.CargoCapacity;
            return stock >= Math.Min(capacity, stockCapacity);
        }

        // ---------------------------------------------------------------- route candidates

        private Candidate? BestCandidate()
        {
            Candidate? best = null;
            foreach (Candidate candidate in Candidates())
            {
                best = Better(best, candidate);
            }

            return best;
        }

        // Freight: every served or primary industry output to the best industry that consumes it (one route per
        // source and cargo), and to the best town that needs it and gets no supply yet (a source splits to at most five
        // destinations).
        // Passengers and mail: each town to a near town it does not exchange that cargo with yet.
        private IEnumerable<Candidate> Candidates()
        {
            var fedIndustries = new HashSet<int>(_routes.Select(route => route.TargetObject));
            for (int index = 0; index < _world.Industries.Count; index++)
            {
                PlacedIndustry industry = _world.Industries[index];
                int source = TownCount + index;
                if (!_catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) ||
                    definition == null ||
                    (definition.Inputs.Count > 0 && !fedIndustries.Contains(source)) ||
                    _routes.Count(route => route.SourceObject == source) >= _maxRoutesPerSource)
                {
                    continue;
                }

                for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
                {
                    string cargo = definition.Outputs[outputIndex].CargoId;
                    bool feedsIndustry = _routes.Any(route => route.SourceObject == source && route.Cargo == cargo && route.TargetObject >= TownCount);
                    Candidate? best = null;
                    if (!feedsIndustry)
                    {
                        for (int target = 0; target < _world.Industries.Count; target++)
                        {
                            if (_catalog.TryGetIndustry(_world.Industries[target].IndustryId, out IndustryDefinition? consumer) &&
                                consumer != null &&
                                IndustryProduction.IsInput(consumer, cargo))
                            {
                                best = Better(best, MakeCandidate(source, TownCount + target, cargo, industry.Coordinate, _world.Industries[target].Coordinate));
                            }
                        }
                    }

                    Candidate? bestTown = null;
                    for (int town = 0; town < _world.Towns.Count; town++)
                    {
                        if (_world.Towns[town].HasNeed(cargo) && !_routes.Any(route => route.TargetObject == town && route.Cargo == cargo))
                        {
                            Candidate? toTown = MakeCandidate(source, town, cargo, industry.Coordinate, _world.Towns[town].Coordinate);
                            if (toTown != null)
                            {
                                toTown.Score *= _neededGoodsBonus;
                                if (_profile.ChasesContracts && IsUnservedContract(town, cargo))
                                {
                                    toTown.Score *= 1000.0;
                                }
                                else if (_world.ContractList.Any(contract => contract.Status == ContractStatus.Proposal && contract.TownIndex == town && contract.CargoId == cargo))
                                {
                                    toTown.Score *= 1.5;
                                }
                            }

                            bestTown = Better(bestTown, toTown);
                        }
                    }

                    // The chain to the next industry and the supply of a needy town are separate jobs.
                    if (best != null)
                    {
                        if (_contractCargo.Contains(cargo))
                        {
                            best.Score *= 8.0;
                        }

                        yield return best;
                    }

                    if (bestTown != null)
                    {
                        yield return bestTown;
                    }
                }
            }

            foreach (CargoDefinition cargo in _catalog.Cargo.Where(c => c.Class == CargoClass.Passengers))
            {
                for (int town = 0; town < _world.Towns.Count; town++)
                {
                    if (_routes.Any(route => route.Cargo == cargo.Id && (route.SourceObject == town || route.TargetObject == town)))
                    {
                        continue;
                    }

                    Candidate? best = null;
                    for (int other = 0; other < _world.Towns.Count; other++)
                    {
                        if (other != town)
                        {
                            best = Better(best, MakeCandidate(town, other, cargo.Id, _world.Towns[town].Coordinate, _world.Towns[other].Coordinate));
                        }
                    }

                    if (best != null)
                    {
                        yield return best;
                    }
                }
            }
        }

        private Candidate? MakeCandidate(int source, int target, string cargo, TileCoordinate from, TileCoordinate to)
        {
            int distance = Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
            if (distance > MaxRouteDistance || _failed.Contains((source, target, cargo)) ||
                !_catalog.TryGetCargo(cargo, out CargoDefinition? definition) || definition == null)
            {
                return null;
            }

            // Pay grows with distance and so does the fleet a route needs; medium routes give the best money per vehicle.
            int paid = Math.Clamp(distance, 8, 50);
            double score = definition.PricePerUnit * (double)paid / (distance + 10);
            return new Candidate(source, target, cargo, distance, score);
        }

        private static Candidate? Better(Candidate? current, Candidate? candidate)
        {
            if (candidate == null)
            {
                return current;
            }

            return current == null || candidate.Score > current.Score ? candidate : current;
        }

        private int CountOpportunities()
        {
            return Candidates().Count();
        }

        // ---------------------------------------------------------------- building

        private bool OpenRoute(Candidate candidate, bool rail, int fleet, GameDate date)
        {
            int? from = EnsureStation(candidate.Source, rail, candidate.Target);
            int? to = EnsureStation(candidate.Target, rail, candidate.Source);
            if (from == null || to == null)
            {
                return false;
            }

            List<TileCoordinate>? path = FindPath(StationTile(from.Value, rail), StationTile(to.Value, rail), rail);
            if (path == null)
            {
                return false;
            }

            foreach (TileCoordinate tile in path)
            {
                if (rail && _world.Map.GetRailTypeId(tile) == TileMap.NoRailTypeId)
                {
                    Execute(new BuildRailCommand(tile));
                }
                else if (!rail && _world.Map.GetRoadTypeId(tile) == TileMap.NoRoadTypeId)
                {
                    Execute(new BuildRoadCommand(tile));
                }
            }

            var route = new Route(rail, from.Value, to.Value, candidate.Cargo, candidate.Source, candidate.Target, path.Count + 1)
            {
                Label = $"{candidate.Cargo} {Name(candidate.Source)} -> {Name(candidate.Target)}",
            };
            if (!AddVehicle(route, date))
            {
                return false;
            }

            for (int vehicle = 1; vehicle < fleet; vehicle++)
            {
                AddVehicle(route, date);
            }

            _routes.Add(route);
            Log($"{date}: route {(rail ? "rail" : "road")} {route.Label}, {route.PathLength} tiles, cash {_world.CompanyCash}");
            return true;
        }

        private bool AddVehicle(Route route, GameDate date)
        {
            VehicleDefinition type = BestType(route.IsRail, date.Year);
            int routeId = EnsureCoreRoute(route);
            if (!Execute(new BuyVehicleForRouteCommand(routeId, _catalog, type.Id, date.Year)))
            {
                return false;
            }

            if (route.IsRail)
            {
                // A train bought for a route already pulls one wagon.
                int trainId = _world.RailVehicles.Max(vehicle => vehicle.Id);
                for (int wagon = 1; wagon < type.MaxWagons; wagon++)
                {
                    Execute(new AddRailWagonCommand(trainId, _catalog));
                }

                route.Vehicles.Add(trainId);
                return true;
            }

            route.Vehicles.Add(_world.RoadVehicles.Max(vehicle => vehicle.Id));
            return true;
        }

        // The Core route behind a scripted route (Issue #229): waits for a full load at the source except for
        // passengers and mail, and goes empty-handed to the target.
        private int EnsureCoreRoute(Route route)
        {
            if (route.CoreRouteId.HasValue)
            {
                return route.CoreRouteId.Value;
            }

            int routeId = _world.NextRouteId;
            RouteMode mode = route.IsRail ? RouteMode.Rail : RouteMode.Road;
            Execute(new CreateRouteCommand("Balance " + routeId.ToString(CultureInfo.InvariantCulture)));
            Execute(new AddRouteStopCommand(routeId, mode, route.FromStation));
            Execute(new AddRouteStopCommand(routeId, mode, route.ToStation));
            if (!PassengerCargo.IsPassengerClass(_catalog, route.Cargo))
            {
                Execute(new SetRouteStopWaitForFullCommand(routeId, 0, true));
            }

            route.CoreRouteId = routeId;
            return routeId;
        }

        private long MonthlyUpkeep()
        {
            long total = 0L;
            foreach (RoadVehicle vehicle in _world.RoadVehicles)
            {
                total += VehicleUpkeep.GetMonthlyUpkeep(_catalog, vehicle);
            }

            foreach (RailVehicle vehicle in _world.RailVehicles)
            {
                total += VehicleUpkeep.GetMonthlyUpkeep(_catalog, vehicle);
            }

            return total;
        }

        private int? EnsureStation(int objectId, bool rail, int towardObjectId)
        {
            if (_stations.TryGetValue((objectId, rail), out int existing))
            {
                return existing;
            }

            TileCoordinate toward = _footprints[towardObjectId][0];
            TileCoordinate? best = null;
            int bestDistance = int.MaxValue;
            foreach (TileCoordinate part in _footprints[objectId])
            {
                foreach (TileCoordinate tile in Neighbors(part))
                {
                    bool buildable = _world.Map.Contains(tile) &&
                                     (rail ? RailStationPlacement.CanBuild(_world.Map, tile) : RoadStationPlacement.CanBuild(_world.Map, tile));

                    // A tile that also touches another town or industry, or a station, would make acceptance ambiguous.
                    if (!buildable || Neighbors(tile).Any(n => TouchesOther(n, objectId)))
                    {
                        continue;
                    }

                    int distance = Math.Abs(tile.X - toward.X) + Math.Abs(tile.Y - toward.Y);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = tile;
                    }
                }
            }

            if (best == null)
            {
                return null;
            }

            if (rail)
            {
                if (!Execute(new BuildRailStationCommand(best.Value)) ||
                    !_world.TryGetRailStationAt(best.Value, out RailStation? railStation) || railStation == null)
                {
                    return null;
                }

                _stations[(objectId, rail)] = railStation.Id;
                return railStation.Id;
            }

            if (!Execute(new BuildRoadStationCommand(best.Value)) ||
                !_world.TryGetRoadStationAt(best.Value, out RoadStation? roadStation) || roadStation == null)
            {
                return null;
            }

            _stations[(objectId, rail)] = roadStation.Id;
            return roadStation.Id;
        }

        private bool TouchesOther(TileCoordinate tile, int objectId)
        {
            if (!_world.Map.Contains(tile))
            {
                return false;
            }

            int other = _world.Map.GetObjectId(tile);
            return (other != TileMap.NoObjectId && other != objectId) ||
                   _world.Map.GetStationId(tile) != TileMap.NoStationId ||
                   _world.Map.GetRailStationId(tile) != TileMap.NoRailStationId;
        }

        // A* over 4-neighbour tiles between two station tiles; reuses own-mode track cheaply, crosses the other mode
        // reluctantly. Returns the interior tiles.
        private List<TileCoordinate>? FindPath(TileCoordinate from, TileCoordinate to, bool rail)
        {
            TileMap map = _world.Map;
            int start = map.GetTileId(from);
            int goal = map.GetTileId(to);
            int limit = (Manhattan(from, to) * 6) + 40;
            var cost = new Dictionary<int, int> { [start] = 0 };
            var previous = new Dictionary<int, int>();
            var open = new PriorityQueue<int, int>();
            open.Enqueue(start, Manhattan(from, to) * 2);
            while (open.Count > 0)
            {
                int current = open.Dequeue();
                if (current == goal)
                {
                    break;
                }

                foreach (TileCoordinate next in Neighbors(map.GetCoordinate(current)))
                {
                    if (!map.Contains(next))
                    {
                        continue;
                    }

                    int nextId = map.GetTileId(next);
                    int step = nextId == goal ? 1 : StepCost(next, rail);
                    int nextCost = cost[current] + step;
                    if (step < 0 || nextCost > limit || (cost.TryGetValue(nextId, out int known) && known <= nextCost))
                    {
                        continue;
                    }

                    cost[nextId] = nextCost;
                    previous[nextId] = current;
                    open.Enqueue(nextId, nextCost + (Manhattan(next, to) * 2));
                }
            }

            if (!previous.TryGetValue(goal, out int beforeGoal))
            {
                return null;
            }

            var path = new List<TileCoordinate>();
            for (int tileId = beforeGoal; tileId != start; tileId = previous[tileId])
            {
                path.Add(map.GetCoordinate(tileId));
            }

            path.Reverse();
            return path;
        }

        // -1 when impassable.
        private int StepCost(TileCoordinate tile, bool rail)
        {
            TileMap map = _world.Map;
            if (map.GetObjectId(tile) != TileMap.NoObjectId ||
                map.GetStationId(tile) != TileMap.NoStationId ||
                map.GetRailStationId(tile) != TileMap.NoRailStationId)
            {
                return -1;
            }

            bool ownMode = rail ? map.GetRailTypeId(tile) != TileMap.NoRailTypeId : map.GetRoadTypeId(tile) != TileMap.NoRoadTypeId;
            bool otherMode = rail ? map.GetRoadTypeId(tile) != TileMap.NoRoadTypeId : map.GetRailTypeId(tile) != TileMap.NoRailTypeId;
            if (ownMode)
            {
                return 1;
            }

            return otherMode ? 6 : 2;
        }

        // ---------------------------------------------------------------- helpers

        private static IEnumerable<TileCoordinate> Neighbors(TileCoordinate tile)
        {
            yield return new TileCoordinate(tile.X + 1, tile.Y);
            yield return new TileCoordinate(tile.X - 1, tile.Y);
            yield return new TileCoordinate(tile.X, tile.Y + 1);
            yield return new TileCoordinate(tile.X, tile.Y - 1);
        }

        private static int Manhattan(TileCoordinate left, TileCoordinate right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private static double Throughput(VehicleDefinition vehicle)
        {
            int capacity = vehicle.Mode == VehicleMode.Rail ? vehicle.MaxWagons * vehicle.CapacityPerWagon : vehicle.CargoCapacity;
            return capacity * (vehicle.SpeedPerTick.RawValue / (double)FixedPoint.OneRawValue);
        }

        private VehicleDefinition BestType(bool rail, int year)
        {
            VehicleMode mode = rail ? VehicleMode.Rail : VehicleMode.Road;
            return _catalog.Vehicles
                .Where(vehicle => vehicle.Mode == mode && VehicleAvailability.CanPurchase(vehicle, _world, year))
                .OrderByDescending(Throughput)
                .First();
        }

        // A truck, or a locomotive with all its wagons.
        private long VehicleSetCost(bool rail, int year)
        {
            VehicleDefinition type = BestType(rail, year);
            if (!rail)
            {
                return type.Purchase;
            }

            VehicleDefinition wagon = _catalog.Vehicles.First(vehicle => vehicle.Mode == VehicleMode.RailWagon);
            return type.Purchase + (type.MaxWagons * wagon.Purchase);
        }

        private string VehicleTypeOf(Route route, int vehicleId)
        {
            if (route.IsRail)
            {
                return _world.TryGetRailVehicle(vehicleId, out RailVehicle? train) && train != null ? train.TypeId : string.Empty;
            }

            return _world.TryGetRoadVehicle(vehicleId, out RoadVehicle? truck) && truck != null ? truck.TypeId : string.Empty;
        }

        private long RouteIncome(Route route)
        {
            long income = 0L;
            foreach (int vehicleId in route.Vehicles)
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

        private TileCoordinate StationTile(int stationId, bool rail)
        {
            if (rail)
            {
                _world.TryGetRailStation(stationId, out RailStation? railStation);
                return railStation!.Coordinate;
            }

            _world.TryGetRoadStation(stationId, out RoadStation? roadStation);
            return roadStation!.Coordinate;
        }

        private string Name(int objectId)
        {
            return objectId < TownCount
                ? _world.Towns[objectId].Name
                : _world.Industries[objectId - TownCount].IndustryId + "#" + (objectId - TownCount).ToString(CultureInfo.InvariantCulture);
        }

        private bool Execute(ISimulationCommand<GeneratedWorld> command)
        {
            CommandProcessingResult result = _processor.Process(new Tick(_tick), command);
            if (!result.IsAccepted)
            {
                Log($"  rejected {command.GetType().Name}");
            }

            return result.IsAccepted;
        }

        private void Log(string message)
        {
            if (_options.Verbose)
            {
                Console.WriteLine("  " + message);
            }
        }

        // ---------------------------------------------------------------- results

        // Adds the full saved state to the rolling hash, so two builds that diverge at any point keep different hashes.
        private void HashState()
        {
            if (_stateHash != null)
            {
                StateHasher.Append(_stateHash, SaveSnapshot.Capture(_seed, new Tick(_tick), _world));
            }
        }

        private void TrackMonth(GameDate date)
        {
            double when = date.Year + ((date.Month - 1) / 12.0);
            int best = _world.Towns.Max(town => town.Population);
            IReadOnlyList<TownNeedTier> tiers = _catalog.TownNeeds.Tiers;
            for (int tier = 1; tier < tiers.Count && tier <= _result.BestTownTierYears.Length; tier++)
            {
                if (best >= tiers[tier].MinPopulation && double.IsPositiveInfinity(_result.BestTownTierYears[tier - 1]))
                {
                    _result.BestTownTierYears[tier - 1] = when;
                }
            }

            int milestone = (date.Year - 1910) / 10;
            if (date.Month == 1 && date.Year % 10 == 0 && milestone >= 0 && milestone < _result.TownsAtMilestoneLevel.Length)
            {
                _result.TownsAtMilestoneLevel[milestone] = _world.Towns.Count(town => _catalog.TownNeeds.GetTierIndexForPopulation(town.Population) >= milestone + 1);
            }
        }

        private void TrackMilestones()
        {
            if (_result.FirstMillionOn == null && _world.CompanyCash >= 1_000_000L)
            {
                _result.FirstMillionOn = Date;
            }

            if (_result.FirstTenMillionOn == null && _world.CompanyCash >= 10_000_000L)
            {
                _result.FirstTenMillionOn = Date;
            }
        }

        private void RecordYear(int year)
        {
            if (_result.LevelThreeTownDeliveryYear == null &&
                _catalog.Cargo.Any(cargo => cargo.ManufacturingLevel == 3 && _world.GetCargoDelivered(cargo.Id, DeliveryDestinationKind.Town) > 0))
            {
                _result.LevelThreeTownDeliveryYear = year;
            }

            int needs = 0;
            int met = 0;
            foreach (Town town in _world.Towns)
            {
                needs += town.Needs.Count;
                met += town.Needs.Count(need =>
                    _catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo) &&
                    cargo != null &&
                    need.MonthSupply >= _catalog.TownNeeds.DemandPerNeedPerMonth.Get(
                        Math.Clamp(cargo.ManufacturingLevel, 1, 3)));
            }

            List<int> populations = _world.Towns.Select(town => town.Population).ToList();
            _result.Years.Add(new YearRow
            {
                Year = year,
                Cash = _world.CompanyCash,
                Score = CompanyScore.Compute(_world, _catalog),
                Income = _world.Finance.Current.Get(FinanceCategory.DeliveryIncome) + _world.Finance.Current.Get(FinanceCategory.ContractBonus),
                ContractBonus = _world.Finance.Current.Get(FinanceCategory.ContractBonus),
                RoadVehicles = _world.RoadVehicles.Count,
                Trains = _world.RailVehicles.Count,
                Routes = _routes.Count,
                RoutesOpened = _routesOpenedThisYear,
                VehiclesAdded = _vehiclesAddedThisYear,
                Opportunities = CountOpportunities(),
                MinPopulation = populations.Min(),
                AveragePopulation = (int)populations.Average(),
                MaxPopulation = populations.Max(),
                TownsOver2k = populations.Count(population => population >= 2000),
                TownsOver5k = populations.Count(population => population >= 5000),
                NeedsMetPercent = needs == 0 ? 0 : 100 * met / needs,
                GrownIndustries = _world.Industries.Count(industry => industry.ProductionLevel > 0),
                StateHash = _stateHash == null ? null : Convert.ToHexString(_stateHash.GetCurrentHash()),
            });
            _routesOpenedThisYear = 0;
            _vehiclesAddedThisYear = 0;
        }

        private sealed class Route
        {
            public Route(bool isRail, int fromStation, int toStation, string cargo, int sourceObject, int targetObject, int pathLength)
            {
                IsRail = isRail;
                FromStation = fromStation;
                ToStation = toStation;
                Cargo = cargo;
                SourceObject = sourceObject;
                TargetObject = targetObject;
                PathLength = pathLength;
            }

            public bool IsRail { get; }

            public int FromStation { get; }

            public int ToStation { get; }

            public string Cargo { get; }

            public int SourceObject { get; }

            public int TargetObject { get; }

            public int PathLength { get; }

            public string Label { get; set; } = string.Empty;

            public List<int> Vehicles { get; } = new List<int>();

            public int? CoreRouteId { get; set; }
        }

        private sealed class Candidate
        {
            public Candidate(int source, int target, string cargo, int distance, double score)
            {
                Source = source;
                Target = target;
                Cargo = cargo;
                Distance = distance;
                Score = score;
            }

            public int Source { get; }

            public int Target { get; }

            public string Cargo { get; }

            public int Distance { get; }

            public double Score { get; set; }
        }
    }
}
