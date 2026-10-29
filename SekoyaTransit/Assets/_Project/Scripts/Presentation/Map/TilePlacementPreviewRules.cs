#nullable enable
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Presentation helpers that forward to Core placement rules for MapPlay ghosts.
    /// </summary>
    public static class TilePlacementPreviewRules
    {
        /// <summary>
        /// Returns whether the road-build ghost should tint valid for the tile.
        /// </summary>
        public static bool IsBuildGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadPlacement.CanBuild(map, coordinate);
        }

        /// <summary>
        /// Returns whether the delete ghost should tint valid for the tile (something to remove).
        /// </summary>
        public static bool IsDemolishGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return TileDemolishTargets.Resolve(map, coordinate).Any;
        }

        /// <summary>
        /// Returns whether the rail-build ghost should tint valid for the tile.
        /// </summary>
        public static bool IsRailBuildGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RailPlacement.CanBuild(map, coordinate);
        }

        /// <summary>
        /// Returns whether the station-build ghost should tint valid for the tile.
        /// </summary>
        public static bool IsStationBuildGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadStationPlacement.CanBuild(map, coordinate);
        }

        /// <summary>
        /// Returns whether the rail-station-build ghost should tint valid for the tile.
        /// </summary>
        public static bool IsRailStationBuildGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RailStationPlacement.CanBuild(map, coordinate);
        }
    }
}
