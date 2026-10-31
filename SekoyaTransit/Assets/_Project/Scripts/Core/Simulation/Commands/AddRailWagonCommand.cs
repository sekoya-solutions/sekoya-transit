#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Attaches one wagon to an existing rail vehicle when below the locomotive max and cash allows.
    /// </summary>
    public sealed class AddRailWagonCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes an add-wagon command.
        /// </summary>
        public AddRailWagonCommand(int vehicleId, ContentCatalog? catalog = null)
        {
            _vehicleId = vehicleId;
            _catalog = catalog ?? CreateMinimalCatalog();
        }

        /// <summary>
        /// Gets the vehicle id.
        /// </summary>
        public int VehicleId
        {
            get
            {
                return _vehicleId;
            }
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

            if (!state.TryGetOwnedRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                return CommandValidationResult.Rejected("Rail vehicle does not exist.");
            }

            if (vehicle.WagonCount >= vehicle.MaxWagons)
            {
                return CommandValidationResult.Rejected("Rail vehicle already has the maximum number of wagons.");
            }

            if (state.CompanyCash < _catalog.RailWagon.Purchase)
            {
                return CommandValidationResult.Rejected("Insufficient company cash.");
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

            if (!state.TryGetOwnedRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                throw new InvalidOperationException($"Rail vehicle {_vehicleId} does not exist.");
            }

            if (!state.TryDebitCompanyCash(_catalog.RailWagon.Purchase, FinanceCategory.VehiclePurchases))
            {
                throw new InvalidOperationException("Company cash is insufficient to add a wagon.");
            }

            if (!vehicle.TryAddWagon())
            {
                throw new InvalidOperationException(
                    $"Rail vehicle {_vehicleId} already has the maximum number of wagons.");
            }
        }

        private static ContentCatalog CreateMinimalCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }
    }
}
