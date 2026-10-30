#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Where a contract of the contracts panel stands (Issue #227).
    /// </summary>
    public enum ContractUiStatus
    {
        /// <summary>Offered: the player may accept or decline it.</summary>
        Proposal = 0,

        /// <summary>Accepted and running: the player may cancel it.</summary>
        Accepted = 1,

        /// <summary>Delivered in full: the player may claim the reward.</summary>
        Completed = 2,

        /// <summary>The deadline passed: the player may remove it.</summary>
        Failed = 3
    }

    /// <summary>
    /// What the player asked for with a button of a contract row (Issue #227).
    /// </summary>
    public enum ContractUiAction
    {
        /// <summary>Accept a proposal.</summary>
        Accept = 0,

        /// <summary>Decline a proposal.</summary>
        Decline = 1,

        /// <summary>Cancel an accepted contract.</summary>
        Cancel = 2,

        /// <summary>Claim the reward of a completed contract.</summary>
        ClaimReward = 3,

        /// <summary>Remove a failed contract.</summary>
        Remove = 4
    }

    /// <summary>
    /// UI-facing snapshot of one contract for the contracts panel (Issues #66 and #227). Presentation builds these from
    /// Core; the reward uses the same Core formula as the payout.
    /// </summary>
    public readonly struct ContractListItem : IEquatable<ContractListItem>
    {
        /// <summary>
        /// Initializes a contract row. <paramref name="dateText"/> is the deadline of an accepted contract, the
        /// completion or failure date of a settled one, and empty for a proposal. <paramref name="canAccept"/> only
        /// matters for a proposal.
        /// </summary>
        public ContractListItem(
            int contractId,
            string cargoId,
            string townName,
            int tileX,
            int tileY,
            ContractUiStatus status,
            long delivered,
            int required,
            int durationMonths,
            string dateText,
            long reward,
            bool canAccept)
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

            if (durationMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationMonths),
                    durationMonths,
                    "Duration must be positive.");
            }

            if (reward < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(reward), reward, "Reward cannot be negative.");
            }

            ContractId = contractId;
            CargoId = cargoId;
            TownName = townName ?? throw new ArgumentNullException(nameof(townName));
            TileX = tileX;
            TileY = tileY;
            Status = status;
            Delivered = delivered;
            Required = required;
            DurationMonths = durationMonths;
            DateText = dateText ?? throw new ArgumentNullException(nameof(dateText));
            Reward = reward;
            CanAccept = canAccept;
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
        /// Gets the target tile column (the camera centers here when the contract is clicked).
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the target tile row.
        /// </summary>
        public int TileY { get; }

        /// <summary>
        /// Gets the contract status.
        /// </summary>
        public ContractUiStatus Status { get; }

        /// <summary>
        /// Gets the units delivered since the contract was accepted.
        /// </summary>
        public long Delivered { get; }

        /// <summary>
        /// Gets the units required.
        /// </summary>
        public int Required { get; }

        /// <summary>
        /// Gets the months an accepted contract lasts.
        /// </summary>
        public int DurationMonths { get; }

        /// <summary>
        /// Gets the formatted deadline, or completion or failure date (empty for a proposal).
        /// </summary>
        public string DateText { get; }

        /// <summary>
        /// Gets the reward paid when it is claimed.
        /// </summary>
        public long Reward { get; }

        /// <summary>
        /// Gets whether a proposal can be accepted now (the accepted limit is not reached).
        /// </summary>
        public bool CanAccept { get; }

        /// <inheritdoc />
        public bool Equals(ContractListItem other)
        {
            return ContractId == other.ContractId
                && string.Equals(CargoId, other.CargoId, StringComparison.Ordinal)
                && string.Equals(TownName, other.TownName, StringComparison.Ordinal)
                && TileX == other.TileX
                && TileY == other.TileY
                && Status == other.Status
                && Delivered == other.Delivered
                && Required == other.Required
                && DurationMonths == other.DurationMonths
                && string.Equals(DateText, other.DateText, StringComparison.Ordinal)
                && Reward == other.Reward
                && CanAccept == other.CanAccept;
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
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ Delivered.GetHashCode();
                hash = (hash * 397) ^ Required;
                hash = (hash * 397) ^ (CanAccept ? 1 : 0);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DateText ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// What the contracts panel shows: the contracts plus the accepted count and the limits of the map size.
    /// </summary>
    public sealed class ContractsPanelData
    {
        /// <summary>
        /// Initializes the panel data.
        /// </summary>
        public ContractsPanelData(
            IReadOnlyList<ContractListItem> items,
            int acceptedCount,
            int maxAccepted,
            int maxContracts)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            AcceptedCount = acceptedCount;
            MaxAccepted = maxAccepted;
            MaxContracts = maxContracts;
        }

        /// <summary>
        /// Gets an empty panel (no contracts, no limits).
        /// </summary>
        public static ContractsPanelData Empty { get; } =
            new ContractsPanelData(Array.Empty<ContractListItem>(), 0, 0, 0);

        /// <summary>
        /// Gets the contracts in offer order.
        /// </summary>
        public IReadOnlyList<ContractListItem> Items { get; }

        /// <summary>
        /// Gets the number of accepted contracts.
        /// </summary>
        public int AcceptedCount { get; }

        /// <summary>
        /// Gets how many contracts can be accepted at once on this map size.
        /// </summary>
        public int MaxAccepted { get; }

        /// <summary>
        /// Gets how many contracts the list holds at most on this map size.
        /// </summary>
        public int MaxContracts { get; }

        /// <summary>
        /// Gets whether the list is at its maximum, so no new contract can appear.
        /// </summary>
        public bool IsFull => Items.Count >= MaxContracts;
    }
}
