#nullable enable
using System;

namespace TransportTycoon.Presentation.Diagnostics
{
    /// <summary>
    /// Accumulates per-frame timings over a fixed span of real time and returns one averaged summary per span, so a
    /// Development player can log its frame cost at a readable rate (Issue #70).
    /// </summary>
    public sealed class FrameStatsWindow
    {
        private readonly double _windowSeconds;
        private int _frames;
        private double _elapsedSeconds;
        private double _maxFrameSeconds;
        private Metric _mainThreadMs;
        private Metric _scriptsMs;
        private Metric _gcAllocatedBytes;

        /// <summary>
        /// Initializes a window that completes once <paramref name="windowSeconds"/> of frame time has accumulated.
        /// </summary>
        public FrameStatsWindow(double windowSeconds)
        {
            if (double.IsNaN(windowSeconds) || double.IsInfinity(windowSeconds) || windowSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(windowSeconds),
                    windowSeconds,
                    "The window must be a finite positive number of seconds.");
            }

            _windowSeconds = windowSeconds;
        }

        /// <summary>
        /// Adds one frame. A null metric means the profiler marker is not available on this player and is left out of
        /// that metric's average. Returns true and the summary when the window completes; the window then starts over.
        /// </summary>
        public bool AddFrame(
            double frameSeconds,
            double? mainThreadMs,
            double? scriptsMs,
            long? gcAllocatedBytes,
            out FrameStatsSummary summary)
        {
            if (double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds) || frameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameSeconds),
                    frameSeconds,
                    "The frame time must be a finite number of seconds, zero or more.");
            }

            _frames++;
            _elapsedSeconds += frameSeconds;
            _maxFrameSeconds = Math.Max(_maxFrameSeconds, frameSeconds);
            _mainThreadMs.Add(mainThreadMs);
            _scriptsMs.Add(scriptsMs);
            _gcAllocatedBytes.Add(gcAllocatedBytes);

            if (_elapsedSeconds < _windowSeconds)
            {
                summary = default;
                return false;
            }

            summary = new FrameStatsSummary(
                _frames,
                _elapsedSeconds,
                _maxFrameSeconds * 1000d,
                _mainThreadMs.Average,
                _scriptsMs.Average,
                _gcAllocatedBytes.Average);
            _frames = 0;
            _elapsedSeconds = 0d;
            _maxFrameSeconds = 0d;
            _mainThreadMs = default;
            _scriptsMs = default;
            _gcAllocatedBytes = default;
            return true;
        }

        private struct Metric
        {
            private double _sum;
            private int _count;

            public double? Average => _count == 0 ? (double?)null : _sum / _count;

            public void Add(double? value)
            {
                if (value.HasValue)
                {
                    _sum += value.Value;
                    _count++;
                }
            }
        }
    }
}
