#nullable enable
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Sorting orders of the map layers (ADR 0032). Ground and track meshes sit below every sprite; buildings and
    /// vehicles sort by depth so nearer objects (lower on screen) hide farther ones; highlights and labels stay on top.
    /// </summary>
    public static class MapSortingOrder
    {
        public const int Ground = -32000;
        public const int Tracks = -31000;
        public const int Highlights = 32000;
        public const int Labels = 32100;

        // A 512 x 512 map spans world y 0..255.5; 60 orders per world unit keeps every tile distinct within range.
        private const int DepthOrigin = 16000;
        private const float OrdersPerWorldUnit = 60f;

        /// <summary>
        /// Order for a sprite whose pivot (its tile footprint center) sits at the given world y.
        /// </summary>
        public static int ForWorldY(float worldY)
        {
            int order = DepthOrigin - Mathf.RoundToInt(worldY * OrdersPerWorldUnit);
            return Mathf.Clamp(order, Tracks + 1, Highlights - 1);
        }
    }
}
