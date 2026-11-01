#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one contract of the active contracts box (Issue #266): a running contract, or a finished
    /// one whose reward is not claimed yet. Presentation builds these from Core.
    /// </summary>
    public readonly struct ActiveContractItem : IEquatable<ActiveContractItem>
    {
        /// <summary>
        /// Initializes an item. <paramref name="daysLeft"/> is the days until the deadline (0 once it is reached) and
        /// only matters while the contract is not <paramref name="completed"/>.
        /// </summary>
        public ActiveContractItem(
            int contractId,
            string cargoId,
            string townName,
            int tileX,
            int tileY,
            long delivered,
            int required,
            int daysLeft,
            bool completed)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (delivered < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(delivered), delivered, "Delivered cannot be negative.");
            }

            if (required <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(required), required, "Required must be positive.");
            }

            if (daysLeft < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(daysLeft), daysLeft, "Days left cannot be negative.");
            }

            ContractId = contractId;
            CargoId = cargoId;
            TownName = townName ?? throw new ArgumentNullException(nameof(townName));
            TileX = tileX;
            TileY = tileY;
            Delivered = delivered;
            Required = required;
            DaysLeft = daysLeft;
            Completed = completed;
        }

        /// <summary>
        /// Gets the contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the id of the requested cargo.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets the name of the target town.
        /// </summary>
        public string TownName { get; }

        /// <summary>
        /// Gets the target tile column (the camera centers here when the entry is clicked).
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the target tile row.
        /// </summary>
        public int TileY { get; }

        /// <summary>
        /// Gets the units delivered since the contract was accepted.
        /// </summary>
        public long Delivered { get; }

        /// <summary>
        /// Gets the units required.
        /// </summary>
        public int Required { get; }

        /// <summary>
        /// Gets the days until the deadline.
        /// </summary>
        public int DaysLeft { get; }

        /// <summary>
        /// Gets whether the goal is delivered in full and the reward waits to be claimed.
        /// </summary>
        public bool Completed { get; }

        /// <inheritdoc />
        public bool Equals(ActiveContractItem other)
        {
            return ContractId == other.ContractId
                && string.Equals(CargoId, other.CargoId, StringComparison.Ordinal)
                && string.Equals(TownName, other.TownName, StringComparison.Ordinal)
                && TileX == other.TileX
                && TileY == other.TileY
                && Delivered == other.Delivered
                && Required == other.Required
                && DaysLeft == other.DaysLeft
                && Completed == other.Completed;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ActiveContractItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ContractId;
                hash = (hash * 397) ^ Delivered.GetHashCode();
                hash = (hash * 397) ^ DaysLeft;
                return (hash * 397) ^ (Completed ? 1 : 0);
            }
        }
    }
}
