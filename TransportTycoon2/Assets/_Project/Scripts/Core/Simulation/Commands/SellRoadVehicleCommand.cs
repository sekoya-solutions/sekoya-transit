#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Removes an existing road vehicle at no cost, immediately, regardless of location or
    /// load/unload state. Any cargo still on the vehicle is discarded (not returned to a station).
    /// </summary>
    public sealed class SellRoadVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;

        /// <summary>
        /// Initializes a sell-vehicle command.
        /// </summary>
        /// <param name="vehicleId">The vehicle to remove.</param>
        public SellRoadVehicleCommand(int vehicleId)
        {
            _vehicleId = vehicleId;
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

            if (!state.TryGetRoadVehicle(_vehicleId, out _))
            {
                return CommandValidationResult.Rejected("Road vehicle does not exist.");
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

            if (!state.RemoveRoadVehicle(_vehicleId))
            {
                throw new System.InvalidOperationException($"Road vehicle {_vehicleId} does not exist.");
            }
        }
    }
}
