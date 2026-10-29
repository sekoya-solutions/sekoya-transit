#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The visible toasts: a small stack where each toast hides after a lifetime in real time seconds and the
    /// oldest toast is dropped when the stack is full (Issue #66). Never pauses or blocks the simulation: time is
    /// supplied by the caller (the shell passes unscaled real time).
    /// </summary>
    public sealed class ToastQueue
    {
        /// <summary>
        /// Placeholder real-time lifetime of a toast in seconds.
        /// </summary>
        public const float PlaceholderLifetimeSeconds = 6f;

        /// <summary>
        /// Placeholder number of toasts visible at once.
        /// </summary>
        public const int PlaceholderMaxVisible = 4;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly float _lifetimeSeconds;
        private readonly int _maxVisible;

        /// <summary>
        /// Initializes a queue.
        /// </summary>
        public ToastQueue(float lifetimeSeconds = PlaceholderLifetimeSeconds, int maxVisible = PlaceholderMaxVisible)
        {
            if (lifetimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetimeSeconds),
                    lifetimeSeconds,
                    "Toast lifetime must be positive.");
            }

            if (maxVisible <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxVisible), maxVisible, "Max visible must be positive.");
            }

            _lifetimeSeconds = lifetimeSeconds;
            _maxVisible = maxVisible;
        }

        /// <summary>
        /// Gets a counter that increases whenever the visible toasts change.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Gets the number of visible toasts.
        /// </summary>
        public int Count
        {
            get
            {
                return _entries.Count;
            }
        }

        /// <summary>
        /// Gets the visible toast at an index (0 is the oldest).
        /// </summary>
        public ToastMessage this[int index]
        {
            get
            {
                return _entries[index].Message;
            }
        }

        /// <summary>
        /// Shows a toast at <paramref name="nowSeconds"/>, dropping the oldest when the stack is full.
        /// </summary>
        public void Enqueue(ToastMessage message, float nowSeconds)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            _entries.Add(new Entry(message, nowSeconds + _lifetimeSeconds));
            while (_entries.Count > _maxVisible)
            {
                _entries.RemoveAt(0);
            }

            Version++;
        }

        /// <summary>
        /// Hides toasts whose lifetime ended by <paramref name="nowSeconds"/>. Returns whether any were hidden.
        /// </summary>
        public bool Expire(float nowSeconds)
        {
            bool removed = false;
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                if (nowSeconds >= _entries[index].ExpiresAt)
                {
                    _entries.RemoveAt(index);
                    removed = true;
                }
            }

            if (removed)
            {
                Version++;
            }

            return removed;
        }

        private readonly struct Entry
        {
            public Entry(ToastMessage message, float expiresAt)
            {
                Message = message;
                ExpiresAt = expiresAt;
            }

            public ToastMessage Message { get; }

            public float ExpiresAt { get; }
        }
    }
}
