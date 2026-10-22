#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Replaces a road vehicle's looping station-order list.
    /// </summary>
    public sealed class SetRoadVehicleOrdersCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly int[] _stationIds;

        /// <summary>
        /// Initializes a set-orders command.
        /// </summary>
        /// <param name="vehicleId">The vehicle to update.</param>
        /// <param name="stationIds">Ordered station ids (may be empty or a single stop for idle).</param>
        public SetRoadVehicleOrdersCommand(int vehicleId, IReadOnlyList<int> stationIds)
        {
#pragma warning disable CA1510
            if (stationIds == null)
            {
                throw new ArgumentNullException(nameof(stationIds));
            }
#pragma warning restore CA1510

            _vehicleId = vehicleId;
            _stationIds = new int[stationIds.Count];
            for (int index = 0; index < stationIds.Count; index++)
            {
                _stationIds[index] = stationIds[index];
            }
        }

        /// <summary>
        /// Gets the target vehicle id.
        /// </summary>
        public int VehicleId
        {
            get
            {
                return _vehicleId;
            }
        }

        /// <summary>
        /// Gets a copy of the ordered station ids.
        /// </summary>
        public IReadOnlyList<int> StationIds
        {
            get
            {
                return _stationIds;
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

            if (!state.TryGetRoadVehicle(_vehicleId, out _))
            {
                return CommandValidationResult.Rejected("Road vehicle does not exist.");
            }

            for (int index = 0; index < _stationIds.Length; index++)
            {
                int stationId = _stationIds[index];
                if (stationId < 0)
                {
                    return CommandValidationResult.Rejected("Order station ids cannot be negative.");
                }

                if (!state.TryGetRoadStation(stationId, out _))
                {
                    return CommandValidationResult.Rejected($"Order references missing road station {stationId}.");
                }
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

            if (!state.TryGetRoadVehicle(_vehicleId, out RoadVehicle? vehicle) || vehicle == null)
            {
                throw new InvalidOperationException($"Road vehicle {_vehicleId} does not exist.");
            }

            vehicle.SetOrders(_stationIds);
        }
    }
}
