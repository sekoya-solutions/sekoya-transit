#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Diagnostics
{
    /// <summary>
    /// Caps the frame rate at the ADR 0017 budget of 60 fps on every player (Issue #70). Without the cap, WebGL draws
    /// at the browser's display rate (165 Hz measured) and spends CPU redrawing a map that did not change.
    /// </summary>
    public static class FramePacing
    {
        /// <summary>Frames per second the game draws at most.</summary>
        public const int TargetFrameRate = 60;

        /// <summary>
        /// Applies the cap. Vertical sync is turned off because on desktop players it overrides
        /// <see cref="Application.targetFrameRate"/>; WebGL ignores vertical sync and honors the target directly.
        /// </summary>
        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            Apply();
        }
    }
}
