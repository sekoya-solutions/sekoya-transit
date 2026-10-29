#nullable enable
using System;

namespace SekoyaTransit.Presentation.Audio
{
    /// <summary>
    /// Lets a sound start at most once per minimum gap of real time, so a burst of triggers (many deliveries paid at
    /// high simulation speed) plays one sound instead of a noisy stack (Issue #68).
    /// </summary>
    public sealed class SoundCooldown
    {
        private readonly float _minimumGapSeconds;
        private float? _lastStartSeconds;

        /// <summary>
        /// Initializes the cooldown; a gap of 0 lets every trigger start the sound.
        /// </summary>
        public SoundCooldown(float minimumGapSeconds)
        {
            if (float.IsNaN(minimumGapSeconds) || minimumGapSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumGapSeconds),
                    minimumGapSeconds,
                    "The minimum gap must be zero or positive.");
            }

            _minimumGapSeconds = minimumGapSeconds;
        }

        /// <summary>
        /// Returns whether the sound may start at <paramref name="nowSeconds"/>, and if so records that it started.
        /// </summary>
        public bool TryStart(float nowSeconds)
        {
            if (_lastStartSeconds.HasValue && nowSeconds - _lastStartSeconds.Value < _minimumGapSeconds)
            {
                return false;
            }

            _lastStartSeconds = nowSeconds;
            return true;
        }
    }
}
