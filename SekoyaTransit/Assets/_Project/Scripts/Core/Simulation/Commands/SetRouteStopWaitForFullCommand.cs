#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Sets whether vehicles wait at a route stop until they are full (Issue #229).
    /// </summary>
    public sealed class SetRouteStopWaitForFullCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly int _stopIndex;
        private readonly bool _waitForFull;

        /// <summary>
        /// Initializes the command for the stop at <paramref name="stopIndex"/>.
        /// </summary>
        public SetRouteStopWaitForFullCommand(int routeId, int stopIndex, bool waitForFull)
        {
            _routeId = routeId;
            _stopIndex = stopIndex;
            _waitForFull = waitForFull;
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

            return _stopIndex >= 0 && _stopIndex < route.Stops.Count
                ? CommandValidationResult.Valid
                : RouteCommandResults.From(RouteEditRefusal.InvalidStop);
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
            RouteStop stop = route.Stops[_stopIndex];
            if (stop.WaitForFull == _waitForFull)
            {
                return;
            }

            route.SetStop(_stopIndex, new RouteStop(stop.StationId, _waitForFull));
            state.ApplyRouteStops(route);
        }
    }
}
