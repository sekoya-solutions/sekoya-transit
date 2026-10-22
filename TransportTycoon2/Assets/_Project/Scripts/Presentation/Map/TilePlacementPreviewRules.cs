#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Presentation.Map
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
        /// Returns whether the road-demolish ghost should tint valid for the tile.
        /// </summary>
        public static bool IsDemolishGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadPlacement.CanDemolish(map, coordinate);
        }

        /// <summary>
        /// Returns whether the station-build ghost should tint valid for the tile.
        /// </summary>
        public static bool IsStationBuildGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadStationPlacement.CanBuild(map, coordinate);
        }

        /// <summary>
        /// Returns whether the station-demolish ghost should tint valid for the tile.
        /// </summary>
        public static bool IsStationDemolishGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadStationPlacement.CanDemolish(map, coordinate);
        }
    }
}
