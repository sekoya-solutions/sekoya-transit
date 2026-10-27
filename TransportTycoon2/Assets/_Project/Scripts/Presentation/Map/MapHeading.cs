#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Direction a vehicle sprite faces on the map. Vehicles only move along the tile axes.
    /// N = +Y (screen upper-left), E = +X (upper-right), S = -Y (lower-right), W = -X (lower-left).
    /// </summary>
    public enum MapHeading
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public static class MapHeadings
    {
        public const int Count = 4;

        /// <summary>
        /// Heading of a movement in tile units, or <paramref name="fallback"/> when there is no movement.
        /// The dominant axis wins; a tie goes to the X axis.
        /// </summary>
        public static MapHeading FromTileDelta(float deltaX, float deltaY, MapHeading fallback)
        {
            const float epsilon = 1e-5f;
            float absX = Mathf.Abs(deltaX);
            float absY = Mathf.Abs(deltaY);
            if (absX < epsilon && absY < epsilon)
            {
                return fallback;
            }

            if (absX >= absY)
            {
                return deltaX > 0f ? MapHeading.East : MapHeading.West;
            }

            return deltaY > 0f ? MapHeading.North : MapHeading.South;
        }

        /// <summary>
        /// Heading of a movement in world units (the 2:1 isometric projection), converted back to tile units.
        /// </summary>
        public static MapHeading FromWorldDelta(float deltaX, float deltaY, MapHeading fallback)
        {
            IsometricProjection.WorldToTileContinuous(deltaX, deltaY, out float tileDeltaX, out float tileDeltaY);
            return FromTileDelta(tileDeltaX, tileDeltaY, fallback);
        }
    }
}
