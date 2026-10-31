#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Puts an unassigned vehicle of the route's mode on a valid route, empty, at the route's first station
    /// (Issue #229).
    /// </summary>
    public sealed class AssignVehicleToRouteCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly int _vehicleId;

        /// <summary>
        /// Initializes an assign command; the vehicle is a truck or a train depending on the route's mode.
        /// </summary>
        public AssignVehicleToRouteCommand(int routeId, int vehicleId)
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

            if (!route.IsValid)
            {
                return CommandValidationResult.Rejected("A route needs at least two stops before it gets vehicles.");
            }

            bool unassigned = route.Mode == RouteMode.Road
                ? state.TryGetUnassignedRoadVehicle(_vehicleId, out _)
                : state.TryGetUnassignedRailVehicle(_vehicleId, out _);
            return unassigned
                ? CommandValidationResult.Valid
                : CommandValidationResult.Rejected("No unassigned vehicle of the route's mode has this id.");
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
                state.DeployRoadVehicle(_vehicleId, route);
            }
            else
            {
                state.DeployRailVehicle(_vehicleId, route);
            }
        }
    }
}
