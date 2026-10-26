#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// What a contract asks units to be delivered to.
    /// </summary>
    public enum ContractTargetKind
    {
        /// <summary>A town (named by its id).</summary>
        Town = 0,

        /// <summary>An industry (named by its localization key and instance number).</summary>
        Industry = 1
    }

    /// <summary>
    /// UI-facing snapshot of one open contract for the contracts panel (Issue #66).
    /// Presentation builds these from Core; the bonus uses the same Core formula as the payout.
    /// </summary>
    public readonly struct ContractListItem : IEquatable<ContractListItem>
    {
        /// <summary>
        /// Initializes a contract row. For a town target <paramref name="targetNumber"/> is the town id and
        /// <paramref name="targetNameKey"/> is ignored; for an industry it is the instance index and the key names
        /// the industry type.
        /// </summary>
        public ContractListItem(
            int contractId,
            string cargoName,
            ContractTargetKind targetKind,
            int targetNumber,
            string? targetNameKey,
            int tileX,
            int tileY,
            long delivered,
            int required,
            string deadline,
            long bonus)
        {
            if (string.IsNullOrWhiteSpace(cargoName))
            {
                throw new ArgumentException("Cargo name cannot be null or whitespace.", nameof(cargoName));
            }

            if (targetKind == ContractTargetKind.Industry && string.IsNullOrWhiteSpace(targetNameKey))
            {
                throw new ArgumentException("An industry target needs a name key.", nameof(targetNameKey));
            }

            if (targetNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetNumber), targetNumber, "Target number cannot be negative.");
            }

            if (delivered < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(delivered), delivered, "Delivered cannot be negative.");
            }

            if (required <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(required), required, "Required must be positive.");
            }

            if (bonus < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(bonus), bonus, "Bonus cannot be negative.");
            }

            ContractId = contractId;
            CargoName = cargoName;
            TargetKind = targetKind;
            TargetNumber = targetNumber;
            TargetNameKey = targetKind == ContractTargetKind.Industry ? targetNameKey : null;
            TileX = tileX;
            TileY = tileY;
            Delivered = delivered;
            Required = required;
            Deadline = deadline ?? throw new ArgumentNullException(nameof(deadline));
            Bonus = bonus;
        }

        /// <summary>
        /// Gets the contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the display name of the requested cargo.
        /// </summary>
        public string CargoName { get; }

        /// <summary>
        /// Gets whether the target is a town or an industry.
        /// </summary>
        public ContractTargetKind TargetKind { get; }

        /// <summary>
        /// Gets the town id or the industry instance index.
        /// </summary>
        public int TargetNumber { get; }

        /// <summary>
        /// Gets the industry name key, or null for a town.
        /// </summary>
        public string? TargetNameKey { get; }

        /// <summary>
        /// Gets the target tile column (the camera centers here when the row is clicked).
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the target tile row.
        /// </summary>
        public int TileY { get; }

        /// <summary>
        /// Gets the units delivered so far.
        /// </summary>
        public long Delivered { get; }

        /// <summary>
        /// Gets the units required.
        /// </summary>
        public int Required { get; }

        /// <summary>
        /// Gets the formatted deadline date.
        /// </summary>
        public string Deadline { get; }

        /// <summary>
        /// Gets the completion bonus.
        /// </summary>
        public long Bonus { get; }

        /// <inheritdoc />
        public bool Equals(ContractListItem other)
        {
            return ContractId == other.ContractId
                && string.Equals(CargoName, other.CargoName, StringComparison.Ordinal)
                && TargetKind == other.TargetKind
                && TargetNumber == other.TargetNumber
                && string.Equals(TargetNameKey, other.TargetNameKey, StringComparison.Ordinal)
                && TileX == other.TileX
                && TileY == other.TileY
                && Delivered == other.Delivered
                && Required == other.Required
                && string.Equals(Deadline, other.Deadline, StringComparison.Ordinal)
                && Bonus == other.Bonus;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ContractListItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ContractId;
                hash = (hash * 397) ^ Delivered.GetHashCode();
                hash = (hash * 397) ^ Required;
                hash = (hash * 397) ^ TargetNumber;
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Deadline ?? string.Empty);
            }
        }
    }
}
