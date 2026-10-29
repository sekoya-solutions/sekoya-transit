#nullable enable
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when a new contract appears (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct ContractOfferedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public ContractOfferedEvent(
            int contractId,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            int amount,
            GameDate deadline)
        {
            ContractId = contractId;
            CargoId = cargoId;
            TargetKind = targetKind;
            TargetIndex = targetIndex;
            Amount = amount;
            Deadline = deadline;
        }

        /// <summary>
        /// Gets the new contract id.
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
        /// Gets the last date the contract stays open.
        /// </summary>
        public GameDate Deadline { get; }
    }
}
