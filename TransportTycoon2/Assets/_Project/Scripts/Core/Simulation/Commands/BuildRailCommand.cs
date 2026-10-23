#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Places basic rail track on a single empty map tile.
    /// </summary>
    public sealed class BuildRailCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly TileCoordinate _coordinate;

        /// <summary>
        /// Initializes a build-rail command for one tile.
        /// </summary>
        /// <param name="coordinate">The tile that should receive rail track.</param>
        public BuildRailCommand(TileCoordinate coordinate)
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

            if (state.Map.GetObjectId(_coordinate) != TileMap.NoObjectId)
            {
                return CommandValidationResult.Rejected("Tile is occupied by a town or industry.");
            }

            if (state.Map.GetStationId(_coordinate) != TileMap.NoStationId)
            {
                return CommandValidationResult.Rejected("Tile is occupied by a road station.");
            }

            if (state.Map.GetRailTypeId(_coordinate) != TileMap.NoRailTypeId)
            {
                return CommandValidationResult.Rejected("Tile already has rail track.");
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

            state.Map.SetRailTypeId(_coordinate, TileMap.BasicRailTypeId);
            state.RailGraph.RebuildFromMap(state.Map);
        }
    }
}
