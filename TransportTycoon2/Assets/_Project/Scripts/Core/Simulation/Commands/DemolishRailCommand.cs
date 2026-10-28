#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Removes rail track from a single map tile.
    /// </summary>
    public sealed class DemolishRailCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly TileCoordinate _coordinate;

        /// <summary>
        /// Initializes a demolish-rail command for one tile.
        /// </summary>
        /// <param name="coordinate">The tile whose rail track should be removed.</param>
        public DemolishRailCommand(TileCoordinate coordinate)
        {
            _coordinate = coordinate;
        }

        /// <summary>
        /// Gets the target tile.
        /// </summary>
        public TileCoordinate Coordinate
        {
            get
            {
                return _coordinate;
            }
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.Map.Contains(_coordinate))
            {
                return CommandValidationResult.Rejected("Tile is outside the map.");
            }

            if (state.Map.GetRailTypeId(_coordinate) == TileMap.NoRailTypeId)
            {
                return CommandValidationResult.Rejected("Tile has no rail track to demolish.");
            }

            return CommandValidationResult.Valid;
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            state.Map.SetRailTypeId(_coordinate, TileMap.NoRailTypeId);
            RailVehicleMovement.RebuildGraphKeepingVehicles(state);
        }
    }
}
