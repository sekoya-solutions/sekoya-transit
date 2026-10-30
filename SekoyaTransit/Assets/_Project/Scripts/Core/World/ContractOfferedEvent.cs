#nullable enable
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when a new contract proposal appears (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct ContractOfferedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public ContractOfferedEvent(int contractId, string cargoId, int townIndex, int amount)
        {
            ContractId = contractId;
            CargoId = cargoId;
            TownIndex = townIndex;
            Amount = amount;
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
        /// Gets the target town's index in the world's town list.
        /// </summary>
        public int TownIndex { get; }

        /// <summary>
        /// Gets the requested units.
        /// </summary>
        public int Amount { get; }
    }
}
