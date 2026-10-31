#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Removes an existing road vehicle immediately and credits half its current purchase price.
    /// Any cargo still on the vehicle is discarded (not returned to a station).
    /// </summary>
    public sealed class SellRoadVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes a sell-vehicle command.
        /// </summary>
        public SellRoadVehicleCommand(int vehicleId, ContentCatalog? catalog = null)
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

            if (!state.TryGetOwnedRoadVehicle(_vehicleId, out RoadVehicle? vehicle) || vehicle == null)
            {
                return CommandValidationResult.Rejected("Road vehicle does not exist.");
            }

            if (!_catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? definition) ||
                definition == null ||
                definition.Mode != VehicleMode.Road)
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

            if (!state.TryGetOwnedRoadVehicle(_vehicleId, out RoadVehicle? vehicle) || vehicle == null)
            {
                throw new InvalidOperationException($"Road vehicle {_vehicleId} does not exist.");
            }

            if (!_catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? definition) ||
                definition == null ||
                definition.Mode != VehicleMode.Road)
            {
                throw new InvalidOperationException($"Road vehicle {_vehicleId} has an unknown type.");
            }

            state.CreditCompanyCash(EconomySettings.HalfRefund(definition.Purchase), FinanceCategory.VehicleSales);
            if (!state.RemoveRoadVehicle(_vehicleId))
            {
                throw new InvalidOperationException($"Road vehicle {_vehicleId} does not exist.");
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
