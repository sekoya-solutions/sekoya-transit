#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Placeholder color nomenclature for the Issue #37 map view and Issue #40 overlays (not production art).
    /// Legend: muted greens = terrain; red = town; blue = industry; brown = road;
    /// purple = road station; amber = empty vehicle; orange-red = loaded vehicle;
    /// yellow hover; cyan selection; green/red placement ghost.
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

        /// <summary>Road tile fill (placeholder brown).</summary>
        public static readonly Color32 RoadReserved = new Color32(140, 95, 55, 255);

        /// <summary>Road station tile fill (placeholder purple).</summary>
        public static readonly Color32 RoadStation = new Color32(170, 70, 200, 255);

        /// <summary>Road vehicle marker (placeholder amber when empty).</summary>
        public static readonly Color32 RoadVehicle = new Color32(240, 190, 40, 255);

        /// <summary>Road vehicle marker when carrying cargo (placeholder orange-red).</summary>
        public static readonly Color32 RoadVehicleLoaded = new Color32(230, 90, 40, 255);

        /// <summary>Road vehicle marker when selected in the Vehicles route editor (bright cyan).</summary>
        public static readonly Color32 RoadVehicleSelected = new Color32(80, 255, 255, 255);

        /// <summary>Hover tile outline (Issue #40).</summary>
        public static readonly Color32 TileHover = new Color32(255, 230, 90, 140);

        /// <summary>Sticky selection highlight (Issue #40).</summary>
        public static readonly Color32 TileSelection = new Color32(80, 200, 255, 170);

        /// <summary>Placement ghost when placeholder rules allow build (Issue #40).</summary>
        public static readonly Color32 GhostValid = new Color32(70, 210, 90, 150);

        /// <summary>Placement ghost when placeholder rules block build (Issue #40).</summary>
        public static readonly Color32 GhostInvalid = new Color32(220, 60, 60, 150);

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
