#nullable enable
using System;

namespace SekoyaTransit.Presentation.Diagnostics
{
    /// <summary>
    /// Estimates the display refresh rate from the time between frames while the game draws on every browser
    /// animation frame (Issue #70). Unity reports 60 Hz on WebGL whatever the display, so the rate is measured.
    /// The first frames are skipped (loading hitches), and the median of the samples ignores the odd slow frame.
    /// </summary>
    public sealed class DisplayRateEstimator
    {
        private readonly int _warmupFrames;
        private readonly double[] _samples;
        private int _skipped;
        private int _count;

        /// <summary>
        /// Initializes an estimator that skips <paramref name="warmupFrames"/> frames, then measures
        /// <paramref name="sampleFrames"/> frames.
        /// </summary>
        public DisplayRateEstimator(int warmupFrames = 30, int sampleFrames = 60)
        {
            if (warmupFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupFrames), warmupFrames, "Cannot be negative.");
            }

            if (sampleFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleFrames), sampleFrames, "Must be at least 1.");
            }

            _warmupFrames = warmupFrames;
            _samples = new double[sampleFrames];
        }

        /// <summary>
        /// Adds the time since the previous frame. Returns true once enough frames were measured, with the estimated
        /// rate in frames per second (0 when the samples were not usable).
        /// </summary>
        public bool AddFrame(double frameSeconds, out double refreshRate)
        {
            refreshRate = 0d;
            if (_count == _samples.Length)
            {
                refreshRate = EstimateRate();
                return true;
            }

            if (_skipped < _warmupFrames)
            {
                _skipped++;
                return false;
            }

            _samples[_count++] = frameSeconds;
            if (_count < _samples.Length)
            {
                return false;
            }

            refreshRate = EstimateRate();
            return true;
        }

        private double EstimateRate()
        {
            var sorted = (double[])_samples.Clone();
            Array.Sort(sorted);
            double median = sorted[sorted.Length / 2];
            return median > 0d && !double.IsNaN(median) && !double.IsInfinity(median) ? 1d / median : 0d;
        }
    }
}
