#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Renames a route (Issue #229).
    /// </summary>
    public sealed class RenameRouteCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly string _name;

        /// <summary>
        /// Initializes a rename command; <paramref name="name"/> is trimmed.
        /// </summary>
        public RenameRouteCommand(int routeId, string name)
        {
            _routeId = routeId;
            _name = name ?? throw new ArgumentNullException(nameof(name));
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

            if (!state.TryGetRoute(_routeId, out _))
            {
                return RouteCommandResults.From(RouteEditRefusal.MissingRoute);
            }

            return RouteCommandResults.From(RouteRules.CheckName(state, _name, _routeId, out _));
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

            RouteRules.CheckName(state, _name, _routeId, out string name);
            RouteCommandResults.Require(state, _routeId).Rename(name);
        }
    }
}
