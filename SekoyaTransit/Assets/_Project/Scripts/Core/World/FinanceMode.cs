namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Which kind of vehicle a ledger amount belongs to (Issue #296). Values are persisted by the save format: never
    /// renumber them.
    /// </summary>
    public enum FinanceMode
    {
        /// <summary>Not tied to one vehicle type: contract bonuses, unlocks, and amounts of saves before format 32.</summary>
        General = 0,

        /// <summary>Road vehicles (trucks).</summary>
        Road = 1,

        /// <summary>Rail vehicles (locomotives and wagons, trains).</summary>
        Rail = 2
    }
}
