#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Places a 1x1 road station on a single empty map tile adjacent to a town or industry.
    /// </summary>
    public sealed class BuildRoadStationCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly TileCoordinate _coordinate;

        /// <summary>
        /// Initializes a build-road-station command for one tile.
        /// </summary>
        /// <param name="coordinate">The tile that should receive a station.</param>
        public BuildRoadStationCommand(TileCoordinate coordinate)
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

            if (state.Map.GetRoadTypeId(_coordinate) != TileMap.NoRoadTypeId)
            {
                return CommandValidationResult.Rejected("Tile already has a road.");
            }

            if (state.Map.GetStationId(_coordinate) != TileMap.NoStationId)
            {
                return CommandValidationResult.Rejected("Tile already has a road station.");
            }

            if (!RoadStationPlacement.HasOrthogonalTownOrIndustryNeighbor(state.Map, _coordinate))
            {
                return CommandValidationResult.Rejected(
                    "Road station must be orthogonally adjacent to a town or industry.");
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

            state.AddRoadStation(_coordinate);
        }
    }
}
