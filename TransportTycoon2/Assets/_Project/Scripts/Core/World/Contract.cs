#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// An open contract: bring <see cref="Amount"/> units of <see cref="CargoId"/> to one town or industry
    /// before <see cref="Deadline"/> (Issue #173).
    /// </summary>
    public sealed class Contract
    {
        /// <summary>
        /// Initializes a contract. <paramref name="targetKind"/> must be town or industry and
        /// <paramref name="targetIndex"/> is the index in the world's towns or industries.
        /// </summary>
        public Contract(
            int id,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            int amount,
            long delivered,
            GameDate startDate,
            GameDate deadline)
        {
            if (id < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Contract id must be positive.");
            }

            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (targetKind != DeliveryDestinationKind.Town && targetKind != DeliveryDestinationKind.Industry)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetKind),
                    targetKind,
                    "A contract target must be a town or an industry.");
            }

            if (targetIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, "Target index cannot be negative.");
            }

            if (amount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Contract amount must be at least 1.");
            }

            if (delivered < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(delivered), delivered, "Delivered units cannot be negative.");
            }

            if (deadline < startDate)
            {
                throw new ArgumentException("Contract deadline cannot be before its start date.", nameof(deadline));
            }

            Id = id;
            CargoId = cargoId;
            TargetKind = targetKind;
            TargetIndex = targetIndex;
            Amount = amount;
            Delivered = delivered;
            StartDate = startDate;
            Deadline = deadline;
        }

        /// <summary>
        /// Gets the world-unique sequential contract id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the cargo content id.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets whether the target is a town or an industry.
        /// </summary>
        public DeliveryDestinationKind TargetKind { get; }

        /// <summary>
        /// Gets the target's index in the world's town or industry list.
        /// </summary>
        public int TargetIndex { get; }

        /// <summary>
        /// Gets the units that must be delivered.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Gets the units delivered so far (may exceed <see cref="Amount"/> until the completion step runs).
        /// </summary>
        public long Delivered { get; private set; }

        /// <summary>
        /// Gets the date the contract appeared.
        /// </summary>
        public GameDate StartDate { get; }

        /// <summary>
        /// Gets the last date on which the contract is still open.
        /// </summary>
        public GameDate Deadline { get; }

        /// <summary>
        /// Returns an independent copy, so snapshots never share mutable progress with a live world.
        /// </summary>
        public Contract Copy()
        {
            return new Contract(Id, CargoId, TargetKind, TargetIndex, Amount, Delivered, StartDate, Deadline);
        }

        internal void AddDelivered(int units)
        {
            Delivered = checked(Delivered + units);
        }
    }
}
