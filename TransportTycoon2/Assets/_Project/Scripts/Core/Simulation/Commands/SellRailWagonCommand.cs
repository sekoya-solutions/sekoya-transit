#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Detaches one wagon from an existing rail vehicle and credits half the current wagon price.
    /// Excess cargo above the new capacity is discarded.
    /// </summary>
    public sealed class SellRailWagonCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes a sell-wagon command.
        /// </summary>
        public SellRailWagonCommand(int vehicleId, ContentCatalog? catalog = null)
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

            if (!state.TryGetRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                return CommandValidationResult.Rejected("Rail vehicle does not exist.");
            }

            if (vehicle.WagonCount <= 0)
            {
                return CommandValidationResult.Rejected("Rail vehicle has no wagons to sell.");
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

            if (!state.TryGetRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                throw new InvalidOperationException($"Rail vehicle {_vehicleId} does not exist.");
            }

            state.CreditCompanyCash(
                EconomySettings.HalfRefund(_catalog.RailWagon.Purchase),
                FinanceCategory.VehicleSales);
            if (!vehicle.TrySellWagon())
            {
                throw new InvalidOperationException($"Rail vehicle {_vehicleId} has no wagons to sell.");
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
