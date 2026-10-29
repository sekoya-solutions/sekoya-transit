#nullable enable
using System;
using System.Globalization;
using UnityEngine;

namespace SekoyaTransit.Presentation.Diagnostics
{
    /// <summary>
    /// Limits the frame rate to about the ADR 0017 budget of 60 fps (Issue #70). Without a limit, WebGL draws at the
    /// browser's display rate (165 Hz measured) and spends CPU redrawing a map that did not change.
    /// </summary>
    public static class FramePacing
    {
        /// <summary>Frames per second the game aims for, at least.</summary>
        public const int TargetFrameRate = 60;

        /// <summary>
        /// Applies the desktop limit: vertical sync off, because it overrides
        /// <see cref="Application.targetFrameRate"/>, and a cap at <see cref="TargetFrameRate"/>.
        /// </summary>
        public static void ApplyDesktop()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }

        /// <summary>
        /// Applies the WebGL limit. A WebGL target frame rate is scheduled with timers, which cost about twice the
        /// CPU per frame of the browser's animation frames (measured in Chrome), so the game draws on every
        /// <paramref name="interval"/>-th animation frame instead.
        /// </summary>
        public static void ApplyWebGl(int interval)
        {
            QualitySettings.vSyncCount = Math.Min(4, Math.Max(1, interval));
            Application.targetFrameRate = -1;
        }

        /// <summary>
        /// Returns how many display refreshes each frame waits for so the frame rate stays at or above
        /// <see cref="TargetFrameRate"/>: 1 below 120 Hz, 2 from 120 Hz (60 to 89 fps on 120 to 179 Hz displays),
        /// and so on, capped at Unity's maximum of 4. An unknown rate (0, negative or not a number) gives 1.
        /// </summary>
        public static int VSyncIntervalFor(double refreshRate)
        {
            if (double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate <= 0d)
            {
                return 1;
            }

            // Half a hertz of slack so a 119.9 Hz display still counts as 120 Hz.
            int interval = (int)Math.Floor((refreshRate + 0.5d) / TargetFrameRate);
            return Math.Min(4, Math.Max(1, interval));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Draw on every animation frame while the display rate is measured, then settle on the interval.
            ApplyWebGl(1);
            var host = new GameObject(nameof(WebGlFramePacer));
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<WebGlFramePacer>();
#else
            ApplyDesktop();
#endif
        }

        /// <summary>
        /// Measures the display rate over the first second or so, applies the WebGL interval, then removes itself.
        /// </summary>
        private sealed class WebGlFramePacer : MonoBehaviour
        {
            private readonly DisplayRateEstimator _estimator = new DisplayRateEstimator();

            private void Update()
            {
                if (!_estimator.AddFrame(Time.unscaledDeltaTime, out double refreshRate))
                {
                    return;
                }

                int interval = VSyncIntervalFor(refreshRate);
                ApplyWebGl(interval);
                Debug.Log(
                    "FramePacing: display about " + refreshRate.ToString("F0", CultureInfo.InvariantCulture) +
                    " Hz, drawing every " + interval + " animation frame(s).");
                Destroy(gameObject);
            }
        }
    }
}
