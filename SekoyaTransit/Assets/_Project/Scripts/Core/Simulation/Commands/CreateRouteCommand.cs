#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Creates an empty route with a player-chosen name (Issue #229). The new route gets
    /// <see cref="GeneratedWorld.NextRouteId"/>.
    /// </summary>
    public sealed class CreateRouteCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly string _name;

        /// <summary>
        /// Initializes a create command; <paramref name="name"/> is trimmed.
        /// </summary>
        public CreateRouteCommand(string name)
        {
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

            RouteEditRefusal refusal = RouteRules.CheckName(state, _name, RoadVehicle.NoRouteId, out _);
            return RouteCommandResults.From(refusal);
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

            RouteRules.CheckName(state, _name, RoadVehicle.NoRouteId, out string name);
            state.AddRoute(name);
        }
    }
}
