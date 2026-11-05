#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Decides when the shell, hidden during load, may be shown: once every load-time text request has been started
    /// and none is running. The timeout is a safety net so a stalled or crashed load can never leave the game without
    /// a menu; it is not the expected path.
    /// </summary>
    public sealed class ShellRevealGate
    {
        private readonly float _deadlineSeconds;
        private bool _allRequested;

        /// <summary>
        /// Initializes a gate whose timeout runs from <paramref name="startSeconds"/>.
        /// </summary>
        public ShellRevealGate(float startSeconds, float timeoutSeconds)
        {
            if (timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "Timeout must be positive.");
            }

            _deadlineSeconds = startSeconds + timeoutSeconds;
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

        /// <summary>
        /// Returns whether the safety timeout has expired.
        /// </summary>
        public bool HasTimedOut(float nowSeconds)
        {
            return nowSeconds >= _deadlineSeconds;
        }
    }
}
