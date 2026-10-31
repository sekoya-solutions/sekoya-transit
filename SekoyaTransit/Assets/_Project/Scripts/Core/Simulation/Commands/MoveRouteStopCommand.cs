#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Moves a route stop one place up (towards the first stop) or down (Issue #229). Refused when it would put the
    /// same station twice in a row. Vehicles on the route get the new order.
    /// </summary>
    public sealed class MoveRouteStopCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly int _stopIndex;
        private readonly bool _up;

        /// <summary>
        /// Initializes a move command for the stop at <paramref name="stopIndex"/>.
        /// </summary>
        public MoveRouteStopCommand(int routeId, int stopIndex, bool up)
        {
            _routeId = routeId;
            _stopIndex = stopIndex;
            _up = up;
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

            return RouteCommandResults.From(RouteRules.CheckMoveStop(state, _routeId, _stopIndex, _up));
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
            route.SwapStops(_stopIndex, _up ? _stopIndex - 1 : _stopIndex + 1);
            state.ApplyRouteStops(route);
        }
    }
}
