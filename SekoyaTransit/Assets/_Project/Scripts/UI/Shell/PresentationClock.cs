#nullable enable
using System;
using UnityEngine;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The clock of presentation effects that run in real time, independent of game speed and pause: toasts, cash
    /// popups, the goals panels (Issue #352). In play it is Unity's unscaled (real) time. While the Editor capture tool
    /// records (ADR 0036), it advances by exactly one fixed step per rendered frame, like the game under the Recorder's
    /// fixed frame rate, so two recordings of the same shot show these effects at the same moments. Unity's own
    /// unscaled time keeps following the wall clock during a capture, which is why these effects need this clock.
    /// </summary>
    public static class PresentationClock
    {
        private static readonly FixedStepClock _clock = new FixedStepClock();

        /// <summary>
        /// Gets the current time in seconds.
        /// </summary>
        public static float Now => _clock.Now(Time.frameCount, Time.unscaledTime);

        /// <summary>
        /// Gets the seconds the current frame advances the clock.
        /// </summary>
        public static float DeltaTime => _clock.DeltaTime(Time.unscaledDeltaTime);

        /// <summary>
        /// Makes the clock advance exactly <paramref name="stepSeconds"/> per frame from this frame on (Editor capture
        /// tool).
        /// </summary>
        public static void BeginFixedStep(float stepSeconds)
        {
            _clock.BeginFixedStep(stepSeconds, Time.frameCount, Time.unscaledTime);
        }

        /// <summary>
        /// Returns the clock to real time without a jump.
        /// </summary>
        public static void EndFixedStep()
        {
            _clock.EndFixedStep(Time.frameCount, Time.unscaledTime);
        }
    }

    /// <summary>
    /// The logic of <see cref="PresentationClock"/>, with the frame count and real time passed in so it can be tested.
    /// The time never jumps when a fixed step begins or ends: it continues from the value it had.
    /// </summary>
    public sealed class FixedStepClock
    {
        private float _offset;
        private float? _step;
        private int _anchorFrame;
        private float _anchorTime;

        /// <summary>
        /// Gets whether a fixed step is active.
        /// </summary>
        public bool IsFixedStep => _step.HasValue;

        /// <summary>
        /// Gets the time at <paramref name="frame"/>, given the real (unscaled) time <paramref name="realTime"/>.
        /// </summary>
        public float Now(int frame, float realTime)
        {
            if (_step.HasValue)
            {
                return _anchorTime + ((frame - _anchorFrame) * _step.Value);
            }

            return realTime + _offset;
        }

        /// <summary>
        /// Gets the seconds the current frame advances the clock, given the real frame time.
        /// </summary>
        public float DeltaTime(float realDeltaTime)
        {
            return _step ?? realDeltaTime;
        }

        /// <summary>
        /// Starts advancing <paramref name="stepSeconds"/> per frame from <paramref name="frame"/> on.
        /// </summary>
        public void BeginFixedStep(float stepSeconds, int frame, float realTime)
        {
            if (!(stepSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "The step must be more than 0 seconds.");
            }

            _anchorTime = Now(frame, realTime);
            _anchorFrame = frame;
            _step = stepSeconds;
        }

        /// <summary>
        /// Returns to real time, continuing from the time reached.
        /// </summary>
        public void EndFixedStep(int frame, float realTime)
        {
            if (!_step.HasValue)
            {
                return;
            }

            float now = Now(frame, realTime);
            _step = null;
            _offset = now - realTime;
        }
    }
}
