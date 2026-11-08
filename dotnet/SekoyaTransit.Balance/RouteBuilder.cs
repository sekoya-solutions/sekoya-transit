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
    /// Building actions shared by the scripted players: stations next to towns and industries, A* road and rail
    /// paths, Core routes and vehicle choice. Every action goes through the same commands as the game UI.
    /// </summary>
    internal sealed class RouteBuilder
    {
        private readonly GeneratedWorld _world;
        private readonly ContentCatalog _catalog;
        private readonly SimulationCommandProcessor<GeneratedWorld> _processor;
        private readonly Func<long> _tick;
        private readonly Action<string> _log;
        private readonly Dictionary<int, List<TileCoordinate>> _footprints = new Dictionary<int, List<TileCoordinate>>();
        private readonly Dictionary<(int ObjectId, bool Rail), int> _stations = new Dictionary<(int, bool), int>();
        private readonly HashSet<int> _siteEdges = new HashSet<int>();
        private readonly bool _keepSiteEdgesFree;

        /// <summary>
        /// Initializes the builder; with <paramref name="keepSiteEdgesFree"/> paths never use a tile next to a town or
        /// industry, so those tiles stay free for the site's own stations.
        /// </summary>
        public RouteBuilder(GeneratedWorld world, ContentCatalog catalog, Func<long> tick, Action<string> log, bool keepSiteEdgesFree = false)
        {
            _keepSiteEdgesFree = keepSiteEdgesFree;
            _world = world;
            _catalog = catalog;
            _tick = tick;
            _log = log;
            _processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            for (int tileId = 0; tileId < world.Map.TileCount; tileId++)
            {
                TileCoordinate tile = world.Map.GetCoordinate(tileId);
                int objectId = world.Map.GetObjectId(tile);
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

            foreach (List<TileCoordinate> footprint in _footprints.Values)
            {
                foreach (TileCoordinate part in footprint)
                {
                    foreach (TileCoordinate edge in Neighbors(part))
                    {
                        if (world.Map.Contains(edge) && world.Map.GetObjectId(edge) == TileMap.NoObjectId)
                        {
                            _siteEdges.Add(world.Map.GetTileId(edge));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the number of towns; map object ids below it are towns, the others industries.
        /// </summary>
        public int TownCount => _world.TownAnchors.Count;

        /// <summary>
        /// Runs a command at the current tick; returns whether Core accepted it.
        /// </summary>
        public bool Execute(ISimulationCommand<GeneratedWorld> command)
        {
            CommandProcessingResult result = _processor.Process(new Tick(_tick()), command);
            if (!result.IsAccepted)
            {
                _log($"  rejected {command.GetType().Name}");
            }

            return result.IsAccepted;
        }

        /// <summary>
        /// Returns the player's station of the mode at a town or industry, building it on the free adjacent tile
        /// nearest to <paramref name="towardObjectId"/>; null when no tile fits.
        /// </summary>
        public int? EnsureStation(int objectId, bool rail, int towardObjectId)
        {
            if (_stations.TryGetValue((objectId, rail), out int existing))
            {
                return existing;
            }

            TileCoordinate? best = FindStationTile(objectId, rail, towardObjectId);
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

        /// <summary>
        /// Returns the tile of the player's station of the mode at a town or industry, or the tile
        /// <see cref="EnsureStation"/> would build it on, without building anything; null when no tile fits.
        /// </summary>
        public TileCoordinate? PeekStationTile(int objectId, bool rail, int towardObjectId)
        {
            return _stations.TryGetValue((objectId, rail), out int existing)
                ? StationTile(existing, rail)
                : FindStationTile(objectId, rail, towardObjectId);
        }

        /// <summary>
        /// Draws the tiles around a town or industry (2 tiles of margin), one character per tile: O the site itself,
        /// o another site, S road station, T rail station, # road and rail, = road, - rail, . empty.
        /// </summary>
        public string DrawSurroundings(int objectId)
        {
            List<TileCoordinate> footprint = _footprints[objectId];
            int minX = footprint.Min(tile => tile.X) - 2;
            int maxX = footprint.Max(tile => tile.X) + 2;
            int minY = footprint.Min(tile => tile.Y) - 2;
            int maxY = footprint.Max(tile => tile.Y) + 2;
            var text = new System.Text.StringBuilder();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var tile = new TileCoordinate(x, y);
                    if (!_world.Map.Contains(tile))
                    {
                        text.Append(' ');
                        continue;
                    }

                    int other = _world.Map.GetObjectId(tile);
                    bool road = _world.Map.GetRoadTypeId(tile) != TileMap.NoRoadTypeId;
                    bool railTrack = _world.Map.GetRailTypeId(tile) != TileMap.NoRailTypeId;
                    text.Append(
                        other == objectId ? 'O' :
                        other != TileMap.NoObjectId ? 'o' :
                        _world.Map.GetStationId(tile) != TileMap.NoStationId ? 'S' :
                        _world.Map.GetRailStationId(tile) != TileMap.NoRailStationId ? 'T' :
                        road && railTrack ? '#' : road ? '=' : railTrack ? '-' : '.');
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        private TileCoordinate? FindStationTile(int objectId, bool rail, int towardObjectId)
        {
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

            return best;
        }

        /// <summary>
        /// A* over 4-neighbour tiles between two station tiles; reuses own-mode track cheaply, crosses the other mode
        /// reluctantly. Returns the interior tiles, or null when no path fits the search limit.
        /// </summary>
        public List<TileCoordinate>? FindPath(TileCoordinate from, TileCoordinate to, bool rail)
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

        /// <summary>
        /// Builds road or rail on every path tile that does not have it yet.
        /// </summary>
        public void BuildPath(List<TileCoordinate> path, bool rail)
        {
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
        }

        /// <summary>
        /// Creates a two-stop Core route (Issue #229) that waits for a full load at the source except for passengers
        /// and mail, and goes empty-handed to the target; returns its id.
        /// </summary>
        public int CreateCoreRoute(int fromStation, int toStation, bool rail, string cargo)
        {
            int routeId = _world.NextRouteId;
            RouteMode mode = rail ? RouteMode.Rail : RouteMode.Road;
            Execute(new CreateRouteCommand("Balance " + routeId.ToString(CultureInfo.InvariantCulture)));
            Execute(new AddRouteStopCommand(routeId, mode, fromStation));
            Execute(new AddRouteStopCommand(routeId, mode, toStation));
            if (!PassengerCargo.IsPassengerClass(_catalog, cargo))
            {
                Execute(new SetRouteStopWaitForFullCommand(routeId, 0, true));
            }

            return routeId;
        }

        /// <summary>
        /// Gets the tile of a road or rail station.
        /// </summary>
        public TileCoordinate StationTile(int stationId, bool rail)
        {
            if (rail)
            {
                _world.TryGetRailStation(stationId, out RailStation? railStation);
                return railStation!.Coordinate;
            }

            _world.TryGetRoadStation(stationId, out RoadStation? roadStation);
            return roadStation!.Coordinate;
        }

        /// <summary>
        /// Gets the best vehicle type the player can buy for the mode this year (highest throughput).
        /// </summary>
        public VehicleDefinition BestType(bool rail, int year)
        {
            VehicleMode mode = rail ? VehicleMode.Rail : VehicleMode.Road;
            return _catalog.Vehicles
                .Where(vehicle => vehicle.Mode == mode && VehicleAvailability.CanPurchase(vehicle, _world, year))
                .OrderByDescending(Throughput)
                .First();
        }

        /// <summary>
        /// Gets the monthly upkeep of the whole fleet.
        /// </summary>
        public long MonthlyUpkeep()
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

        /// <summary>
        /// Gets a readable name of a town or industry.
        /// </summary>
        public string Name(int objectId)
        {
            return objectId < TownCount
                ? _world.Towns[objectId].Name
                : _world.Industries[objectId - TownCount].IndustryId + "#" + (objectId - TownCount).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the tile coordinate of a town or industry.
        /// </summary>
        public TileCoordinate Coordinate(int objectId)
        {
            return objectId < TownCount ? _world.Towns[objectId].Coordinate : _world.Industries[objectId - TownCount].Coordinate;
        }

        /// <summary>
        /// Gets the cargo capacity times the speed of a vehicle type (a train with all its wagons).
        /// </summary>
        public static double Throughput(VehicleDefinition vehicle)
        {
            int capacity = vehicle.Mode == VehicleMode.Rail ? vehicle.MaxWagons * vehicle.CapacityPerWagon : vehicle.CargoCapacity;
            return capacity * (vehicle.SpeedPerTick.RawValue / (double)FixedPoint.OneRawValue);
        }

        /// <summary>
        /// Gets the Manhattan distance between two tiles.
        /// </summary>
        public static int Manhattan(TileCoordinate left, TileCoordinate right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private static IEnumerable<TileCoordinate> Neighbors(TileCoordinate tile)
        {
            yield return new TileCoordinate(tile.X + 1, tile.Y);
            yield return new TileCoordinate(tile.X - 1, tile.Y);
            yield return new TileCoordinate(tile.X, tile.Y + 1);
            yield return new TileCoordinate(tile.X, tile.Y - 1);
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

        // -1 when impassable.
        private int StepCost(TileCoordinate tile, bool rail)
        {
            TileMap map = _world.Map;
            if (_keepSiteEdgesFree && _siteEdges.Contains(map.GetTileId(tile)))
            {
                return -1;
            }

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
    }
}
