#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Spawns a free placeholder road vehicle at an existing road station.
    /// </summary>
    public sealed class BuyRoadVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _stationId;

        /// <summary>
        /// Initializes a buy-vehicle command.
        /// </summary>
        /// <param name="stationId">The station where the vehicle should spawn.</param>
        public BuyRoadVehicleCommand(int stationId)
        {
            _stationId = stationId;
        }

        /// <summary>
        /// Gets the spawn station id.
        /// </summary>
        public int StationId
        {
            get
            {
                return _stationId;
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

            if (!state.TryGetRoadStation(_stationId, out RoadStation? station) || station == null)
            {
                return CommandValidationResult.Rejected("Road station does not exist.");
            }

            if (!state.RoadGraph.TryGetNodeId(station.Coordinate, out _))
            {
                return CommandValidationResult.Rejected("Road station is not present in the road graph.");
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

            state.AddRoadVehicle(_stationId);
        }
    }
}
