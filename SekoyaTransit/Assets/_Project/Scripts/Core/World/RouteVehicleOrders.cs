#nullable enable

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Shared order rules for vehicles whose stops were just rewritten from their route (Issue #229).
    /// </summary>
    internal static class RouteVehicleOrders
    {
        /// <summary>
        /// A vehicle standing on a station of its new stop list services that stop first, so it loads before leaving
        /// instead of driving off empty to the first stop (Issue #193 preview feedback).
        /// </summary>
        internal static void StartAtStandingStation(GeneratedWorld world, RoadVehicle vehicle)
        {
            if (vehicle.IsOnEdge || vehicle.Orders.Count < 2)
            {
                return;
            }

            for (int index = 0; index < vehicle.Orders.Count; index++)
            {
                if (world.TryGetRoadStation(vehicle.Orders[index].StationId, out RoadStation? station) &&
                    station != null &&
                    world.RoadGraph.TryGetNodeId(station.Coordinate, out int nodeId) &&
                    nodeId == vehicle.EdgeFromNodeId)
                {
                    vehicle.StartAtOrder(index);
                    return;
                }
            }
        }

        /// <inheritdoc cref="StartAtStandingStation(GeneratedWorld, RoadVehicle)" />
        internal static void StartAtStandingStation(GeneratedWorld world, RailVehicle vehicle)
        {
            if (vehicle.IsOnEdge || vehicle.Orders.Count < 2)
            {
                return;
            }

            for (int index = 0; index < vehicle.Orders.Count; index++)
            {
                if (world.TryGetRailStation(vehicle.Orders[index].StationId, out RailStation? station) &&
                    station != null &&
                    world.RailGraph.TryGetNodeId(station.Coordinate, out int nodeId) &&
                    nodeId == vehicle.EdgeFromNodeId)
                {
                    vehicle.StartAtOrder(index);
                    return;
                }
            }
        }
    }
}
