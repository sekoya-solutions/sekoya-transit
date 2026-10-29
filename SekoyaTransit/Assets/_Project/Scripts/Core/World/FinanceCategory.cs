namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Category of a company cash change in the finance ledger (Issue #66). Values are persisted by the save
    /// format: never renumber them.
    /// </summary>
    public enum FinanceCategory
    {
        /// <summary>Income from accepted cargo and passenger deliveries (road and rail).</summary>
        DeliveryIncome = 0,

        /// <summary>Income from contract completion bonuses.</summary>
        ContractBonus = 1,

        /// <summary>Income from selling vehicles and wagons.</summary>
        VehicleSales = 2,

        /// <summary>Spending on monthly vehicle running costs (upkeep).</summary>
        RunningCosts = 3,

        /// <summary>Spending on buying vehicles and wagons.</summary>
        VehiclePurchases = 4,

        /// <summary>Spending on permanently unlocking vehicle types.</summary>
        VehicleUnlocks = 5
    }
}
