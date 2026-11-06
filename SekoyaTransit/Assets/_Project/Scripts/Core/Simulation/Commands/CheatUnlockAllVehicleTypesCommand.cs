#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Playtest cheat (Issue #316): unlocks every non-starting road truck and rail locomotive type at no cost and lifts the
    /// vehicle year gate, so every vehicle type can be bought at once whatever the calendar year.
    /// </summary>
    public sealed class CheatUnlockAllVehicleTypesCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes the cheat for the vehicle types of <paramref name="catalog"/>.
        /// </summary>
        public CheatUnlockAllVehicleTypesCommand(ContentCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
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

            if (state.VehicleYearGateLifted && !HasLockedType(state))
            {
                return CommandValidationResult.Rejected("Every vehicle type is already unlocked and available.");
            }

            return CommandValidationResult.Valid;
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

            IReadOnlyList<VehicleDefinition> vehicles = _catalog.Vehicles;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleDefinition definition = vehicles[index];
                if (IsLockedType(definition, state))
                {
                    state.UnlockVehicleType(definition.Id);
                }
            }

            state.LiftVehicleYearGate();
        }

        private bool HasLockedType(GeneratedWorld state)
        {
            IReadOnlyList<VehicleDefinition> vehicles = _catalog.Vehicles;
            for (int index = 0; index < vehicles.Count; index++)
            {
                if (IsLockedType(vehicles[index], state))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLockedType(VehicleDefinition definition, GeneratedWorld state)
        {
            return (definition.Mode == VehicleMode.Road || definition.Mode == VehicleMode.Rail)
                && !definition.Starting
                && !state.IsVehicleTypeUnlocked(definition.Id);
        }
    }
}
