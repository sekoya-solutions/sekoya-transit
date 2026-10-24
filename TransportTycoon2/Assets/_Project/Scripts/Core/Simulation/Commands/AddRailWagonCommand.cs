#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Attaches one free wagon to an existing rail vehicle when below the locomotive max.
    /// </summary>
    public sealed class AddRailWagonCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;

        /// <summary>
        /// Initializes an add-wagon command.
        /// </summary>
        /// <param name="vehicleId">The train that should receive a wagon.</param>
        public AddRailWagonCommand(int vehicleId)
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

            if (vehicle.WagonCount >= RailVehicle.MaxWagons)
            {
                return CommandValidationResult.Rejected("Rail vehicle already has the maximum number of wagons.");
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

            if (!vehicle.TryAddWagon())
            {
                throw new System.InvalidOperationException(
                    $"Rail vehicle {_vehicleId} already has the maximum number of wagons.");
            }
        }
    }
}
