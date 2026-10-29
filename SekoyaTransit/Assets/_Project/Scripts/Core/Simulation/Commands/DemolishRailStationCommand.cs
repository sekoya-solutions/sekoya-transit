#nullable enable
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Removes a rail station from a single map tile.
    /// </summary>
    public sealed class DemolishRailStationCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly TileCoordinate _coordinate;

        /// <summary>
        /// Initializes a demolish-rail-station command for one tile.
        /// </summary>
        /// <param name="coordinate">The tile whose station should be removed.</param>
        public DemolishRailStationCommand(TileCoordinate coordinate)
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

            if (state.Map.GetRailStationId(_coordinate) == TileMap.NoRailStationId)
            {
                return CommandValidationResult.Rejected("Tile has no rail station to demolish.");
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

            state.RemoveRailStationAt(_coordinate);
        }
    }
}
