#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// The route state saved from format version 27 (Issue #229): routes with their finance, the unassigned (off-map)
    /// vehicles, the next route id, and the day of the last daily upkeep charge.
    /// </summary>
    public sealed class RouteSaveData
    {
        private readonly ReadOnlyCollection<Route> _routes;
        private readonly ReadOnlyCollection<RoadVehicle> _unassignedRoadVehicles;
        private readonly ReadOnlyCollection<RailVehicle> _unassignedRailVehicles;

        /// <summary>
        /// Initializes the data with deep copies, routes sorted by id and vehicles sorted by id.
        /// </summary>
        public RouteSaveData(
            IReadOnlyList<Route> routes,
            IReadOnlyList<RoadVehicle> unassignedRoadVehicles,
            IReadOnlyList<RailVehicle> unassignedRailVehicles,
            int nextRouteId,
            int lastUpkeepDay)
        {
#pragma warning disable CA1510
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            if (unassignedRoadVehicles == null)
            {
                throw new ArgumentNullException(nameof(unassignedRoadVehicles));
            }

            if (unassignedRailVehicles == null)
            {
                throw new ArgumentNullException(nameof(unassignedRailVehicles));
            }
#pragma warning restore CA1510

            if (nextRouteId < GeneratedWorld.FirstRouteId)
            {
                throw new ArgumentOutOfRangeException(nameof(nextRouteId), nextRouteId, "Next route id must be positive.");
            }

            if (lastUpkeepDay < 0 || lastUpkeepDay > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(lastUpkeepDay), lastUpkeepDay, "Upkeep day must be 0 to 31.");
            }

            var routeCopy = new List<Route>(routes.Count);
            for (int index = 0; index < routes.Count; index++)
            {
                Route route = routes[index] ?? throw new ArgumentException("Routes cannot contain null.", nameof(routes));
                if (route.Id >= nextRouteId)
                {
                    throw new ArgumentException($"Route {route.Id} is not below the next route id.", nameof(routes));
                }

                routeCopy.Add(route.Copy());
            }

            routeCopy.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int index = 1; index < routeCopy.Count; index++)
            {
                if (routeCopy[index].Id == routeCopy[index - 1].Id)
                {
                    throw new ArgumentException($"Duplicate route id {routeCopy[index].Id}.", nameof(routes));
                }
            }

            var roadCopy = new List<RoadVehicle>(unassignedRoadVehicles.Count);
            for (int index = 0; index < unassignedRoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = unassignedRoadVehicles[index] ??
                    throw new ArgumentException("Vehicles cannot contain null.", nameof(unassignedRoadVehicles));
                if (vehicle.RouteId != RoadVehicle.NoRouteId || vehicle.Orders.Count != 0)
                {
                    throw new ArgumentException(
                        $"Unassigned road vehicle {vehicle.Id} has a route or orders.",
                        nameof(unassignedRoadVehicles));
                }

                roadCopy.Add(vehicle.Copy());
            }

            roadCopy.Sort((left, right) => left.Id.CompareTo(right.Id));
            var railCopy = new List<RailVehicle>(unassignedRailVehicles.Count);
            for (int index = 0; index < unassignedRailVehicles.Count; index++)
            {
                RailVehicle vehicle = unassignedRailVehicles[index] ??
                    throw new ArgumentException("Vehicles cannot contain null.", nameof(unassignedRailVehicles));
                if (vehicle.RouteId != RoadVehicle.NoRouteId || vehicle.Orders.Count != 0)
                {
                    throw new ArgumentException(
                        $"Unassigned rail vehicle {vehicle.Id} has a route or orders.",
                        nameof(unassignedRailVehicles));
                }

                railCopy.Add(vehicle.Copy());
            }

            railCopy.Sort((left, right) => left.Id.CompareTo(right.Id));
            _routes = routeCopy.AsReadOnly();
            _unassignedRoadVehicles = roadCopy.AsReadOnly();
            _unassignedRailVehicles = railCopy.AsReadOnly();
            NextRouteId = nextRouteId;
            LastUpkeepDay = lastUpkeepDay;
        }

        /// <summary>
        /// Gets an empty route state (a new game).
        /// </summary>
        public static RouteSaveData Empty
        {
            get
            {
                return new RouteSaveData(
                    Array.Empty<Route>(),
                    Array.Empty<RoadVehicle>(),
                    Array.Empty<RailVehicle>(),
                    GeneratedWorld.FirstRouteId,
                    0);
            }
        }

        /// <summary>
        /// Gets the routes in ascending id order.
        /// </summary>
        public IReadOnlyList<Route> Routes
        {
            get
            {
                return _routes;
            }
        }

        /// <summary>
        /// Gets the unassigned road vehicles in ascending id order.
        /// </summary>
        public IReadOnlyList<RoadVehicle> UnassignedRoadVehicles
        {
            get
            {
                return _unassignedRoadVehicles;
            }
        }

        /// <summary>
        /// Gets the unassigned trains in ascending id order.
        /// </summary>
        public IReadOnlyList<RailVehicle> UnassignedRailVehicles
        {
            get
            {
                return _unassignedRailVehicles;
            }
        }

        /// <summary>
        /// Gets the id the next created route receives.
        /// </summary>
        public int NextRouteId { get; }

        /// <summary>
        /// Gets the calendar day of the last daily upkeep charge, or 0 when unset.
        /// </summary>
        public int LastUpkeepDay { get; }

        /// <summary>
        /// Captures the route state of a world.
        /// </summary>
        public static RouteSaveData Capture(GeneratedWorld world)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            return new RouteSaveData(
                world.Routes,
                world.UnassignedRoadVehicles,
                world.UnassignedRailVehicles,
                world.NextRouteId,
                world.LastUpkeepDay);
        }
    }
}
