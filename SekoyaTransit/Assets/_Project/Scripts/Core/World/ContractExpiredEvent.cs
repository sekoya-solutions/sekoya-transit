#nullable enable
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when an unfinished contract passes its deadline (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct ContractExpiredEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public ContractExpiredEvent(
            int contractId,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            int amount,
            long delivered,
            GameDate date)
        {
            ContractId = contractId;
            CargoId = cargoId;
            TargetKind = targetKind;
            TargetIndex = targetIndex;
            Amount = amount;
            Delivered = delivered;
            Date = date;
        }

        /// <summary>
        /// Gets the expired contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the requested cargo id.
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
        /// Gets the requested units.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Gets the units delivered before expiry (progress is lost).
        /// </summary>
        public long Delivered { get; }

        /// <summary>
        /// Gets the date the expiry was processed.
        /// </summary>
        public GameDate Date { get; }
    }
}
