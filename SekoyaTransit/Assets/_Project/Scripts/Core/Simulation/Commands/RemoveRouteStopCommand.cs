#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Removes one stop from a route (Issue #229). Neighbouring stops left at the same station merge into one; a route
    /// left with fewer than two stops unassigns its vehicles (they leave the map and lose their cargo).
    /// </summary>
    public sealed class RemoveRouteStopCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly int _stopIndex;

        /// <summary>
        /// Initializes a remove-stop command for the stop at <paramref name="stopIndex"/>.
        /// </summary>
        public RemoveRouteStopCommand(int routeId, int stopIndex)
        {
            _routeId = routeId;
            _stopIndex = stopIndex;
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
            route.RemoveStopAt(_stopIndex);
            state.ApplyRouteStops(route);
        }
    }
}
