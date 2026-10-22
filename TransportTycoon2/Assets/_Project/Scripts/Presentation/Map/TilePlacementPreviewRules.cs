#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Placeholder placement validity for Issue #40 ghosts. Real road/station rules land with later Core issues.
    /// </summary>
    public static class TilePlacementPreviewRules
    {
        /// <summary>
        /// Returns true when a 1x1 ghost may show as valid: tile has no town/industry (or other) object.
        /// </summary>
        /// <param name="objectId">Core object ID at the tile, or <see cref="TileMap.NoObjectId"/> when empty.</param>
        public static bool IsPlaceholderPlacementValid(int objectId)
        {
            return objectId == TileMap.NoObjectId;
        }
    }
}
