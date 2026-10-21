#nullable enable
using System;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Identifies the autosave slot or a validated manual save slot.
    /// </summary>
    public readonly struct SaveSlot : IEquatable<SaveSlot>
    {
        private const string _autosaveKey = "autosave";
        private const string _manualPrefix = "manual-";
        private const int _maximumManualIdentifierLength = 48;

        /// <summary>
        /// Gets the single autosave slot.
        /// </summary>
        public static SaveSlot Autosave { get; } = new SaveSlot(_autosaveKey, true);

        private SaveSlot(string storageKey, bool isAutosave)
        {
            StorageKey = storageKey;
            IsAutosave = isAutosave;
        }

        /// <summary>
        /// Gets the storage-safe key for this slot.
        /// </summary>
        public string StorageKey { get; }

        /// <summary>
        /// Gets whether this is the reserved autosave slot.
        /// </summary>
        public bool IsAutosave { get; }

        /// <summary>
        /// Creates a manual slot from a case-insensitive ASCII identifier using
        /// letters, digits, hyphens, or underscores.
        /// </summary>
        public static SaveSlot Manual(string identifier)
        {
            if (string.IsNullOrEmpty(identifier) || identifier.Length > _maximumManualIdentifierLength)
            {
                throw new ArgumentException(
                    $"Manual slot identifiers must contain 1 to {_maximumManualIdentifierLength} characters.",
                    nameof(identifier));
            }

            for (int index = 0; index < identifier.Length; index++)
            {
                char character = identifier[index];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-' ||
                    character == '_';
                if (!valid)
                {
                    throw new ArgumentException(
                        "Manual slot identifiers may contain only ASCII letters, digits, hyphens, and underscores.",
                        nameof(identifier));
                }
            }

            return new SaveSlot(_manualPrefix + identifier.ToLowerInvariant(), false);
        }

        /// <summary>
        /// Parses a validated storage key returned by a save store.
        /// </summary>
        public static bool TryParseStorageKey(string? storageKey, out SaveSlot slot)
        {
            if (string.Equals(storageKey, _autosaveKey, StringComparison.Ordinal))
            {
                slot = Autosave;
                return true;
            }

            if (storageKey != null &&
                storageKey.StartsWith(_manualPrefix, StringComparison.Ordinal))
            {
                string identifier = storageKey.Substring(_manualPrefix.Length);
                try
                {
                    slot = Manual(identifier);
                    return string.Equals(slot.StorageKey, storageKey, StringComparison.Ordinal);
                }
                catch (ArgumentException)
                {
                }
            }

            slot = default;
            return false;
        }

        public bool Equals(SaveSlot other)
        {
            return IsAutosave == other.IsAutosave &&
                string.Equals(StorageKey, other.StorageKey, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is SaveSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((StorageKey?.GetHashCode() ?? 0) * 397) ^ IsAutosave.GetHashCode();
            }
        }

        public override string ToString()
        {
            return StorageKey ?? string.Empty;
        }

        public static bool operator ==(SaveSlot left, SaveSlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SaveSlot left, SaveSlot right)
        {
            return !left.Equals(right);
        }
    }
}
