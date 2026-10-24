#nullable enable
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
        private readonly EconomySettings _economy;

        /// <summary>
        /// Initializes a sell-vehicle command.
        /// </summary>
        /// <param name="vehicleId">The vehicle to remove.</param>
        /// <param name="economy">Purchase prices used for the half refund; placeholders when omitted.</param>
        public SellRailVehicleCommand(int vehicleId, EconomySettings? economy = null)
        {
            _vehicleId = vehicleId;
            _economy = economy ?? EconomySettings.CreatePlaceholder();
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
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRailVehicle(_vehicleId, out _))
            {
                return CommandValidationResult.Rejected("Rail vehicle does not exist.");
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

            state.CreditCompanyCash(_economy.SellRefundForRail(vehicle.WagonCount));
            if (!state.RemoveRailVehicle(_vehicleId))
            {
                throw new System.InvalidOperationException($"Rail vehicle {_vehicleId} does not exist.");
            }
        }
    }
}
