#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Presentation helpers that forward to Core road placement rules for MapPlay ghosts.
    /// </summary>
    public static class TilePlacementPreviewRules
    {
        /// <summary>
        /// Returns whether the build ghost should tint valid for the tile.
        /// </summary>
        public static bool IsBuildGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadPlacement.CanBuild(map, coordinate);
        }

        /// <summary>
        /// Returns whether the demolish ghost should tint valid for the tile.
        /// </summary>
        public static bool IsDemolishGhostValid(TileMap map, TileCoordinate coordinate)
        {
            return RoadPlacement.CanDemolish(map, coordinate);
        }
    }
}
