#nullable enable
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when a contract is completed and its bonus paid (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct ContractCompletedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public ContractCompletedEvent(
            int contractId,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            int amount,
            long reward,
            int populationBonus,
            GameDate date)
        {
            ContractId = contractId;
            CargoId = cargoId;
            TargetKind = targetKind;
            TargetIndex = targetIndex;
            Amount = amount;
            Reward = reward;
            PopulationBonus = populationBonus;
            Date = date;
        }

        /// <summary>
        /// Gets the completed contract id.
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
        /// Gets the one-off bonus paid, in whole money.
        /// </summary>
        public long Reward { get; }

        /// <summary>
        /// Gets the inhabitants actually added to the target town (0 for industries or a town at its cap).
        /// </summary>
        public int PopulationBonus { get; }

        /// <summary>
        /// Gets the completion date.
        /// </summary>
        public GameDate Date { get; }
    }
}
