#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Removes an existing rail vehicle immediately and credits half the current locomotive
    /// plus wagon purchase prices. Any cargo still on the vehicle is discarded.
    /// </summary>
    public sealed class SellRailVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes a sell-vehicle command.
        /// </summary>
        public SellRailVehicleCommand(int vehicleId, ContentCatalog? catalog = null)
        {
            _vehicleId = vehicleId;
            _catalog = catalog ?? CreateMinimalCatalog();
        }

        /// <summary>
        /// Gets the vehicle id to sell.
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

            if (!_catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? definition) ||
                definition == null ||
                definition.Mode != VehicleMode.Rail)
            {
                return CommandValidationResult.Rejected("Vehicle type is unknown.");
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
                _catalog.SellRefundForRail(vehicle.TypeId, vehicle.WagonCount),
                FinanceCategory.VehicleSales);
            if (!state.RemoveRailVehicle(_vehicleId))
            {
                throw new InvalidOperationException($"Rail vehicle {_vehicleId} does not exist.");
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
