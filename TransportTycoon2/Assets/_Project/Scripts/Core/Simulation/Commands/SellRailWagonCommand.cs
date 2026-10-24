#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Detaches one wagon from an existing rail vehicle. Excess cargo above the new capacity is discarded.
    /// </summary>
    public sealed class SellRailWagonCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;

        /// <summary>
        /// Initializes a sell-wagon command.
        /// </summary>
        /// <param name="vehicleId">The train that should lose a wagon.</param>
        public SellRailWagonCommand(int vehicleId)
        {
            _vehicleId = vehicleId;
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
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRailVehicle(_vehicleId, out RailVehicle? vehicle) || vehicle == null)
            {
                throw new System.InvalidOperationException($"Rail vehicle {_vehicleId} does not exist.");
            }

            if (!vehicle.TrySellWagon())
            {
                throw new System.InvalidOperationException($"Rail vehicle {_vehicleId} has no wagons to sell.");
            }
        }
    }
}
