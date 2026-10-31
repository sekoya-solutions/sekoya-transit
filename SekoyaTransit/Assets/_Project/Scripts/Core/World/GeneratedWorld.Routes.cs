#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Routes and unassigned vehicles (Issue #229). Vehicles in <see cref="RoadVehicles"/> and
    /// <see cref="RailVehicles"/> are on the map and each runs on a valid route; unassigned vehicles are owned but
    /// kept off the map, carry nothing, and cost no upkeep.
    /// </summary>
    public sealed partial class GeneratedWorld
    {
        /// <summary>
        /// Id given to the first route of a world.
        /// </summary>
        public const int FirstRouteId = 1;

        private readonly List<Route> _routes = new List<Route>();
        private readonly List<RoadVehicle> _unassignedRoadVehicles = new List<RoadVehicle>();
        private readonly List<RailVehicle> _unassignedRailVehicles = new List<RailVehicle>();
        private ReadOnlyCollection<Route>? _routesView;
        private ReadOnlyCollection<RoadVehicle>? _unassignedRoadVehiclesView;
        private ReadOnlyCollection<RailVehicle>? _unassignedRailVehiclesView;

        /// <summary>
        /// Gets the routes in ascending id order.
        /// </summary>
        public IReadOnlyList<Route> Routes
        {
            get
            {
                return _routesView ??= _routes.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the id the next created route receives.
        /// </summary>
        public int NextRouteId { get; private set; } = FirstRouteId;

        /// <summary>
        /// Gets the owned road vehicles that are not on a route (off the map), in ascending id order.
        /// </summary>
        public IReadOnlyList<RoadVehicle> UnassignedRoadVehicles
        {
            get
            {
                return _unassignedRoadVehiclesView ??= _unassignedRoadVehicles.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the owned trains that are not on a route (off the map), in ascending id order.
        /// </summary>
        public IReadOnlyList<RailVehicle> UnassignedRailVehicles
        {
            get
            {
                return _unassignedRailVehiclesView ??= _unassignedRailVehicles.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the number of owned road vehicles, on the map or unassigned.
        /// </summary>
        public int OwnedRoadVehicleCount
        {
            get
            {
                return _roadVehicles.Count + _unassignedRoadVehicles.Count;
            }
        }

        /// <summary>
        /// Gets the number of owned trains, on the map or unassigned.
        /// </summary>
        public int OwnedRailVehicleCount
        {
            get
            {
                return _railVehicles.Count + _unassignedRailVehicles.Count;
            }
        }

        /// <summary>
        /// Gets the calendar day of the last day vehicle upkeep was charged for, or 0 when unset.
        /// </summary>
        public int LastUpkeepDay { get; private set; }

        /// <summary>
        /// Returns the route with the given id when it exists.
        /// </summary>
        public bool TryGetRoute(int routeId, out Route? route)
        {
            for (int index = 0; index < _routes.Count; index++)
            {
                if (_routes[index].Id == routeId)
                {
                    route = _routes[index];
                    return true;
                }
            }

            route = null;
            return false;
        }

        /// <summary>
        /// Returns whether another route than <paramref name="exceptRouteId"/> already uses <paramref name="name"/>,
        /// ignoring case.
        /// </summary>
        public bool IsRouteNameTaken(string name, int exceptRouteId = RoadVehicle.NoRouteId)
        {
            for (int index = 0; index < _routes.Count; index++)
            {
                if (_routes[index].Id != exceptRouteId &&
                    string.Equals(_routes[index].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the owned road vehicle with the given id, on the map or unassigned.
        /// </summary>
        public bool TryGetOwnedRoadVehicle(int vehicleId, out RoadVehicle? vehicle)
        {
            if (TryGetRoadVehicle(vehicleId, out vehicle))
            {
                return true;
            }

            return TryFind(_unassignedRoadVehicles, vehicleId, out vehicle, out _);
        }

        /// <summary>
        /// Returns the owned train with the given id, on the map or unassigned.
        /// </summary>
        public bool TryGetOwnedRailVehicle(int vehicleId, out RailVehicle? vehicle)
        {
            if (TryGetRailVehicle(vehicleId, out vehicle))
            {
                return true;
            }

            return TryFind(_unassignedRailVehicles, vehicleId, out vehicle, out _);
        }

        /// <summary>
        /// Returns the unassigned road vehicle with the given id when it exists.
        /// </summary>
        public bool TryGetUnassignedRoadVehicle(int vehicleId, out RoadVehicle? vehicle)
        {
            return TryFind(_unassignedRoadVehicles, vehicleId, out vehicle, out _);
        }

        /// <summary>
        /// Returns the unassigned train with the given id when it exists.
        /// </summary>
        public bool TryGetUnassignedRailVehicle(int vehicleId, out RailVehicle? vehicle)
        {
            return TryFind(_unassignedRailVehicles, vehicleId, out vehicle, out _);
        }

        /// <summary>
        /// Credits delivery income to a route's current month; ignored for <see cref="RoadVehicle.NoRouteId"/>.
        /// </summary>
        internal void AddRouteRevenue(int routeId, long amount)
        {
            if (routeId != RoadVehicle.NoRouteId && TryGetRoute(routeId, out Route? route) && route != null)
            {
                route.AddRevenue(amount);
            }
        }

        /// <summary>
        /// Creates a new unassigned road vehicle (bought, not yet on the map).
        /// </summary>
        internal RoadVehicle AddUnassignedRoadVehicle(string typeId, int cargoCapacity)
        {
            int id = _nextRoadVehicleId;
            _nextRoadVehicleId = checked(_nextRoadVehicleId + 1);
            var vehicle = new RoadVehicle(id, typeId, 0, cargoCapacity);
            InsertUnassignedSorted(vehicle);
            return vehicle;
        }

        /// <summary>
        /// Creates a new unassigned locomotive without wagons (bought, not yet on the map).
        /// </summary>
        internal RailVehicle AddUnassignedRailVehicle(string typeId, int maxWagons, int capacityPerWagon)
        {
            int id = _nextRailVehicleId;
            _nextRailVehicleId = checked(_nextRailVehicleId + 1);
            var vehicle = new RailVehicle(id, typeId, 0, maxWagons, capacityPerWagon);
            InsertUnassignedSorted(vehicle);
            return vehicle;
        }

        internal Route AddRoute(string name)
        {
            var route = new Route(NextRouteId, name);
            NextRouteId = checked(NextRouteId + 1);
            _routes.Add(route);
            return route;
        }

        internal void RemoveRoute(int routeId)
        {
            for (int index = 0; index < _routes.Count; index++)
            {
                if (_routes[index].Id == routeId)
                {
                    _routes.RemoveAt(index);
                    return;
                }
            }
        }

        /// <summary>
        /// Rewrites the orders of every vehicle on <paramref name="route"/> from its stops, or unassigns them all when
        /// the route is no longer valid.
        /// </summary>
        internal void ApplyRouteStops(Route route)
        {
            if (!route.IsValid)
            {
                UnassignRouteVehicles(route.Id);
                return;
            }

            if (route.Mode == RouteMode.Road)
            {
                RoadVehicleOrder[] orders = route.ToRoadOrders();
                for (int index = 0; index < _roadVehicles.Count; index++)
                {
                    RoadVehicle vehicle = _roadVehicles[index];
                    if (vehicle.RouteId == route.Id)
                    {
                        vehicle.SetOrders(orders);
                        RouteVehicleOrders.StartAtStandingStation(this, vehicle);
                    }
                }
            }
            else if (route.Mode == RouteMode.Rail)
            {
                RailVehicleOrder[] orders = route.ToRailOrders();
                for (int index = 0; index < _railVehicles.Count; index++)
                {
                    RailVehicle vehicle = _railVehicles[index];
                    if (vehicle.RouteId == route.Id)
                    {
                        vehicle.SetOrders(orders);
                        RouteVehicleOrders.StartAtStandingStation(this, vehicle);
                    }
                }
            }
        }

        /// <summary>
        /// Takes every vehicle of a route off the map: they lose their cargo and become unassigned.
        /// </summary>
        internal void UnassignRouteVehicles(int routeId)
        {
            for (int index = _roadVehicles.Count - 1; index >= 0; index--)
            {
                if (_roadVehicles[index].RouteId == routeId)
                {
                    UnassignRoadVehicleAt(index);
                }
            }

            for (int index = _railVehicles.Count - 1; index >= 0; index--)
            {
                if (_railVehicles[index].RouteId == routeId)
                {
                    UnassignRailVehicleAt(index);
                }
            }
        }

        /// <summary>
        /// Takes an on-map road vehicle off the map, empty and unassigned.
        /// </summary>
        internal void UnassignRoadVehicle(int vehicleId)
        {
            if (!TryFind(_roadVehicles, vehicleId, out _, out int index))
            {
                throw new InvalidOperationException($"Road vehicle {vehicleId} is not on the map.");
            }

            UnassignRoadVehicleAt(index);
        }

        /// <summary>
        /// Takes an on-map train off the map, empty and unassigned.
        /// </summary>
        internal void UnassignRailVehicle(int vehicleId)
        {
            if (!TryFind(_railVehicles, vehicleId, out _, out int index))
            {
                throw new InvalidOperationException($"Rail vehicle {vehicleId} is not on the map.");
            }

            UnassignRailVehicleAt(index);
        }

        /// <summary>
        /// Puts an unassigned road vehicle on a valid road route, empty, at the route's first station.
        /// </summary>
        internal RoadVehicle DeployRoadVehicle(int vehicleId, Route route)
        {
            if (!TryFind(_unassignedRoadVehicles, vehicleId, out RoadVehicle? stored, out int index) || stored == null)
            {
                throw new InvalidOperationException($"Road vehicle {vehicleId} is not unassigned.");
            }

            int nodeId = GetRouteStartNode(route, RouteMode.Road);
            RoadVehicle deployed = stored.CreateParkedCopy(nodeId, route.ToRoadOrders(), route.Id);
            _unassignedRoadVehicles.RemoveAt(index);
            InsertVehicleSorted(deployed);
            return deployed;
        }

        /// <summary>
        /// Puts an unassigned train on a valid rail route, empty, at the route's first station.
        /// </summary>
        internal RailVehicle DeployRailVehicle(int vehicleId, Route route)
        {
            if (!TryFind(_unassignedRailVehicles, vehicleId, out RailVehicle? stored, out int index) || stored == null)
            {
                throw new InvalidOperationException($"Rail vehicle {vehicleId} is not unassigned.");
            }

            int nodeId = GetRouteStartNode(route, RouteMode.Rail);
            RailVehicle deployed = stored.CreateParkedCopy(nodeId, route.ToRailOrders(), route.Id);
            _unassignedRailVehicles.RemoveAt(index);
            InsertRailVehicleSorted(deployed);
            return deployed;
        }

        /// <summary>
        /// Removes a station from every route of its mode; routes left with fewer than two stops unassign their
        /// vehicles.
        /// </summary>
        internal void RemoveStationFromRoutes(RouteMode mode, int stationId)
        {
            for (int index = 0; index < _routes.Count; index++)
            {
                Route route = _routes[index];
                if (route.Mode == mode && route.RemoveStation(stationId))
                {
                    ApplyRouteStops(route);
                }
            }
        }

        internal void RestoreRoute(Route route)
        {
#pragma warning disable CA1510
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
#pragma warning restore CA1510

            int insertAt = _routes.Count;
            for (int index = 0; index < _routes.Count; index++)
            {
                if (_routes[index].Id == route.Id)
                {
                    throw new ArgumentException($"Duplicate route id {route.Id}.", nameof(route));
                }

                if (_routes[index].Id > route.Id && insertAt == _routes.Count)
                {
                    insertAt = index;
                }
            }

            _routes.Insert(insertAt, route);
            if (route.Id >= NextRouteId)
            {
                NextRouteId = checked(route.Id + 1);
            }
        }

        internal void RestoreNextRouteId(int nextRouteId)
        {
            if (nextRouteId < FirstRouteId)
            {
                throw new ArgumentOutOfRangeException(nameof(nextRouteId), nextRouteId, "Next route id must be positive.");
            }

            if (nextRouteId > NextRouteId)
            {
                NextRouteId = nextRouteId;
            }
        }

        internal void RestoreUnassignedRoadVehicle(RoadVehicle vehicle)
        {
#pragma warning disable CA1510
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            InsertUnassignedSorted(vehicle);
            AdvanceNextRoadVehicleIdPast(vehicle.Id);
        }

        internal void RestoreUnassignedRailVehicle(RailVehicle vehicle)
        {
#pragma warning disable CA1510
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            InsertUnassignedSorted(vehicle);
            AdvanceNextRailVehicleIdPast(vehicle.Id);
        }

        /// <summary>
        /// Records the calendar day that vehicle upkeep was last charged for (0/0/0 when unset).
        /// </summary>
        internal void SetLastUpkeepDate(int year, int month, int day)
        {
            SetLastUpkeepMonth(year, month);
            if (day < 0 || day > 31 || (month == 0) != (day == 0))
            {
                throw new ArgumentOutOfRangeException(nameof(day), day, "Upkeep day must be unset with the month, or 1 to 31.");
            }

            LastUpkeepDay = day;
        }

        private static int IndexOf(List<RoadVehicle> vehicles, int vehicleId)
        {
            for (int index = 0; index < vehicles.Count; index++)
            {
                if (vehicles[index].Id == vehicleId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int IndexOf(List<RailVehicle> vehicles, int vehicleId)
        {
            for (int index = 0; index < vehicles.Count; index++)
            {
                if (vehicles[index].Id == vehicleId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool TryFind(List<RoadVehicle> vehicles, int vehicleId, out RoadVehicle? vehicle, out int index)
        {
            index = IndexOf(vehicles, vehicleId);
            vehicle = index >= 0 ? vehicles[index] : null;
            return index >= 0;
        }

        private static bool TryFind(List<RailVehicle> vehicles, int vehicleId, out RailVehicle? vehicle, out int index)
        {
            index = IndexOf(vehicles, vehicleId);
            vehicle = index >= 0 ? vehicles[index] : null;
            return index >= 0;
        }

        // Ids are unique across the on-map and unassigned lists of a mode; each list stays sorted by id.
        private void InsertUnassignedSorted(RoadVehicle vehicle)
        {
            if (IndexOf(_roadVehicles, vehicle.Id) >= 0 || IndexOf(_unassignedRoadVehicles, vehicle.Id) >= 0)
            {
                throw new ArgumentException($"Duplicate road vehicle id {vehicle.Id}.", nameof(vehicle));
            }

            int insertAt = _unassignedRoadVehicles.Count;
            for (int index = 0; index < _unassignedRoadVehicles.Count; index++)
            {
                if (_unassignedRoadVehicles[index].Id > vehicle.Id)
                {
                    insertAt = index;
                    break;
                }
            }

            _unassignedRoadVehicles.Insert(insertAt, vehicle);
        }

        private void InsertUnassignedSorted(RailVehicle vehicle)
        {
            if (IndexOf(_railVehicles, vehicle.Id) >= 0 || IndexOf(_unassignedRailVehicles, vehicle.Id) >= 0)
            {
                throw new ArgumentException($"Duplicate rail vehicle id {vehicle.Id}.", nameof(vehicle));
            }

            int insertAt = _unassignedRailVehicles.Count;
            for (int index = 0; index < _unassignedRailVehicles.Count; index++)
            {
                if (_unassignedRailVehicles[index].Id > vehicle.Id)
                {
                    insertAt = index;
                    break;
                }
            }

            _unassignedRailVehicles.Insert(insertAt, vehicle);
        }

        private void UnassignRoadVehicleAt(int index)
        {
            RoadVehicle vehicle = _roadVehicles[index];
            _roadVehicles.RemoveAt(index);
            RoadVehicle parked = vehicle.CreateParkedCopy(
                vehicle.EdgeFromNodeId,
                Array.Empty<RoadVehicleOrder>(),
                RoadVehicle.NoRouteId);
            InsertUnassignedSorted(parked);
        }

        private void UnassignRailVehicleAt(int index)
        {
            RailVehicle vehicle = _railVehicles[index];
            _railVehicles.RemoveAt(index);
            RailVehicle parked = vehicle.CreateParkedCopy(
                vehicle.EdgeFromNodeId,
                Array.Empty<RailVehicleOrder>(),
                RoadVehicle.NoRouteId);
            InsertUnassignedSorted(parked);
        }

        private int GetRouteStartNode(Route route, RouteMode mode)
        {
            if (route.Mode != mode || !route.IsValid)
            {
                throw new InvalidOperationException($"Route {route.Id} is not a valid {mode} route.");
            }

            int stationId = route.Stops[0].StationId;
            if (mode == RouteMode.Road)
            {
                if (TryGetRoadStation(stationId, out RoadStation? station) && station != null &&
                    RoadGraph.TryGetNodeId(station.Coordinate, out int nodeId))
                {
                    return nodeId;
                }
            }
            else if (TryGetRailStation(stationId, out RailStation? station) && station != null &&
                RailGraph.TryGetNodeId(station.Coordinate, out int nodeId))
            {
                return nodeId;
            }

            throw new InvalidOperationException($"Route {route.Id} starts at a station that is not on the graph.");
        }
    }
}
