#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Placeholder color nomenclature for the Issue #37 map view (not production art).
    /// Legend: muted greens = terrain; red = town; blue = industry; brown = road (reserved).
    /// </summary>
    public static class MapPlaceholderPalette
    {
        /// <summary>Default flat terrain fill.</summary>
        public static readonly Color32 TerrainDefault = new Color32(90, 140, 70, 255);

        /// <summary>Alternate terrain tone for type IDs other than zero (still placeholder).</summary>
        public static readonly Color32 TerrainAlternate = new Color32(110, 150, 80, 255);

        /// <summary>Visible isometric grid edge.</summary>
        public static readonly Color32 GridLine = new Color32(40, 60, 35, 255);

        /// <summary>Town object marker.</summary>
        public static readonly Color32 Town = new Color32(210, 55, 55, 255);

        /// <summary>Industry object marker.</summary>
        public static readonly Color32 Industry = new Color32(55, 110, 210, 255);

        /// <summary>Reserved for roads when road tile data exists.</summary>
        public static readonly Color32 RoadReserved = new Color32(140, 95, 55, 255);

        /// <summary>
        /// Resolves a ground fill color from a terrain type ID.
        /// </summary>
        public static Color32 TerrainColor(int terrainTypeId)
        {
            if (terrainTypeId <= 0)
            {
                return TerrainDefault;
            }

            return TerrainAlternate;
        }
    }
}
