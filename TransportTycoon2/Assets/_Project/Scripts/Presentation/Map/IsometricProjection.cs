#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Presentation-only isometric projection: one tile is a 1 × 0.5 world-unit diamond.
    /// </summary>
    public static class IsometricProjection
    {
        /// <summary>
        /// Converts a tile coordinate to the diamond center in world space.
        /// </summary>
        public static Vector3 TileToWorld(float tileX, float tileY)
        {
            return new Vector3((tileX - tileY) * 0.5f, (tileX + tileY) * 0.25f, 0f);
        }

        /// <summary>
        /// Returns the world-space center of a square map of the given side length in tiles.
        /// </summary>
        public static Vector3 MapCenter(int sideLengthTiles)
        {
            float mid = (sideLengthTiles - 1) * 0.5f;
            return TileToWorld(mid, mid);
        }
    }
}
