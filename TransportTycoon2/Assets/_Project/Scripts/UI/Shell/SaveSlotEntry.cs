#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// One row of the save and load panel: the autosave or a numbered manual slot and what it holds (Issue #183).
    /// </summary>
    public sealed class SaveSlotEntry : IEquatable<SaveSlotEntry>
    {
        /// <summary>
        /// Id of the autosave slot.
        /// </summary>
        public const string AutosaveId = "autosave";

        /// <summary>
        /// Number of manual slots offered by the panel.
        /// </summary>
        public const int ManualSlotCount = 5;

        private SaveSlotEntry(
            string id,
            bool isEmpty,
            bool isUnreadable,
            string dateText,
            ulong seed,
            int mapTiles)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            IsEmpty = isEmpty;
            IsUnreadable = isUnreadable;
            DateText = dateText ?? throw new ArgumentNullException(nameof(dateText));
            Seed = seed;
            MapTiles = mapTiles;
        }

        /// <summary>
        /// Gets <see cref="AutosaveId"/> or the manual slot number ("1" to "5").
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets whether this is the autosave slot.
        /// </summary>
        public bool IsAutosave => string.Equals(Id, AutosaveId, StringComparison.Ordinal);

        /// <summary>
        /// Gets whether nothing is saved in the slot.
        /// </summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// Gets whether the slot holds a file that could not be read.
        /// </summary>
        public bool IsUnreadable { get; }

        /// <summary>
        /// Gets the in-game date of the save, ready for display.
        /// </summary>
        public string DateText { get; }

        /// <summary>
        /// Gets the world seed of the save.
        /// </summary>
        public ulong Seed { get; }

        /// <summary>
        /// Gets the map width and height in tiles.
        /// </summary>
        public int MapTiles { get; }

        /// <summary>
        /// Gets whether a game can be loaded from the slot.
        /// </summary>
        public bool CanLoad => !IsEmpty && !IsUnreadable;

        /// <summary>
        /// Creates an entry for a slot with no save.
        /// </summary>
        public static SaveSlotEntry Empty(string id)
        {
            return new SaveSlotEntry(id, true, false, string.Empty, 0UL, 0);
        }

        /// <summary>
        /// Creates an entry for a slot whose file cannot be read.
        /// </summary>
        public static SaveSlotEntry Unreadable(string id)
        {
            return new SaveSlotEntry(id, false, true, string.Empty, 0UL, 0);
        }

        /// <summary>
        /// Creates an entry for a readable save.
        /// </summary>
        public static SaveSlotEntry Filled(string id, string dateText, ulong seed, int mapTiles)
        {
            return new SaveSlotEntry(id, false, false, dateText, seed, mapTiles);
        }

        /// <inheritdoc />
        public bool Equals(SaveSlotEntry? other)
        {
            return other is not null
                && string.Equals(Id, other.Id, StringComparison.Ordinal)
                && IsEmpty == other.IsEmpty
                && IsUnreadable == other.IsUnreadable
                && string.Equals(DateText, other.DateText, StringComparison.Ordinal)
                && Seed == other.Seed
                && MapTiles == other.MapTiles;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as SaveSlotEntry);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return unchecked((Id.GetHashCode() * 31) + DateText.GetHashCode() + Seed.GetHashCode() + MapTiles);
        }
    }
}
