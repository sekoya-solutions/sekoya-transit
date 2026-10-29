#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Records successive locomotive world positions and samples wagon placements along that path
    /// (snake trail). Guarantees wagons sit where the loco already was — never a chord across grass.
    /// Presentation-only; no Unity or Core dependency so EditMode tests can validate placement math.
    /// </summary>
    public sealed class RailWagonTrailHistory
    {
        private readonly List<float> _xs;
        private readonly List<float> _ys;
        private readonly int _maxPoints;
        private readonly float _minRecordDistanceSq;

        // Polyline scratch buffers reused across calls, so sampling every frame allocates nothing (Issue #70).
        private float[] _polylineX = Array.Empty<float>();
        private float[] _polylineY = Array.Empty<float>();

        /// <summary>
        /// Creates a trail buffer.
        /// </summary>
        /// <param name="maxPoints">Cap on stored samples (oldest dropped).</param>
        /// <param name="minRecordDistance">
        /// Minimum movement before a new sample is stored (world units).
        /// </param>
        public RailWagonTrailHistory(int maxPoints = 96, float minRecordDistance = 0.08f)
        {
            if (maxPoints < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPoints), maxPoints, "Need at least 2 points.");
            }

            if (minRecordDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minRecordDistance),
                    minRecordDistance,
                    "Min record distance cannot be negative.");
            }

            _maxPoints = maxPoints;
            _minRecordDistanceSq = minRecordDistance * minRecordDistance;
            _xs = new List<float>(maxPoints);
            _ys = new List<float>(maxPoints);
        }

        /// <summary>
        /// Gets how many recorded samples are stored (excludes the live loco tip passed to sample).
        /// </summary>
        public int Count
        {
            get
            {
                return _xs.Count;
            }
        }

        /// <summary>
        /// Clears all recorded samples (call when the vehicle despawns or hides).
        /// </summary>
        public void Clear()
        {
            _xs.Clear();
            _ys.Clear();
        }

        /// <summary>
        /// Replaces the buffer with a seed path, oldest first (farthest behind → closest to loco).
        /// Used when the train becomes visible before it has traveled enough for a natural trail.
        /// </summary>
        public void SeedOldestFirst(IReadOnlyList<float> xs, IReadOnlyList<float> ys)
        {
#pragma warning disable CA1510
            if (xs == null)
            {
                throw new ArgumentNullException(nameof(xs));
            }

            if (ys == null)
            {
                throw new ArgumentNullException(nameof(ys));
            }
#pragma warning restore CA1510

            if (xs.Count != ys.Count)
            {
                throw new ArgumentException("Seed x/y lengths must match.");
            }

            Clear();
            for (int index = 0; index < xs.Count; index++)
            {
                Append(xs[index], ys[index]);
            }
        }

        /// <summary>
        /// Records the current locomotive position when it has moved far enough from the last sample.
        /// </summary>
        public void Record(float x, float y)
        {
            if (_xs.Count > 0)
            {
                float dx = x - _xs[_xs.Count - 1];
                float dy = y - _ys[_ys.Count - 1];
                if ((dx * dx) + (dy * dy) < _minRecordDistanceSq)
                {
                    // Still update the tip so parked/micro-moves keep the newest sample current.
                    _xs[_xs.Count - 1] = x;
                    _ys[_ys.Count - 1] = y;
                    return;
                }
            }

            Append(x, y);
        }

        /// <summary>
        /// Samples wagon world positions at <paramref name="spacing"/> intervals behind the loco
        /// along the recorded path (arc length). Wagon 0 is closest to the locomotive.
        /// </summary>
        /// <param name="locoX">Current locomotive X (polyline tip).</param>
        /// <param name="locoY">Current locomotive Y.</param>
        /// <param name="wagonCount">Number of wagons to place.</param>
        /// <param name="spacing">World-unit gap between loco→wagon0 and between wagons.</param>
        /// <param name="outX">Length ≥ wagonCount; filled with wagon X.</param>
        /// <param name="outY">Length ≥ wagonCount; filled with wagon Y.</param>
        public void SampleWagons(
            float locoX,
            float locoY,
            int wagonCount,
            float spacing,
            float[] outX,
            float[] outY)
        {
#pragma warning disable CA1510
            if (outX == null)
            {
                throw new ArgumentNullException(nameof(outX));
            }

            if (outY == null)
            {
                throw new ArgumentNullException(nameof(outY));
            }
#pragma warning restore CA1510

            if (wagonCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wagonCount), wagonCount, "Wagon count cannot be negative.");
            }

            if (spacing <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Spacing must be positive.");
            }

            if (outX.Length < wagonCount || outY.Length < wagonCount)
            {
                throw new ArgumentException("Output arrays must be at least wagonCount long.");
            }

            if (wagonCount == 0)
            {
                return;
            }

            // Polyline: history (oldest → newest) then live loco tip.
            int pointCount = _xs.Count + 1;
            if (_polylineX.Length < pointCount)
            {
                _polylineX = new float[pointCount];
                _polylineY = new float[pointCount];
            }

            float[] px = _polylineX;
            float[] py = _polylineY;
            for (int index = 0; index < _xs.Count; index++)
            {
                px[index] = _xs[index];
                py[index] = _ys[index];
            }

            px[pointCount - 1] = locoX;
            py[pointCount - 1] = locoY;

            for (int wagon = 0; wagon < wagonCount; wagon++)
            {
                float targetDistance = spacing * (wagon + 1);
                SampleAtDistanceBehindTip(px, py, pointCount, targetDistance, out float wx, out float wy);
                outX[wagon] = wx;
                outY[wagon] = wy;
            }
        }

        private void Append(float x, float y)
        {
            _xs.Add(x);
            _ys.Add(y);
            while (_xs.Count > _maxPoints)
            {
                _xs.RemoveAt(0);
                _ys.RemoveAt(0);
            }
        }

        private static void SampleAtDistanceBehindTip(
            float[] px,
            float[] py,
            int pointCount,
            float targetDistance,
            out float x,
            out float y)
        {
            int last = pointCount - 1;
            if (last <= 0)
            {
                x = px[0];
                y = py[0];
                return;
            }

            float remaining = targetDistance;
            for (int index = last; index > 0; index--)
            {
                float ax = px[index];
                float ay = py[index];
                float bx = px[index - 1];
                float by = py[index - 1];
                float dx = bx - ax;
                float dy = by - ay;
                float segmentLength = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (segmentLength < 0.0001f)
                {
                    continue;
                }

                if (remaining <= segmentLength)
                {
                    float t = remaining / segmentLength;
                    x = ax + (dx * t);
                    y = ay + (dy * t);
                    return;
                }

                remaining -= segmentLength;
            }

            // Trail shorter than requested: clamp to oldest sample.
            x = px[0];
            y = py[0];
        }
    }
}
