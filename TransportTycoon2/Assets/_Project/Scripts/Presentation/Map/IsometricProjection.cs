#nullable enable
using System;
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
        /// Inverse of <see cref="TileToWorld"/> for continuous (non-integer) tile space.
        /// </summary>
        public static void WorldToTileContinuous(float worldX, float worldY, out float tileX, out float tileY)
        {
            tileX = worldX + (2f * worldY);
            tileY = (2f * worldY) - worldX;
        }

        /// <summary>
        /// Maps a world point to the nearest in-bounds map tile, if any.
        /// </summary>
        /// <returns>True when the nearest tile lies inside the square map.</returns>
        public static bool TryWorldToTile(float worldX, float worldY, int sideLengthTiles, out int tileX, out int tileY)
        {
            if (sideLengthTiles < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sideLengthTiles),
                    sideLengthTiles,
                    "Map side length must be at least 1.");
            }

            WorldToTileContinuous(worldX, worldY, out float continuousX, out float continuousY);
            tileX = Mathf.RoundToInt(continuousX);
            tileY = Mathf.RoundToInt(continuousY);
            if (tileX < 0 || tileY < 0 || tileX >= sideLengthTiles || tileY >= sideLengthTiles)
            {
                tileX = 0;
                tileY = 0;
                return false;
            }

            return true;
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
