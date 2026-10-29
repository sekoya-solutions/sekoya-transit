#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Advances road vehicles along graph edges in FixedPoint units per tick.
    /// </summary>
    public static class RoadVehicleMovement
    {
        /// <summary>
        /// Fallback speed when no catalog is supplied (tests): one quarter tile per tick.
        /// </summary>
        public static readonly FixedPoint FallbackSpeed = FixedPoint.FromRaw(FixedPoint.OneRawValue / 4);

        /// <summary>
        /// Advances every vehicle in the world by one simulation tick.
        /// </summary>
        /// <param name="contentCatalog">
        /// Optional content catalog used to price accepted delivery unloads; omit in tests that ignore income.
        /// </param>
        /// <param name="calendarDate">
        /// Optional calendar date for industry month Transported credits during loads.
        /// </param>
        public static void AdvanceAll(
            GeneratedWorld world,
            ContentCatalog? contentCatalog = null,
            GameDate? calendarDate = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            AdvanceUnloadOrTravelAll(world, contentCatalog, calendarDate);
            IndustryCargoLoading.Distribute(world, contentCatalog, calendarDate);
            TownCargoLoading.Distribute(world, contentCatalog, calendarDate);
            FinishServicesAll(world, contentCatalog);
        }

        /// <summary>
        /// Advances unload or travel for every road vehicle (no industry load distribution).
        /// </summary>
        public static void AdvanceUnloadOrTravelAll(
            GeneratedWorld world,
            ContentCatalog? contentCatalog = null,
            GameDate? calendarDate = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                AdvanceUnloadOrTravel(world, world.RoadVehicles[index], contentCatalog, calendarDate);
            }
        }

        /// <summary>
        /// Finishes service and repaths for every road vehicle that can leave its stop.
        /// </summary>
        public static void FinishServicesAll(GeneratedWorld world, ContentCatalog? contentCatalog = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                if (!vehicle.IsServicingStop)
                {
                    continue;
                }

                if (!RoadVehicleCargoTransfer.TryFinishService(world, vehicle, contentCatalog))
                {
                    continue;
                }

                vehicle.AdvanceOrderIndex();
                vehicle.ActivePath = null;
                vehicle.PathEdgeIndex = 0;
                if (!TryEnsurePath(world, vehicle))
                {
                    vehicle.SetStuck(true);
                }
            }
        }

        /// <summary>
        /// Advances a single vehicle by one simulation tick (includes industry load pass).
        /// Prefer <see cref="AdvanceAll"/> when multiple vehicles share an industry.
        /// </summary>
        public static void Advance(
            GeneratedWorld world,
            RoadVehicle vehicle,
            ContentCatalog? contentCatalog = null,
            GameDate? calendarDate = null)
        {
            AdvanceUnloadOrTravel(world, vehicle, contentCatalog, calendarDate);
            if (vehicle.IsServicingStop && vehicle.IsUnloadComplete)
            {
                IndustryCargoLoading.Distribute(world, contentCatalog, calendarDate);
                TownCargoLoading.Distribute(world, contentCatalog, calendarDate);
                if (RoadVehicleCargoTransfer.TryFinishService(world, vehicle, contentCatalog))
                {
                    vehicle.AdvanceOrderIndex();
                    vehicle.ActivePath = null;
                    vehicle.PathEdgeIndex = 0;
                    if (!TryEnsurePath(world, vehicle))
                    {
                        vehicle.SetStuck(true);
                    }
                }
            }
        }

        private static void AdvanceUnloadOrTravel(
            GeneratedWorld world,
            RoadVehicle vehicle,
            ContentCatalog? contentCatalog,
            GameDate? calendarDate = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            if (vehicle.OrderStationIds.Count < 2)
            {
                return;
            }

            if (vehicle.IsServicingStop)
            {
                _ = RoadVehicleCargoTransfer.AdvanceUnloadTick(world, vehicle, contentCatalog, calendarDate);
                return;
            }

            int graphVersion = world.RoadGraph.Version;
            if (vehicle.CachedGraphVersion != graphVersion)
            {
                HandleGraphVersionChange(world, vehicle);
                vehicle.CachedGraphVersion = graphVersion;
            }

            if (vehicle.IsStuck)
            {
                if (!TryEnsurePath(world, vehicle))
                {
                    return;
                }

                vehicle.SetStuck(false);
            }

            if (!TryEnsurePath(world, vehicle))
            {
                vehicle.SetStuck(true);
                return;
            }

            FixedPoint remaining = ResolveSpeed(contentCatalog, vehicle.TypeId);
            int guard = 0;
            while (remaining > FixedPoint.Zero && guard < 64)
            {
                guard++;
                if (!vehicle.IsOnEdge)
                {
                    if (!TryBeginNextEdge(world, vehicle))
                    {
                        // Arrived at destination node with no further edges in path.
                        if (!OnArrivedAtDestinationNode(world, vehicle))
                        {
                            vehicle.SetStuck(true);
                            return;
                        }

                        // Cargo service begins this tick; do not spend remaining speed on travel.
                        if (vehicle.IsServicingStop)
                        {
                            return;
                        }

                        continue;
                    }
                }

                if (!world.RoadGraph.TryFindEdgeBetween(
                        vehicle.EdgeFromNodeId,
                        vehicle.EdgeToNodeId,
                        out _,
                        out int edgeLength,
                        out _))
                {
                    vehicle.ParkAtNode(vehicle.EdgeFromNodeId);
                    vehicle.ActivePath = null;
                    if (!TryEnsurePath(world, vehicle))
                    {
                        vehicle.SetStuck(true);
                        return;
                    }

                    continue;
                }

                FixedPoint edgeLengthFp = FixedPoint.FromInt64(edgeLength);
                FixedPoint distanceLeft = edgeLengthFp - vehicle.ProgressAlongEdge;
                if (remaining < distanceLeft)
                {
                    vehicle.SetProgress(vehicle.ProgressAlongEdge + remaining);
                    remaining = FixedPoint.Zero;
                    break;
                }

                remaining -= distanceLeft;
                int arrivedNodeId = vehicle.EdgeToNodeId;
                int departedNodeId = vehicle.EdgeFromNodeId;
                vehicle.ParkAtNode(arrivedNodeId);
                OnFinishedEdge(vehicle, departedNodeId, arrivedNodeId);

                RoadPath? path = vehicle.ActivePath;
                if (path == null || vehicle.PathEdgeIndex >= path.EdgeIds.Count)
                {
                    if (!OnArrivedAtDestinationNode(world, vehicle))
                    {
                        vehicle.SetStuck(true);
                        return;
                    }

                    if (vehicle.IsServicingStop)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Resolves a presentation tile coordinate from the vehicle's edge progress.
        /// </summary>
        public static bool TryGetPresentationTile(
            GeneratedWorld world,
            RoadVehicle vehicle,
            out TileCoordinate tile,
            out FixedPoint progressWithinTile)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            progressWithinTile = FixedPoint.Zero;
            if (!vehicle.IsOnEdge)
            {
                if (!world.RoadGraph.TryGetNode(vehicle.EdgeFromNodeId, out RoadGraphNode? node) || node == null)
                {
                    tile = default;
                    return false;
                }

                tile = node.Coordinate;
                return true;
            }

            if (!world.RoadGraph.TryFindEdgeBetween(
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    out RoadGraphEdge? edge,
                    out _,
                    out bool reverse) ||
                edge == null)
            {
                tile = default;
                return false;
            }

            IReadOnlyList<TileCoordinate> path = edge.Path;
            long wholeSteps = vehicle.ProgressAlongEdge.ToInt64();
            if (wholeSteps < 0)
            {
                wholeSteps = 0;
            }

            if (wholeSteps >= path.Count - 1)
            {
                tile = reverse ? path[0] : path[path.Count - 1];
                progressWithinTile = FixedPoint.Zero;
                return true;
            }

            FixedPoint fraction = vehicle.ProgressAlongEdge - FixedPoint.FromInt64(wholeSteps);
            int fromIndex;
            int toIndex;
            if (reverse)
            {
                fromIndex = path.Count - 1 - (int)wholeSteps;
                toIndex = fromIndex - 1;
            }
            else
            {
                fromIndex = (int)wholeSteps;
                toIndex = fromIndex + 1;
            }

            tile = path[fromIndex];
            progressWithinTile = fraction;
            // Retain toIndex for callers that interpolate; fraction is toward path[toIndex].
            _ = toIndex;
            return true;
        }

        /// <summary>
        /// Returns the next tile along the travel direction for presentation interpolation.
        /// </summary>
        public static bool TryGetPresentationSegment(
            GeneratedWorld world,
            RoadVehicle vehicle,
            out TileCoordinate fromTile,
            out TileCoordinate toTile,
            out FixedPoint fraction)
        {
            fromTile = default;
            toTile = default;
            fraction = FixedPoint.Zero;

            if (!vehicle.IsOnEdge)
            {
                if (!world.RoadGraph.TryGetNode(vehicle.EdgeFromNodeId, out RoadGraphNode? node) || node == null)
                {
                    return false;
                }

                fromTile = node.Coordinate;
                toTile = node.Coordinate;
                return true;
            }

            if (!world.RoadGraph.TryFindEdgeBetween(
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    out RoadGraphEdge? edge,
                    out _,
                    out bool reverse) ||
                edge == null)
            {
                return false;
            }

            IReadOnlyList<TileCoordinate> path = edge.Path;
            long wholeSteps = vehicle.ProgressAlongEdge.ToInt64();
            if (wholeSteps < 0)
            {
                wholeSteps = 0;
            }

            if (wholeSteps >= path.Count - 1)
            {
                TileCoordinate end = reverse ? path[0] : path[path.Count - 1];
                fromTile = end;
                toTile = end;
                fraction = FixedPoint.Zero;
                return true;
            }

            fraction = vehicle.ProgressAlongEdge - FixedPoint.FromInt64(wholeSteps);
            if (reverse)
            {
                int fromIndex = path.Count - 1 - (int)wholeSteps;
                fromTile = path[fromIndex];
                toTile = path[fromIndex - 1];
            }
            else
            {
                int fromIndex = (int)wholeSteps;
                fromTile = path[fromIndex];
                toTile = path[fromIndex + 1];
            }

            return true;
        }

        /// <summary>
        /// Rebuilds the road graph after a track or station edit and keeps every vehicle where it is (Issue #193). The
        /// rebuild can split an edge at a new junction or merge two edges when a junction disappears, so each
        /// vehicle's tile and the next tile it moves toward are read from the old graph first; afterwards the vehicle
        /// continues on the new edge holding those two tiles, at the same spot. A vehicle whose edge did not change
        /// keeps the same edge and progress; only its route is recomputed from the new graph. Done at the edit, not
        /// on the next tick, so no frame ever shows a vehicle on an edge that no longer exists.
        /// </summary>
        internal static void RebuildGraphKeepingVehicles(GeneratedWorld world)
        {
            IReadOnlyList<RoadVehicle> vehicles = world.RoadVehicles;
            TravelStep[] steps = vehicles.Count == 0 ? Array.Empty<TravelStep>() : new TravelStep[vehicles.Count];
            for (int index = 0; index < vehicles.Count; index++)
            {
                steps[index] = CaptureTravelStep(world, vehicles[index]);
            }

            world.RoadGraph.RebuildFromMap(world.Map);
            for (int index = 0; index < vehicles.Count; index++)
            {
                PlaceAfterRebuild(world, vehicles[index], steps[index]);
            }
        }

        // Reads the vehicle's tile and the next tile it moves toward from the current (old) graph.
        private static TravelStep CaptureTravelStep(GeneratedWorld world, RoadVehicle vehicle)
        {
            RoadGraph graph = world.RoadGraph;
            if (vehicle.CachedGraphVersion != graph.Version)
            {
                return default;
            }

            if (vehicle.IsOnEdge)
            {
                if (graph.TryFindEdgeBetween(
                        vehicle.EdgeFromNodeId,
                        vehicle.EdgeToNodeId,
                        out RoadGraphEdge? edge,
                        out _,
                        out bool reversed) &&
                    edge != null &&
                    GraphEdgeRelocation.TryGetTravelStep(
                        edge.Path,
                        reversed,
                        vehicle.ProgressAlongEdge,
                        out TileCoordinate tile,
                        out TileCoordinate next,
                        out FixedPoint fraction))
                {
                    return new TravelStep(tile, next, fraction);
                }

                return default;
            }

            // Stopped on a node: the next tile is the first step of the next edge on its route, when known.
            if (!graph.TryGetNode(vehicle.EdgeFromNodeId, out RoadGraphNode? node) || node == null)
            {
                return default;
            }

            RoadPath? path = vehicle.ActivePath;
            int edgeIndex = vehicle.PathEdgeIndex;
            if (path != null &&
                edgeIndex < path.EdgeIds.Count &&
                path.NodeIds[edgeIndex] == vehicle.EdgeFromNodeId &&
                graph.TryFindEdgeBetween(
                    path.NodeIds[edgeIndex],
                    path.NodeIds[edgeIndex + 1],
                    out RoadGraphEdge? nextEdge,
                    out _,
                    out bool nextReversed) &&
                nextEdge != null)
            {
                TileCoordinate firstStep = nextReversed
                    ? nextEdge.Path[nextEdge.Path.Count - 2]
                    : nextEdge.Path[1];
                return new TravelStep(node.Coordinate, firstStep, FixedPoint.Zero);
            }

            return new TravelStep(node.Coordinate);
        }

        // Puts the vehicle back on the rebuilt graph at its captured tile; leaves it for the next tick's
        // graph-change handling (snap to a node) only when the track under it is gone.
        private static void PlaceAfterRebuild(GeneratedWorld world, RoadVehicle vehicle, TravelStep step)
        {
            if (!step.HasTile)
            {
                return;
            }

            RoadGraph graph = world.RoadGraph;
            bool placed;
            if (!vehicle.IsOnEdge && graph.TryGetNodeId(step.Tile, out _))
            {
                // Still on a node (a station or a junction that survived): stay put.
                placed = true;
            }
            else if (step.HasNext &&
                graph.TryFindEdgeThrough(
                    step.Tile,
                    step.Next,
                    out RoadGraphEdge? edge,
                    out int tileIndex,
                    out bool reversed) &&
                edge != null)
            {
                vehicle.BeginEdge(reversed ? edge.NodeIdB : edge.NodeIdA, reversed ? edge.NodeIdA : edge.NodeIdB);
                vehicle.SetProgress(FixedPoint.FromInt64(tileIndex) + step.Fraction);
                placed = true;
            }
            else
            {
                placed = false;
            }

            if (placed)
            {
                vehicle.ActivePath = null;
                vehicle.PathEdgeIndex = 0;
                vehicle.CachedGraphVersion = graph.Version;
            }
        }

        private readonly struct TravelStep
        {
            public TravelStep(TileCoordinate tile)
            {
                HasTile = true;
                HasNext = false;
                Tile = tile;
                Next = default;
                Fraction = FixedPoint.Zero;
            }

            public TravelStep(TileCoordinate tile, TileCoordinate next, FixedPoint fraction)
            {
                HasTile = true;
                HasNext = true;
                Tile = tile;
                Next = next;
                Fraction = fraction;
            }

            public bool HasTile { get; }

            public bool HasNext { get; }

            public TileCoordinate Tile { get; }

            public TileCoordinate Next { get; }

            public FixedPoint Fraction { get; }
        }

        private static void HandleGraphVersionChange(GeneratedWorld world, RoadVehicle vehicle)
        {
            vehicle.ActivePath = null;
            vehicle.PathEdgeIndex = 0;

            if (!vehicle.IsOnEdge)
            {
                if (!world.RoadGraph.TryGetNode(vehicle.EdgeFromNodeId, out _))
                {
                    // Last node vanished; cannot snap further — stay stuck until a path exists from nowhere.
                    vehicle.SetStuck(true);
                }

                return;
            }

            // Mid-edge: keep traveling if the same endpoint pair still exists; otherwise snap to from-node.
            if (world.RoadGraph.TryFindEdgeBetween(
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    out _,
                    out int edgeLength,
                    out _))
            {
                FixedPoint max = FixedPoint.FromInt64(edgeLength);
                if (vehicle.ProgressAlongEdge > max)
                {
                    vehicle.SetProgress(max);
                }

                return;
            }

            int snapNode = vehicle.EdgeFromNodeId;
            if (!world.RoadGraph.TryGetNode(snapNode, out _) &&
                world.RoadGraph.TryGetNode(vehicle.EdgeToNodeId, out _))
            {
                snapNode = vehicle.EdgeToNodeId;
            }

            if (world.RoadGraph.TryGetNode(snapNode, out _))
            {
                vehicle.ParkAtNode(snapNode);
            }
            else
            {
                vehicle.SetStuck(true);
            }
        }

        private static bool TryEnsurePath(GeneratedWorld world, RoadVehicle vehicle)
        {
            if (vehicle.ActivePath != null && vehicle.CachedGraphVersion == world.RoadGraph.Version)
            {
                return true;
            }

            if (!vehicle.TryGetCurrentDestinationStationId(out int destinationStationId))
            {
                return false;
            }

            if (!TryGetStationNodeId(world, destinationStationId, out int destinationNodeId))
            {
                return false;
            }

            int fromNodeId = vehicle.IsOnEdge ? vehicle.EdgeToNodeId : vehicle.EdgeFromNodeId;
            if (vehicle.IsOnEdge)
            {
                // Finish current edge first; path starts at edge destination.
                if (!world.RoadGraph.TryFindEdgeBetween(vehicle.EdgeFromNodeId, vehicle.EdgeToNodeId, out _, out _, out _))
                {
                    fromNodeId = vehicle.EdgeFromNodeId;
                    if (!world.RoadGraph.TryGetNode(fromNodeId, out _))
                    {
                        return false;
                    }

                    vehicle.ParkAtNode(fromNodeId);
                }
            }

            if (!world.RoadGraph.TryGetNode(fromNodeId, out _))
            {
                return false;
            }

            if (fromNodeId == destinationNodeId)
            {
                vehicle.ActivePath = new RoadPath(new[] { fromNodeId }, Array.Empty<int>(), 0);
                vehicle.PathEdgeIndex = 0;
                return true;
            }

            if (!world.RoadRouteCache.TryGetPath(world.RoadGraph, fromNodeId, destinationNodeId, out RoadPath? path) ||
                path == null)
            {
                return false;
            }

            vehicle.ActivePath = path;
            vehicle.PathEdgeIndex = 0;
            return true;
        }

        private static void OnFinishedEdge(RoadVehicle vehicle, int departedNodeId, int arrivedNodeId)
        {
            RoadPath? path = vehicle.ActivePath;
            if (path == null || vehicle.PathEdgeIndex >= path.EdgeIds.Count)
            {
                return;
            }

            // Finished the next edge in the active path.
            if (path.NodeIds[vehicle.PathEdgeIndex] == departedNodeId &&
                path.NodeIds[vehicle.PathEdgeIndex + 1] == arrivedNodeId)
            {
                vehicle.PathEdgeIndex++;
                return;
            }

            // Finished an orphan mid-trip edge that only delivered us to the path start.
            if (vehicle.PathEdgeIndex == 0 && path.NodeIds[0] == arrivedNodeId)
            {
                return;
            }

            // Path no longer matches travel; force repath next tick.
            vehicle.ActivePath = null;
            vehicle.PathEdgeIndex = 0;
        }

        private static bool TryBeginNextEdge(GeneratedWorld world, RoadVehicle vehicle)
        {
            RoadPath? path = vehicle.ActivePath;
            if (path == null || vehicle.PathEdgeIndex >= path.EdgeIds.Count)
            {
                return false;
            }

            int fromNodeId = path.NodeIds[vehicle.PathEdgeIndex];
            int toNodeId = path.NodeIds[vehicle.PathEdgeIndex + 1];
            if (!vehicle.IsOnEdge && vehicle.EdgeFromNodeId != fromNodeId)
            {
                // Path was computed from a different node; repath.
                vehicle.ActivePath = null;
                return false;
            }

            if (!world.RoadGraph.TryFindEdgeBetween(fromNodeId, toNodeId, out _, out _, out _))
            {
                vehicle.ActivePath = null;
                return false;
            }

            vehicle.BeginEdge(fromNodeId, toNodeId);
            return true;
        }

        private static bool OnArrivedAtDestinationNode(GeneratedWorld world, RoadVehicle vehicle)
        {
            if (!vehicle.TryGetCurrentDestinationStationId(out int destinationStationId))
            {
                return false;
            }

            if (!TryGetStationNodeId(world, destinationStationId, out int destinationNodeId))
            {
                return false;
            }

            if (vehicle.EdgeFromNodeId != destinationNodeId)
            {
                // Path finished but not at destination — treat as failure.
                return false;
            }

            // Begin unload/load at this stop; leave and repath after cargo transfer finishes.
            vehicle.BeginServicingStop();
            return true;
        }

        private static bool TryGetStationNodeId(GeneratedWorld world, int stationId, out int nodeId)
        {
            for (int index = 0; index < world.RoadStations.Count; index++)
            {
                if (world.RoadStations[index].Id == stationId)
                {
                    TileCoordinate coordinate = world.RoadStations[index].Coordinate;
                    if (world.RoadGraph.TryGetNodeId(coordinate, out nodeId))
                    {
                        return true;
                    }

                    nodeId = 0;
                    return false;
                }
            }

            nodeId = 0;
            return false;
        }

        private static FixedPoint ResolveSpeed(ContentCatalog? contentCatalog, string typeId)
        {
            if (contentCatalog != null &&
                contentCatalog.TryGetVehicle(typeId, out VehicleDefinition? definition) &&
                definition != null &&
                definition.Mode == VehicleMode.Road)
            {
                return definition.SpeedPerTick;
            }

            return FallbackSpeed;
        }
    }
}
