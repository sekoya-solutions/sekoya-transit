#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Decides when the shell, hidden during load, may be shown: once every load-time text request has been started
    /// and none is running. The timeout is a safety net so a stalled or crashed load can never leave the game without
    /// a menu; it is not the expected path.
    /// </summary>
    /// <remarks>
    /// The timeout counts running game time, not wall-clock time. Request callbacks only run between frames, so a
    /// frame blocked for many seconds (the first frame can take that long on a slow machine) says nothing about a
    /// stalled load, and must not use up the timeout. Each frame therefore counts for at most
    /// <see cref="MaxFrameSeconds"/>.
    /// </remarks>
    public sealed class ShellRevealGate
    {
        /// <summary>
        /// The most a single frame can add to the elapsed time.
        /// </summary>
        public const float MaxFrameSeconds = 0.25f;

        private readonly float _timeoutSeconds;
        private float _elapsedSeconds;
        private bool _allRequested;

        /// <summary>
        /// Initializes a gate that times out after <paramref name="timeoutSeconds"/> of running game time.
        /// </summary>
        public ShellRevealGate(float timeoutSeconds)
        {
            if (timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "Timeout must be positive.");
            }

            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Gets whether the safety timeout has expired.
        /// </summary>
        public bool HasTimedOut => _elapsedSeconds >= _timeoutSeconds;

        /// <summary>
        /// Advances the timeout by one frame of the given duration.
        /// </summary>
        public void Tick(float frameSeconds)
        {
            if (frameSeconds > 0f)
            {
                _elapsedSeconds += Math.Min(frameSeconds, MaxFrameSeconds);
            }
        }

        /// <summary>
        /// Records that every load-time text request has been started, so a pending count of zero means finished.
        /// </summary>
        public void MarkAllRequested()
        {
            _allRequested = true;
        }

        /// <summary>
        /// Returns whether the load-time texts have all finished.
        /// </summary>
        public bool IsLoaded(int pendingRequests)
        {
            return _allRequested && pendingRequests == 0;
        }
    }
}
