#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Deletes a route (Issue #229); its vehicles leave the map, lose their cargo and become unassigned.
    /// </summary>
    public sealed class DeleteRouteCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;

        /// <summary>
        /// Initializes a delete command.
        /// </summary>
        public DeleteRouteCommand(int routeId)
        {
            _routeId = routeId;
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

            return state.TryGetRoute(_routeId, out _)
                ? CommandValidationResult.Valid
                : RouteCommandResults.From(RouteEditRefusal.MissingRoute);
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

            RouteCommandResults.Require(state, _routeId);
            state.UnassignRouteVehicles(_routeId);
            state.RemoveRoute(_routeId);
        }
    }
}
