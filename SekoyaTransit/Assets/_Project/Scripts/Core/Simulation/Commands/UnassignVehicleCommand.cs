#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Removes a vehicle from its route without selling it (Issue #229): it leaves the map, loses its cargo, and
    /// costs no upkeep until it joins a route again.
    /// </summary>
    public sealed class UnassignVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly int _vehicleId;

        /// <summary>
        /// Initializes an unassign command; the vehicle is a truck or a train depending on the route's mode.
        /// </summary>
        public UnassignVehicleCommand(int routeId, int vehicleId)
        {
            _routeId = routeId;
            _vehicleId = vehicleId;
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRoute(_routeId, out Route? route) || route == null)
            {
                return RouteCommandResults.From(RouteEditRefusal.MissingRoute);
            }

            int vehicleRouteId = RoadVehicle.NoRouteId;
            if (route.Mode == RouteMode.Road && state.TryGetRoadVehicle(_vehicleId, out RoadVehicle? truck) && truck != null)
            {
                vehicleRouteId = truck.RouteId;
            }
            else if (route.Mode == RouteMode.Rail && state.TryGetRailVehicle(_vehicleId, out RailVehicle? train) && train != null)
            {
                vehicleRouteId = train.RouteId;
            }

            return vehicleRouteId == _routeId
                ? CommandValidationResult.Valid
                : CommandValidationResult.Rejected("The vehicle does not run on this route.");
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            Route route = RouteCommandResults.Require(state, _routeId);
            if (route.Mode == RouteMode.Road)
            {
                state.UnassignRoadVehicle(_vehicleId);
            }
            else
            {
                state.UnassignRailVehicle(_vehicleId);
            }
        }
    }
}
