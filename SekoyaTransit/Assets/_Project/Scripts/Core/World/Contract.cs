#nullable enable
using System;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A contract: bring <see cref="Amount"/> units of <see cref="CargoId"/> to one town (Issue #173, reworked into
    /// proposals the player accepts in Issue #227). Only deliveries made while the contract is
    /// <see cref="ContractStatus.Accepted"/> count.
    /// </summary>
    public sealed class Contract
    {
        /// <summary>
        /// Initializes a contract. <paramref name="townIndex"/> is the index in the world's towns. A proposal has no
        /// deliveries; its <paramref name="deadline"/> and <paramref name="settledDate"/> are not used yet and must
        /// equal <paramref name="startDate"/>, as must the <paramref name="settledDate"/> of an accepted contract.
        /// </summary>
        public Contract(
            int id,
            string cargoId,
            int townIndex,
            int amount,
            long delivered,
            ContractStatus status,
            GameDate startDate,
            GameDate deadline,
            GameDate settledDate)
        {
            if (id < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Contract id must be positive.");
            }

            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (townIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(townIndex), townIndex, "Town index cannot be negative.");
            }

            if (amount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Contract amount must be at least 1.");
            }

            if (delivered < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(delivered), delivered, "Delivered units cannot be negative.");
            }

            if (status < ContractStatus.Proposal || status > ContractStatus.Failed)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown contract status.");
            }

            if (deadline < startDate)
            {
                throw new ArgumentException("Contract deadline cannot be before its start date.", nameof(deadline));
            }

            if (settledDate < startDate)
            {
                throw new ArgumentException("Contract settled date cannot be before its start date.", nameof(settledDate));
            }

            if (status == ContractStatus.Proposal && (delivered != 0L || deadline != startDate))
            {
                throw new ArgumentException("A proposal has no deliveries and no deadline yet.", nameof(status));
            }

            if ((status == ContractStatus.Proposal || status == ContractStatus.Accepted) && settledDate != startDate)
            {
                throw new ArgumentException("An unsettled contract has no settled date.", nameof(settledDate));
            }

            Id = id;
            CargoId = cargoId;
            TownIndex = townIndex;
            Amount = amount;
            Delivered = delivered;
            Status = status;
            StartDate = startDate;
            Deadline = deadline;
            SettledDate = settledDate;
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
        /// Gets the target town's index in the world's town list.
        /// </summary>
        public int TownIndex { get; }

        /// <summary>
        /// Gets the units that must be delivered.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Gets the units delivered since the contract was accepted (may exceed <see cref="Amount"/> until the
        /// completion step runs).
        /// </summary>
        public long Delivered { get; private set; }

        /// <summary>
        /// Gets the contract status.
        /// </summary>
        public ContractStatus Status { get; private set; }

        /// <summary>
        /// Gets the date the contract was proposed.
        /// </summary>
        public GameDate StartDate { get; }

        /// <summary>
        /// Gets the last date on which the contract can still be completed (the proposal date until accepted).
        /// </summary>
        public GameDate Deadline { get; private set; }

        /// <summary>
        /// Gets the date the contract was completed or failed (the proposal date until then).
        /// </summary>
        public GameDate SettledDate { get; private set; }

        /// <summary>
        /// Returns an independent copy, so snapshots never share mutable progress with a live world.
        /// </summary>
        public Contract Copy()
        {
            return new Contract(Id, CargoId, TownIndex, Amount, Delivered, Status, StartDate, Deadline, SettledDate);
        }

        internal void AddDelivered(int units)
        {
            Delivered = checked(Delivered + units);
        }

        internal void Accept(GameDate deadline)
        {
            Status = ContractStatus.Accepted;
            Deadline = deadline;
        }

        internal void Complete(GameDate date)
        {
            Status = ContractStatus.Completed;
            SettledDate = date;
        }

        internal void Fail(GameDate date)
        {
            Status = ContractStatus.Failed;
            SettledDate = date;
        }
    }
}
