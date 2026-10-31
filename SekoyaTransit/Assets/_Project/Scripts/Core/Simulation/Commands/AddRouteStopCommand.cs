#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Appends a station to a route's stops (Issue #229). The first stop fixes the route's mode; road and rail
    /// stations cannot be mixed, and the same station cannot follow itself. Vehicles on the route get the new stops.
    /// </summary>
    public sealed class AddRouteStopCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly RouteMode _mode;
        private readonly int _stationId;

        /// <summary>
        /// Initializes an add-stop command for a road or rail station.
        /// </summary>
        public AddRouteStopCommand(int routeId, RouteMode mode, int stationId)
        {
            if (mode != RouteMode.Road && mode != RouteMode.Rail)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "A stop is a road or a rail station.");
            }

            _routeId = routeId;
            _mode = mode;
            _stationId = stationId;
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

            return RouteCommandResults.From(RouteRules.CheckAppendStop(state, _routeId, _mode, _stationId));
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
            route.AppendStop(_mode, new RouteStop(_stationId));
            state.ApplyRouteStops(route);
        }
    }
}
