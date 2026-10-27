#nullable enable
using System.Globalization;

namespace TransportTycoon.Presentation.Diagnostics
{
    /// <summary>
    /// Averaged frame cost over one <see cref="FrameStatsWindow"/> span (Issue #70).
    /// </summary>
    public readonly struct FrameStatsSummary
    {
        /// <summary>
        /// Initializes a summary.
        /// </summary>
        public FrameStatsSummary(
            int frames,
            double durationSeconds,
            double maxFrameMs,
            double? averageMainThreadMs,
            double? averageScriptsMs,
            double? averageGcAllocatedBytes)
        {
            Frames = frames;
            DurationSeconds = durationSeconds;
            MaxFrameMs = maxFrameMs;
            AverageMainThreadMs = averageMainThreadMs;
            AverageScriptsMs = averageScriptsMs;
            AverageGcAllocatedBytes = averageGcAllocatedBytes;
        }

        /// <summary>Frames in the span.</summary>
        public int Frames { get; }

        /// <summary>Real time covered by the span, in seconds.</summary>
        public double DurationSeconds { get; }

        /// <summary>Frames per second over the span.</summary>
        public double FramesPerSecond => DurationSeconds > 0d ? Frames / DurationSeconds : 0d;

        /// <summary>Average time between frames, in milliseconds.</summary>
        public double AverageFrameMs => Frames > 0 ? DurationSeconds * 1000d / Frames : 0d;

        /// <summary>Longest time between two frames, in milliseconds.</summary>
        public double MaxFrameMs { get; }

        /// <summary>Average main-thread work per frame in milliseconds, or null when the player does not report it.</summary>
        public double? AverageMainThreadMs { get; }

        /// <summary>Average MonoBehaviour Update work per frame in milliseconds, or null when not reported.</summary>
        public double? AverageScriptsMs { get; }

        /// <summary>Average managed memory allocated per frame in bytes, or null when not reported.</summary>
        public double? AverageGcAllocatedBytes { get; }

        /// <summary>
        /// Formats the summary as one developer log line (not player-facing text, so not localized).
        /// </summary>
        public string ToLogLine()
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            return string.Format(
                culture,
                "[FrameStats] {0:F1} s: {1} frames, {2:F1} fps, frame avg {3:F2} ms max {4:F2} ms, " +
                "main thread avg {5} ms, scripts avg {6} ms, GC alloc avg {7} B/frame",
                DurationSeconds,
                Frames,
                FramesPerSecond,
                AverageFrameMs,
                MaxFrameMs,
                Format(AverageMainThreadMs, "F2", culture),
                Format(AverageScriptsMs, "F2", culture),
                Format(AverageGcAllocatedBytes, "F0", culture));
        }

        private static string Format(double? value, string format, CultureInfo culture)
        {
            return value.HasValue ? value.Value.ToString(format, culture) : "n/a";
        }
    }
}
