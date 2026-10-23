#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Spawns a free placeholder rail vehicle at an existing rail station.
    /// </summary>
    public sealed class BuyRailVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _stationId;

        /// <summary>
        /// Initializes a buy-vehicle command.
        /// </summary>
        /// <param name="stationId">The station where the vehicle should spawn.</param>
        public BuyRailVehicleCommand(int stationId)
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

            if (!state.TryGetRailStation(_stationId, out RailStation? station) || station == null)
            {
                return CommandValidationResult.Rejected("Rail station does not exist.");
            }

            if (!state.RailGraph.TryGetNodeId(station.Coordinate, out _))
            {
                return CommandValidationResult.Rejected("Rail station is not present in the rail graph.");
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

            state.AddRailVehicle(_stationId);
        }
    }
}
