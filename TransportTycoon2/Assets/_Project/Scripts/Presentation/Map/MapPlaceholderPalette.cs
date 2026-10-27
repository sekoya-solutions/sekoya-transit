#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Colors that are not map art: the vehicle markers drawn when no map art is supplied (tests and art-less scenes;
    /// amber/orange-red road, mint/deep-teal rail, cyan selected) and the Issue #40 overlays (yellow hover, cyan
    /// selection, green/red placement ghost). Map art itself comes from <see cref="MapArtSet"/> (ADR 0032).
    /// </summary>
    public static class MapPlaceholderPalette
    {
        /// <summary>Road vehicle marker (placeholder amber when empty).</summary>
        public static readonly Color32 RoadVehicle = new Color32(240, 190, 40, 255);

        /// <summary>Road vehicle marker when carrying cargo (placeholder orange-red).</summary>
        public static readonly Color32 RoadVehicleLoaded = new Color32(230, 90, 40, 255);

        /// <summary>Road vehicle marker when selected in the Vehicles route editor (bright cyan).</summary>
        public static readonly Color32 RoadVehicleSelected = new Color32(80, 255, 255, 255);

        /// <summary>Rail vehicle marker (placeholder mint when empty).</summary>
        public static readonly Color32 RailVehicle = new Color32(90, 220, 200, 255);

        /// <summary>Rail vehicle marker when carrying cargo (placeholder deep teal).</summary>
        public static readonly Color32 RailVehicleLoaded = new Color32(20, 120, 130, 255);

        /// <summary>Rail vehicle marker when selected in the Vehicles route editor (bright cyan).</summary>
        public static readonly Color32 RailVehicleSelected = new Color32(80, 255, 255, 255);

        /// <summary>Rail wagon marker trailing the locomotive (slightly cooler mint).</summary>
        public static readonly Color32 RailWagon = new Color32(40, 170, 190, 255);

        /// <summary>Hover tile outline (Issue #40).</summary>
        public static readonly Color32 TileHover = new Color32(255, 230, 90, 140);

        /// <summary>Sticky selection highlight (Issue #40).</summary>
        public static readonly Color32 TileSelection = new Color32(80, 200, 255, 170);

        /// <summary>Placement ghost when placeholder rules allow build (Issue #40).</summary>
        public static readonly Color32 GhostValid = new Color32(70, 210, 90, 150);

        /// <summary>Placement ghost when placeholder rules block build (Issue #40).</summary>
        public static readonly Color32 GhostInvalid = new Color32(220, 60, 60, 150);
    }
}
