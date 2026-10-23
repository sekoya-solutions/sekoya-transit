#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Advances rail vehicles along graph edges in FixedPoint units per tick.
    /// </summary>
    public static class RailVehicleMovement
    {
        /// <summary>
        /// Placeholder speed: one quarter tile per tick.
        /// </summary>
        public static readonly FixedPoint PlaceholderSpeed = FixedPoint.FromRaw(FixedPoint.OneRawValue / 4);

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

            AdvanceUnloadOrTravelAll(world, contentCatalog);
            IndustryCargoLoading.Distribute(world, calendarDate);
            FinishServicesAll(world);
        }

        /// <summary>
        /// Advances unload or travel for every rail vehicle (no industry load distribution).
        /// </summary>
        public static void AdvanceUnloadOrTravelAll(
            GeneratedWorld world,
            ContentCatalog? contentCatalog = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                AdvanceUnloadOrTravel(world, world.RailVehicles[index], contentCatalog);
            }
        }

        /// <summary>
        /// Finishes service and repaths for every rail vehicle that can leave its stop.
        /// </summary>
        public static void FinishServicesAll(GeneratedWorld world)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                if (!vehicle.IsServicingStop)
                {
                    continue;
                }

                if (!RailVehicleCargoTransfer.TryFinishService(world, vehicle))
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
            RailVehicle vehicle,
            ContentCatalog? contentCatalog = null,
            GameDate? calendarDate = null)
        {
            AdvanceUnloadOrTravel(world, vehicle, contentCatalog);
            if (vehicle.IsServicingStop && vehicle.IsUnloadComplete)
            {
                IndustryCargoLoading.Distribute(world, calendarDate);
                if (RailVehicleCargoTransfer.TryFinishService(world, vehicle))
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
            RailVehicle vehicle,
            ContentCatalog? contentCatalog)
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
                _ = RailVehicleCargoTransfer.AdvanceUnloadTick(world, vehicle, contentCatalog);
                return;
            }

            int graphVersion = world.RailGraph.Version;
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

            FixedPoint remaining = PlaceholderSpeed;
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

                if (!world.RailGraph.TryFindEdgeBetween(
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

                RailPath? path = vehicle.ActivePath;
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
        /// Builds a MapPlay placeholder order list: start station plus every other rail station
        /// reachable from it on the current graph (stable station-id order).
        /// </summary>
        public static int[] BuildAutoOrders(GeneratedWorld world, int startStationId)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            if (!TryGetStationNodeId(world, startStationId, out int startNodeId))
            {
                return new[] { startStationId };
            }

            var orders = new List<int> { startStationId };
            for (int index = 0; index < world.RailStations.Count; index++)
            {
                RailStation station = world.RailStations[index];
                if (station.Id == startStationId)
                {
                    continue;
                }

                if (!TryGetStationNodeId(world, station.Id, out int destinationNodeId))
                {
                    continue;
                }

                if (world.RailRouteCache.TryGetPath(world.RailGraph, startNodeId, destinationNodeId, out _))
                {
                    orders.Add(station.Id);
                }
            }

            return orders.ToArray();
        }

        /// <summary>
        /// Resolves a presentation tile coordinate from the vehicle's edge progress.
        /// </summary>
        public static bool TryGetPresentationTile(
            GeneratedWorld world,
            RailVehicle vehicle,
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
                if (!world.RailGraph.TryGetNode(vehicle.EdgeFromNodeId, out RailGraphNode? node) || node == null)
                {
                    tile = default;
                    return false;
                }

                tile = node.Coordinate;
                return true;
            }

            if (!world.RailGraph.TryFindEdgeBetween(
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    out RailGraphEdge? edge,
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
            RailVehicle vehicle,
            out TileCoordinate fromTile,
            out TileCoordinate toTile,
            out FixedPoint fraction)
        {
            fromTile = default;
            toTile = default;
            fraction = FixedPoint.Zero;

            if (!vehicle.IsOnEdge)
            {
                if (!world.RailGraph.TryGetNode(vehicle.EdgeFromNodeId, out RailGraphNode? node) || node == null)
                {
                    return false;
                }

                fromTile = node.Coordinate;
                toTile = node.Coordinate;
                return true;
            }

            if (!world.RailGraph.TryFindEdgeBetween(
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    out RailGraphEdge? edge,
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

        private static void HandleGraphVersionChange(GeneratedWorld world, RailVehicle vehicle)
        {
            vehicle.ActivePath = null;
            vehicle.PathEdgeIndex = 0;

            if (!vehicle.IsOnEdge)
            {
                if (!world.RailGraph.TryGetNode(vehicle.EdgeFromNodeId, out _))
                {
                    // Last node vanished; cannot snap further � stay stuck until a path exists from nowhere.
                    vehicle.SetStuck(true);
                }

                return;
            }

            // Mid-edge: keep traveling if the same endpoint pair still exists; otherwise snap to from-node.
            if (world.RailGraph.TryFindEdgeBetween(
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
            if (!world.RailGraph.TryGetNode(snapNode, out _) &&
                world.RailGraph.TryGetNode(vehicle.EdgeToNodeId, out _))
            {
                snapNode = vehicle.EdgeToNodeId;
            }

            if (world.RailGraph.TryGetNode(snapNode, out _))
            {
                vehicle.ParkAtNode(snapNode);
            }
            else
            {
                vehicle.SetStuck(true);
            }
        }

        private static bool TryEnsurePath(GeneratedWorld world, RailVehicle vehicle)
        {
            if (vehicle.ActivePath != null && vehicle.CachedGraphVersion == world.RailGraph.Version)
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
                if (!world.RailGraph.TryFindEdgeBetween(vehicle.EdgeFromNodeId, vehicle.EdgeToNodeId, out _, out _, out _))
                {
                    fromNodeId = vehicle.EdgeFromNodeId;
                    if (!world.RailGraph.TryGetNode(fromNodeId, out _))
                    {
                        return false;
                    }

                    vehicle.ParkAtNode(fromNodeId);
                }
            }

            if (!world.RailGraph.TryGetNode(fromNodeId, out _))
            {
                return false;
            }

            if (fromNodeId == destinationNodeId)
            {
                vehicle.ActivePath = new RailPath(new[] { fromNodeId }, Array.Empty<int>(), 0);
                vehicle.PathEdgeIndex = 0;
                return true;
            }

            if (!world.RailRouteCache.TryGetPath(world.RailGraph, fromNodeId, destinationNodeId, out RailPath? path) ||
                path == null)
            {
                return false;
            }

            vehicle.ActivePath = path;
            vehicle.PathEdgeIndex = 0;
            return true;
        }

        private static void OnFinishedEdge(RailVehicle vehicle, int departedNodeId, int arrivedNodeId)
        {
            RailPath? path = vehicle.ActivePath;
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

        private static bool TryBeginNextEdge(GeneratedWorld world, RailVehicle vehicle)
        {
            RailPath? path = vehicle.ActivePath;
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

            if (!world.RailGraph.TryFindEdgeBetween(fromNodeId, toNodeId, out _, out _, out _))
            {
                vehicle.ActivePath = null;
                return false;
            }

            vehicle.BeginEdge(fromNodeId, toNodeId);
            return true;
        }

        private static bool OnArrivedAtDestinationNode(GeneratedWorld world, RailVehicle vehicle)
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
                // Path finished but not at destination � treat as failure.
                return false;
            }

            // Begin unload/load at this stop; leave and repath after cargo transfer finishes.
            vehicle.BeginServicingStop();
            return true;
        }

        private static bool TryGetStationNodeId(GeneratedWorld world, int stationId, out int nodeId)
        {
            for (int index = 0; index < world.RailStations.Count; index++)
            {
                if (world.RailStations[index].Id == stationId)
                {
                    TileCoordinate coordinate = world.RailStations[index].Coordinate;
                    if (world.RailGraph.TryGetNodeId(coordinate, out nodeId))
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
    }
}
