#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one road/rail type for the unlocks panel.
    /// </summary>
    public readonly struct VehicleUnlockListItem : IEquatable<VehicleUnlockListItem>
    {
        /// <summary>
        /// Initializes an unlock row.
        /// </summary>
        public VehicleUnlockListItem(
            string typeId,
            string nameKey,
            VehicleListKind kind,
            int availableYear,
            long unlockCost,
            VehicleUnlockRowStatus status,
            bool canAffordUnlock)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Type id cannot be null or whitespace.", nameof(typeId));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (availableYear < 1900)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableYear),
                    availableYear,
                    "Available year cannot be before 1900.");
            }

            if (unlockCost < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(unlockCost), unlockCost, "Unlock cost cannot be negative.");
            }

            TypeId = typeId;
            NameKey = nameKey;
            Kind = kind;
            AvailableYear = availableYear;
            UnlockCost = unlockCost;
            Status = status;
            CanAffordUnlock = canAffordUnlock;
        }

        /// <summary>
        /// Gets the catalog type id.
        /// </summary>
        public string TypeId { get; }

        /// <summary>
        /// Gets the localization key for the type name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets road vs rail.
        /// </summary>
        public VehicleListKind Kind { get; }

        /// <summary>
        /// Gets the calendar year when the type appears.
        /// </summary>
        public int AvailableYear { get; }

        /// <summary>
        /// Gets the one-time unlock cost.
        /// </summary>
        public long UnlockCost { get; }

        /// <summary>
        /// Gets the row status.
        /// </summary>
        public VehicleUnlockRowStatus Status { get; }

        /// <summary>
        /// Gets whether company cash covers the unlock cost (only meaningful when Unlockable).
        /// </summary>
        public bool CanAffordUnlock { get; }

        /// <inheritdoc />
        public bool Equals(VehicleUnlockListItem other)
        {
            return TypeId == other.TypeId
                && NameKey == other.NameKey
                && Kind == other.Kind
                && AvailableYear == other.AvailableYear
                && UnlockCost == other.UnlockCost
                && Status == other.Status
                && CanAffordUnlock == other.CanAffordUnlock;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is VehicleUnlockListItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TypeId.GetHashCode();
                hash = (hash * 397) ^ NameKey.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ AvailableYear;
                hash = (hash * 397) ^ UnlockCost.GetHashCode();
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ CanAffordUnlock.GetHashCode();
                return hash;
            }
        }
    }
}
