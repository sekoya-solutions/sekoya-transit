#nullable enable
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Attaches one wagon to an existing rail vehicle when below the locomotive max and cash allows.
    /// </summary>
    public sealed class AddRailWagonCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly EconomySettings _economy;

        /// <summary>
        /// Initializes an add-wagon command.
        /// </summary>
        /// <param name="vehicleId">The train that should receive a wagon.</param>
        /// <param name="economy">Purchase prices; placeholders when omitted.</param>
        public AddRailWagonCommand(int vehicleId, EconomySettings? economy = null)
        {
            _vehicleId = vehicleId;
            _economy = economy ?? EconomySettings.CreatePlaceholder();
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
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                return CommandValidationResult.Rejected("Rail vehicle does not exist.");
            }

            if (vehicle.WagonCount >= RailVehicle.MaxWagons)
            {
                return CommandValidationResult.Rejected("Rail vehicle already has the maximum number of wagons.");
            }

            if (state.CompanyCash < _economy.RailWagonPurchase)
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
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                throw new System.InvalidOperationException($"Rail vehicle {_vehicleId} does not exist.");
            }

            if (!state.TryDebitCompanyCash(_economy.RailWagonPurchase))
            {
                throw new System.InvalidOperationException("Company cash is insufficient to add a wagon.");
            }

            if (!vehicle.TryAddWagon())
            {
                throw new System.InvalidOperationException(
                    $"Rail vehicle {_vehicleId} already has the maximum number of wagons.");
            }
        }
    }
}
