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
    /// Plays one game on the real Core like a steady human player: once per game month it saves for and buys vehicle
    /// unlocks, replaces outdated vehicles when rich, adds vehicles to routes whose cargo piles up, and opens at most
    /// <see cref="BalanceOptions.RoutesPerMonth"/> new routes (stations plus a road or rail path found by A*). Every
    /// action goes through the same commands as the game UI, so rejected actions cost nothing.
    /// </summary>
    internal sealed class ScriptedPlayer
    {
        private const int _ticksPerYear = 36000;
        private const int _maxRoutesPerSource = 5;
        private const int _maxRoadVehiclesPerRoute = 25;
        private const int _maxTrainsPerRoute = 6;
        private const int _busyFreightStock = 25;
        private const int _busyPassengerStock = 35;

        private readonly ContentCatalog _catalog;
        private readonly BalanceOptions _options;
        private readonly GeneratedWorld _world;
        private readonly SimulationCalendar _calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
        private readonly SimulationCommandProcessor<GeneratedWorld> _processor;
        private readonly EventBus _events = new EventBus();
        private readonly List<Route> _routes = new List<Route>();
        private readonly Dictionary<int, List<TileCoordinate>> _footprints = new Dictionary<int, List<TileCoordinate>>();
        private readonly Dictionary<(int ObjectId, bool Rail), int> _stations = new Dictionary<(int, bool), int>();
        private readonly HashSet<(int Source, int Target, string Cargo)> _failed = new HashSet<(int, int, string)>();
        private readonly GameResult _result;
        private long _tick;
        private int _routesOpenedThisYear;
        private int _vehiclesAddedThisYear;

        public ScriptedPlayer(ContentCatalog catalog, BalanceOptions options, ulong seed)
        {
            _catalog = catalog;
            _options = options;
            _world = NewGame.Create(new NewGameRequest(seed, options.Size), catalog);
            _processor = new SimulationCommandProcessor<GeneratedWorld>(_world);
            _result = new GameResult(seed, _world.Towns.Count, _world.Industries.Count);
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
            long upkeep = VehicleUpkeep.ComputeMonthlyUpkeep(_world, _catalog);
            long reserve = Math.Max(15000L, upkeep * 3L);
            long saving = PlanUnlock(date, reserve);

            // While saving, keep expanding with half of the money above the reserve and the target.
            long spendable = _world.CompanyCash - reserve - (saving / 2L);
            if (_options.ReplaceVehicles)
            {
                spendable -= ReplaceOutdated(date, spendable);
            }

            foreach (Route route in _routes.ToList())
            {
                if (spendable <= 0L)
                {
                    break;
                }

                if (!IsBusy(route))
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

            for (int attempt = 0; attempt < _options.RoutesPerMonth && spendable > 0L; attempt++)
            {
                Candidate? candidate = BestCandidate();
                if (candidate == null)
                {
                    break;
                }

                bool rail = _options.UseRail &&
                            candidate.Distance >= _options.RailMinDistance &&
                            !PassengerCargo.IsPassengerClass(_catalog, candidate.Cargo);
                long cost = VehicleSetCost(rail, date.Year) * (rail ? 1L : 2L);
                if (cost > spendable)
                {
                    break;
                }

                if (OpenRoute(candidate, rail, date))
                {
                    spendable -= cost;
                    _routesOpenedThisYear++;
                }
                else
                {
                    _failed.Add((candidate.Source, candidate.Target, candidate.Cargo));
                }
            }
        }

        // Returns the cash being saved for the oldest available locked vehicle (the one the player saw first) (0 when none), buying it when affordable.
        private long PlanUnlock(GameDate date, long reserve)
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

            if (target == null)
            {
                return 0L;
            }

            if (_world.CompanyCash < target.UnlockCost + reserve)
            {
                return target.UnlockCost;
            }

            if (Execute(new UnlockVehicleTypeCommand(target.Id, date.Year, _catalog)))
            {
                _result.Unlocks[target.Id].UnlockedOn = date;
                Log($"{date}: unlock {target.Id} for {target.UnlockCost}");
            }

            return 0L;
        }

        // Sells and rebuys vehicles that the best available type outperforms by 15 %, while the money really spent
        // (new price minus refund) leaves a margin; returns that money.
        private long ReplaceOutdated(GameDate date, long spendable)
        {
            long cashBefore = _world.CompanyCash;
            foreach (Route route in _routes)
            {
                VehicleDefinition best = BestType(route.IsRail, date.Year);
                foreach (int vehicleId in route.Vehicles.ToList())
                {
                    long cost = VehicleSetCost(route.IsRail, date.Year);
                    if (spendable - (cashBefore - _world.CompanyCash) < cost + 60000L)
                    {
                        return cashBefore - _world.CompanyCash;
                    }

                    if (!_catalog.TryGetVehicle(VehicleTypeOf(route, vehicleId), out VehicleDefinition? current) ||
                        current == null ||
                        current.Id == best.Id ||
                        Throughput(best) < Throughput(current) * 1.15)
                    {
                        continue;
                    }

                    bool sold = route.IsRail
                        ? Execute(new SellRailVehicleCommand(vehicleId, _catalog))
                        : Execute(new SellRoadVehicleCommand(vehicleId, _catalog));
                    if (!sold)
                    {
                        continue;
                    }

                    route.Vehicles.Remove(vehicleId);
                    AddVehicle(route, date);
                }
            }

            return cashBefore - _world.CompanyCash;
        }

        private bool IsBusy(Route route)
        {
            if (route.Vehicles.Count >= (route.IsRail ? _maxTrainsPerRoute : _maxRoadVehiclesPerRoute))
            {
                return false;
            }

            if (route.SourceObject < TownCount)
            {
                return _world.Towns[route.SourceObject].WaitingCargo.GetAmount(route.Cargo) >= _busyPassengerStock;
            }

            return _world.Industries[route.SourceObject - TownCount].Cargo.GetAmount(route.Cargo) >= _busyFreightStock;
        }

        // ---------------------------------------------------------------- route candidates

        private Candidate? BestCandidate()
        {
            Candidate? best = null;
            foreach (Candidate candidate in Candidates())
            {
                if (best == null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
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
                        if (_world.Towns[town].HasNeed(cargo) &&
                            !_routes.Any(route => route.TargetObject == town && route.Cargo == cargo))
                        {
                            Candidate? toTown = MakeCandidate(source, town, cargo, industry.Coordinate, _world.Towns[town].Coordinate);
                            if (toTown != null)
                            {
                                // A player likes to grow towns: a needed delivery is worth a little more than its pay.
                                toTown.Score *= 1.3;
                            }

                            bestTown = Better(bestTown, toTown);
                        }
                    }

                    // The chain to the next industry and the supply of a needy town are separate jobs.
                    if (best != null)
                    {
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
            if (distance > _options.MaxRouteDistance || _failed.Contains((source, target, cargo)) ||
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

        private bool OpenRoute(Candidate candidate, bool rail, GameDate date)
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

            if (!rail)
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
            bool waitForFull = !PassengerCargo.IsPassengerClass(_catalog, route.Cargo);
            if (route.IsRail)
            {
                if (!Execute(new BuyRailVehicleCommand(route.FromStation, _catalog, type.Id, date.Year)))
                {
                    return false;
                }

                int trainId = _world.RailVehicles[_world.RailVehicles.Count - 1].Id;
                for (int wagon = 0; wagon < type.MaxWagons; wagon++)
                {
                    Execute(new AddRailWagonCommand(trainId, _catalog));
                }

                Execute(new SetRailVehicleOrdersCommand(
                    trainId,
                    new[] { new RailVehicleOrder(route.FromStation, waitForFull), new RailVehicleOrder(route.ToStation) },
                    _catalog));
                route.Vehicles.Add(trainId);
                return true;
            }

            if (!Execute(new BuyRoadVehicleCommand(route.FromStation, _catalog, type.Id, date.Year)))
            {
                return false;
            }

            int truckId = _world.RoadVehicles[_world.RoadVehicles.Count - 1].Id;
            Execute(new SetRoadVehicleOrdersCommand(
                truckId,
                new[] { new RoadVehicleOrder(route.FromStation, waitForFull), new RoadVehicleOrder(route.ToStation) },
                _catalog));
            route.Vehicles.Add(truckId);
            return true;
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
                met += town.Needs.Count(need => need.MonthSupply >= _catalog.TownNeeds.DemandPerNeedPerMonth);
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
