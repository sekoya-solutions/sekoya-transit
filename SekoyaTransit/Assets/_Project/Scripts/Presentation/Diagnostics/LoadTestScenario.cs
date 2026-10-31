#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Presentation.Diagnostics
{
    /// <summary>
    /// Development-only performance load (Issue #70): pairs nearby towns and industries, links each pair with a road
    /// or rail line between two stations, and fills the lines with vehicles, all through the normal player commands.
    /// The defaults give about 200 vehicles (20 road lines of 8 trucks, 10 rail lines of 4 trains).
    /// </summary>
    public static class LoadTestScenario
    {
        /// <summary>Cash credited before building so purchases are never refused for money.</summary>
        public const long LoadTestCash = 100_000_000L;

        private const int MinPairDistance = 10;
        private const int MaxPairDistance = 64;
        private const int WagonsPerTrain = 3;

        private static readonly TileCoordinate[] _orthogonalOffsets =
        {
            new TileCoordinate(0, -1),
            new TileCoordinate(1, 0),
            new TileCoordinate(0, 1),
            new TileCoordinate(-1, 0),
        };

        /// <summary>
        /// Builds the lines and buys the vehicles. Lines that cannot be placed or connected are skipped, so the
        /// result can hold fewer vehicles than requested on a crowded map.
        /// </summary>
        public static LoadTestScenarioResult Build(
            GeneratedWorld world,
            ContentCatalog catalog,
            SimulationCommandProcessor<GeneratedWorld> processor,
            int calendarYear,
            int roadLines = 20,
            int trucksPerLine = 8,
            int railLines = 10,
            int trainsPerLine = 4)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }
#pragma warning restore CA1510

            world.CreditCompanyCash(LoadTestCash, FinanceCategory.ContractBonus);
            var changedTiles = new List<TileCoordinate>();
            var search = new PathSearch(world.Map.TileCount);
            List<TileCoordinate> anchors = CollectAnchors(world);

            // A town or industry can host up to four stations, so anchors are reused; only a pair is never retried.
            var triedPairs = new HashSet<long>();
            int builtRoadLines = 0;
            int builtRailLines = 0;
            int roadVehicles = 0;
            int railVehicles = 0;
            bool progress = true;
            while (progress && (builtRoadLines < roadLines || builtRailLines < railLines))
            {
                progress = false;
                for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
                {
                    bool roadOpen = builtRoadLines < roadLines;
                    bool railOpen = builtRailLines < railLines;
                    if (!roadOpen && !railOpen)
                    {
                        break;
                    }

                    // Alternate in proportion to the quotas, so a crowded map still gets both road and rail lines.
                    bool wantRoad = roadOpen
                        && (!railOpen || (long)builtRoadLines * railLines <= (long)builtRailLines * roadLines);

                    int partnerIndex = FindPartner(anchors, triedPairs, anchorIndex);
                    if (partnerIndex < 0)
                    {
                        continue;
                    }

                    triedPairs.Add(PairKey(anchorIndex, partnerIndex));
                    TileCoordinate? stationA = FindFreeNeighbor(world, anchors[anchorIndex]);
                    TileCoordinate? stationB = FindFreeNeighbor(world, anchors[partnerIndex]);
                    if (!stationA.HasValue || !stationB.HasValue || stationA.Value.Equals(stationB.Value))
                    {
                        continue;
                    }

                    List<TileCoordinate>? path = search.Find(world, stationA.Value, stationB.Value, !wantRoad);
                    if (path == null)
                    {
                        continue;
                    }

                    progress = true;
                    BuildLine(wantRoad, stationA.Value, stationB.Value, path);
                }
            }

            return new LoadTestScenarioResult(builtRoadLines, roadVehicles, builtRailLines, railVehicles, changedTiles);

            void BuildLine(bool wantRoad, TileCoordinate stationA, TileCoordinate stationB, List<TileCoordinate> path)
            {
                if (wantRoad)
                {
                    int bought = BuildRoadLine(
                        world, catalog, processor, calendarYear, stationA, stationB, path, trucksPerLine);
                    if (bought > 0)
                    {
                        builtRoadLines++;
                        roadVehicles += bought;
                    }
                }
                else
                {
                    int bought = BuildRailLine(
                        world, catalog, processor, calendarYear, stationA, stationB, path, trainsPerLine);
                    if (bought > 0)
                    {
                        builtRailLines++;
                        railVehicles += bought;
                    }
                }

                changedTiles.Add(stationA);
                changedTiles.Add(stationB);
                changedTiles.AddRange(path);
            }
        }

        private static long PairKey(int first, int second)
        {
            int low = Math.Min(first, second);
            int high = Math.Max(first, second);
            return ((long)low << 32) | (uint)high;
        }

        private static int BuildRoadLine(
            GeneratedWorld world,
            ContentCatalog catalog,
            SimulationCommandProcessor<GeneratedWorld> processor,
            int calendarYear,
            TileCoordinate stationA,
            TileCoordinate stationB,
            List<TileCoordinate> path,
            int vehicleCount)
        {
            if (!processor.Process(Tick.Zero, new BuildRoadStationCommand(stationA)).IsAccepted
                || !processor.Process(Tick.Zero, new BuildRoadStationCommand(stationB)).IsAccepted)
            {
                return 0;
            }

            for (int index = 0; index < path.Count; index++)
            {
                if (world.Map.GetRoadTypeId(path[index]) == TileMap.NoRoadTypeId)
                {
                    processor.Process(Tick.Zero, new BuildRoadCommand(path[index]));
                }
            }

            world.TryGetRoadStationAt(stationA, out RoadStation? first);
            world.TryGetRoadStationAt(stationB, out RoadStation? second);
            if (first == null || second == null)
            {
                return 0;
            }

            // Half the fleet starts at each end (one route per direction) so the line fills in both directions.
            int forward = CreateRoute(world, processor, RouteMode.Road, first.Id, second.Id);
            int backward = CreateRoute(world, processor, RouteMode.Road, second.Id, first.Id);
            int bought = 0;
            for (int vehicle = 0; vehicle < vehicleCount; vehicle++)
            {
                int routeId = vehicle % 2 == 0 ? forward : backward;
                if (processor.Process(
                        Tick.Zero,
                        new BuyVehicleForRouteCommand(routeId, catalog, catalog.StartingRoadVehicle.Id, calendarYear))
                    .IsAccepted)
                {
                    bought++;
                }
            }

            return bought;
        }

        private static int BuildRailLine(
            GeneratedWorld world,
            ContentCatalog catalog,
            SimulationCommandProcessor<GeneratedWorld> processor,
            int calendarYear,
            TileCoordinate stationA,
            TileCoordinate stationB,
            List<TileCoordinate> path,
            int vehicleCount)
        {
            if (!processor.Process(Tick.Zero, new BuildRailStationCommand(stationA)).IsAccepted
                || !processor.Process(Tick.Zero, new BuildRailStationCommand(stationB)).IsAccepted)
            {
                return 0;
            }

            for (int index = 0; index < path.Count; index++)
            {
                processor.Process(Tick.Zero, new BuildRailCommand(path[index]));
            }

            world.TryGetRailStationAt(stationA, out RailStation? first);
            world.TryGetRailStationAt(stationB, out RailStation? second);
            if (first == null || second == null)
            {
                return 0;
            }

            int forward = CreateRoute(world, processor, RouteMode.Rail, first.Id, second.Id);
            int backward = CreateRoute(world, processor, RouteMode.Rail, second.Id, first.Id);
            int bought = 0;
            for (int vehicle = 0; vehicle < vehicleCount; vehicle++)
            {
                int routeId = vehicle % 2 == 0 ? forward : backward;
                if (!processor.Process(
                        Tick.Zero,
                        new BuyVehicleForRouteCommand(routeId, catalog, catalog.StartingRailLocomotive.Id, calendarYear))
                    .IsAccepted)
                {
                    continue;
                }

                // A train bought for a route already pulls one wagon.
                int vehicleId = world.RailVehicles[world.RailVehicles.Count - 1].Id;
                for (int wagon = 1; wagon < WagonsPerTrain; wagon++)
                {
                    processor.Process(Tick.Zero, new AddRailWagonCommand(vehicleId, catalog));
                }

                bought++;
            }

            return bought;
        }

        private static int CreateRoute(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            RouteMode mode,
            int fromStationId,
            int toStationId)
        {
            int routeId = world.NextRouteId;
            processor.Process(Tick.Zero, new CreateRouteCommand("Load test " + routeId.ToString(CultureInfo.InvariantCulture)));
            processor.Process(Tick.Zero, new AddRouteStopCommand(routeId, mode, fromStationId));
            processor.Process(Tick.Zero, new AddRouteStopCommand(routeId, mode, toStationId));
            return routeId;
        }

        private static List<TileCoordinate> CollectAnchors(GeneratedWorld world)
        {
            var anchors = new List<TileCoordinate>(world.Industries.Count + world.Towns.Count);
            for (int index = 0; index < world.Industries.Count; index++)
            {
                anchors.Add(world.Industries[index].Coordinate);
            }

            for (int index = 0; index < world.Towns.Count; index++)
            {
                anchors.Add(world.Towns[index].Coordinate);
            }

            return anchors;
        }

        private static int FindPartner(List<TileCoordinate> anchors, HashSet<long> triedPairs, int anchorIndex)
        {
            TileCoordinate anchor = anchors[anchorIndex];
            int best = -1;
            int bestDistance = int.MaxValue;
            for (int index = 0; index < anchors.Count; index++)
            {
                if (index == anchorIndex || triedPairs.Contains(PairKey(anchorIndex, index)))
                {
                    continue;
                }

                int distance = Math.Abs(anchors[index].X - anchor.X) + Math.Abs(anchors[index].Y - anchor.Y);
                if (distance >= MinPairDistance && distance <= MaxPairDistance && distance < bestDistance)
                {
                    best = index;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static TileCoordinate? FindFreeNeighbor(GeneratedWorld world, TileCoordinate anchor)
        {
            for (int index = 0; index < _orthogonalOffsets.Length; index++)
            {
                var neighbor = new TileCoordinate(
                    anchor.X + _orthogonalOffsets[index].X,
                    anchor.Y + _orthogonalOffsets[index].Y);
                if (world.Map.Contains(neighbor)
                    && world.Map.GetObjectId(neighbor) == TileMap.NoObjectId
                    && world.Map.GetStationId(neighbor) == TileMap.NoStationId
                    && world.Map.GetRoadTypeId(neighbor) == TileMap.NoRoadTypeId
                    && world.Map.GetRailTypeId(neighbor) == TileMap.NoRailTypeId)
                {
                    return neighbor;
                }
            }

            return null;
        }

        /// <summary>
        /// Breadth-first search between two station tiles, limited to a box around them. All buffers are reused and
        /// visited tiles are marked with a search number instead of clearing an array: the whole scenario runs in one
        /// frame, and WebGL cannot collect garbage until the frame ends (a first version ran out of memory).
        /// </summary>
        private sealed class PathSearch
        {
            private const int BoxMargin = 16;

            private readonly int[] _visitedBy;
            private readonly int[] _cameFrom;
            private readonly Queue<TileCoordinate> _queue = new Queue<TileCoordinate>();
            private readonly List<TileCoordinate> _path = new List<TileCoordinate>();
            private int _searchNumber;

            public PathSearch(int tileCount)
            {
                _visitedBy = new int[tileCount];
                _cameFrom = new int[tileCount];
            }

            // Returns the tiles strictly between the stations (a buffer reused by the next search), or null. Rail lines
            // avoid existing track so every line stays a separate two-station shuttle.
            public List<TileCoordinate>? Find(GeneratedWorld world, TileCoordinate from, TileCoordinate to, bool avoidRail)
            {
                _searchNumber++;
                int minX = Math.Min(from.X, to.X) - BoxMargin;
                int maxX = Math.Max(from.X, to.X) + BoxMargin;
                int minY = Math.Min(from.Y, to.Y) - BoxMargin;
                int maxY = Math.Max(from.Y, to.Y) + BoxMargin;
                TileMap map = world.Map;
                int fromId = map.GetTileId(from);
                _queue.Clear();
                _queue.Enqueue(from);
                _visitedBy[fromId] = _searchNumber;
                _cameFrom[fromId] = -1;
                bool found = false;
                while (_queue.Count > 0)
                {
                    TileCoordinate current = _queue.Dequeue();
                    if (current.Equals(to))
                    {
                        found = true;
                        break;
                    }

                    int currentId = map.GetTileId(current);
                    for (int index = 0; index < _orthogonalOffsets.Length; index++)
                    {
                        var next = new TileCoordinate(
                            current.X + _orthogonalOffsets[index].X,
                            current.Y + _orthogonalOffsets[index].Y);
                        if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY || !map.Contains(next))
                        {
                            continue;
                        }

                        int nextId = map.GetTileId(next);
                        if (_visitedBy[nextId] == _searchNumber)
                        {
                            continue;
                        }

                        if (!next.Equals(to)
                            && (map.GetObjectId(next) != TileMap.NoObjectId
                                || map.GetStationId(next) != TileMap.NoStationId
                                || (avoidRail && map.GetRailTypeId(next) != TileMap.NoRailTypeId)))
                        {
                            continue;
                        }

                        _visitedBy[nextId] = _searchNumber;
                        _cameFrom[nextId] = currentId;
                        _queue.Enqueue(next);
                    }
                }

                if (!found)
                {
                    return null;
                }

                _path.Clear();
                int cursor = _cameFrom[map.GetTileId(to)];
                while (cursor >= 0 && cursor != fromId)
                {
                    _path.Add(map.GetCoordinate(cursor));
                    cursor = _cameFrom[cursor];
                }

                _path.Reverse();
                return _path;
            }
        }
    }

    /// <summary>
    /// What <see cref="LoadTestScenario.Build"/> placed.
    /// </summary>
    public sealed class LoadTestScenarioResult
    {
        /// <summary>
        /// Initializes a result.
        /// </summary>
        public LoadTestScenarioResult(
            int roadLines,
            int roadVehicles,
            int railLines,
            int railVehicles,
            IReadOnlyList<TileCoordinate> changedTiles)
        {
            RoadLines = roadLines;
            RoadVehicles = roadVehicles;
            RailLines = railLines;
            RailVehicles = railVehicles;
            ChangedTiles = changedTiles;
        }

        /// <summary>Road lines that received at least one truck.</summary>
        public int RoadLines { get; }

        /// <summary>Trucks bought.</summary>
        public int RoadVehicles { get; }

        /// <summary>Rail lines that received at least one train.</summary>
        public int RailLines { get; }

        /// <summary>Locomotives bought.</summary>
        public int RailVehicles { get; }

        /// <summary>Tiles whose road, rail or station changed, for the map view to redraw.</summary>
        public IReadOnlyList<TileCoordinate> ChangedTiles { get; }
    }
}
