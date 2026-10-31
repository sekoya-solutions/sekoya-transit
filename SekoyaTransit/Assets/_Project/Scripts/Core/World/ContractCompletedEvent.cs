#nullable enable
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when an accepted contract has been delivered in full; the reward waits to be claimed
    /// (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct ContractCompletedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public ContractCompletedEvent(int contractId, string cargoId, int townIndex, int amount, GameDate date)
        {
            ContractId = contractId;
            CargoId = cargoId;
            TownIndex = townIndex;
            Amount = amount;
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
        /// Gets the target town's index in the world's town list.
        /// </summary>
        public int TownIndex { get; }

        /// <summary>
        /// Gets the requested units.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// Gets the completion date.
        /// </summary>
        public GameDate Date { get; }
    }
}
