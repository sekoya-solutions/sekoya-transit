#nullable enable
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when an accepted contract passes its deadline unfinished (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct ContractFailedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public ContractFailedEvent(
            int contractId,
            string cargoId,
            int townIndex,
            int amount,
            long delivered,
            GameDate date)
        {
            ContractId = contractId;
            CargoId = cargoId;
            TownIndex = townIndex;
            Amount = amount;
            Delivered = delivered;
            Date = date;
        }

        /// <summary>
        /// Gets the failed contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the requested cargo id.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets the target town's index in the world's town list.
        /// </summary>
        public int TownIndex { get; }

        /// <summary>
        /// Gets the requested units.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Gets the units delivered before the deadline.
        /// </summary>
        public long Delivered { get; }

        /// <summary>
        /// Gets the date the contract failed.
        /// </summary>
        public GameDate Date { get; }
    }
}
