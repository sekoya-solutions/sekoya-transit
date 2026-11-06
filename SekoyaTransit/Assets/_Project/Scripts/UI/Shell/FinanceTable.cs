#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The raw figures of one finance column (Issue #296): non-negative amounts per kind plus the signed total profit.
    /// Built from the Core ledger by Presentation, so the UI does not depend on Core.
    /// </summary>
    public readonly struct FinanceFigures
    {
        /// <summary>
        /// Initializes the figures.
        /// </summary>
        public FinanceFigures(
            long truckCost,
            long truckBenefit,
            long trainCost,
            long trainBenefit,
            long totalCost,
            long totalBenefit,
            long truckBought,
            long trainBought,
            long vehiclesSold,
            long vehiclesUnlocked,
            long contractProfit,
            long cheats,
            long totalProfit)
        {
            TruckCost = truckCost;
            TruckBenefit = truckBenefit;
            TrainCost = trainCost;
            TrainBenefit = trainBenefit;
            TotalCost = totalCost;
            TotalBenefit = totalBenefit;
            TruckBought = truckBought;
            TrainBought = trainBought;
            VehiclesSold = vehiclesSold;
            VehiclesUnlocked = vehiclesUnlocked;
            ContractProfit = contractProfit;
            Cheats = cheats;
            TotalProfit = totalProfit;
        }

        /// <summary>Gets the truck running costs.</summary>
        public long TruckCost { get; }

        /// <summary>Gets the truck delivery income.</summary>
        public long TruckBenefit { get; }

        /// <summary>Gets the train running costs.</summary>
        public long TrainCost { get; }

        /// <summary>Gets the train delivery income.</summary>
        public long TrainBenefit { get; }

        /// <summary>Gets all vehicle running costs.</summary>
        public long TotalCost { get; }

        /// <summary>Gets all vehicle delivery income.</summary>
        public long TotalBenefit { get; }

        /// <summary>Gets the money spent buying trucks.</summary>
        public long TruckBought { get; }

        /// <summary>Gets the money spent buying locomotives and wagons.</summary>
        public long TrainBought { get; }

        /// <summary>Gets the money received for sold vehicles.</summary>
        public long VehiclesSold { get; }

        /// <summary>Gets the money spent unlocking vehicle types.</summary>
        public long VehiclesUnlocked { get; }

        /// <summary>Gets the money received from contract bonuses.</summary>
        public long ContractProfit { get; }

        /// <summary>Gets the money added by the playtest cash cheat (Issue #316).</summary>
        public long Cheats { get; }

        /// <summary>Gets the change in company cash over the period (income minus spending, signed).</summary>
        public long TotalProfit { get; }
    }

    /// <summary>
    /// One column of the finance table: its figures, or "not available yet".
    /// </summary>
    public readonly struct FinanceColumn
    {
        /// <summary>
        /// Initializes a column.
        /// </summary>
        public FinanceColumn(bool isAvailable, FinanceFigures figures)
        {
            IsAvailable = isAvailable;
            Figures = figures;
        }

        /// <summary>
        /// Gets whether the figures are real (the period was observed in full).
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// Gets the figures (all zero when not available).
        /// </summary>
        public FinanceFigures Figures { get; }
    }

    /// <summary>
    /// The finance table data: last month, current month, last year, current year.
    /// </summary>
    public sealed class FinanceTable
    {
        /// <summary>
        /// Number of columns.
        /// </summary>
        public const int ColumnCount = 4;

        private readonly FinanceColumn[] _columns;

        /// <summary>
        /// Initializes the table.
        /// </summary>
        public FinanceTable(
            FinanceColumn lastMonth,
            FinanceColumn currentMonth,
            FinanceColumn lastYear,
            FinanceColumn currentYear)
        {
            _columns = new[] { lastMonth, currentMonth, lastYear, currentYear };
        }

        /// <summary>
        /// Gets an empty table where only the current period columns are available.
        /// </summary>
        public static FinanceTable Empty { get; } = new FinanceTable(
            new FinanceColumn(false, default),
            new FinanceColumn(true, default),
            new FinanceColumn(false, default),
            new FinanceColumn(true, default));

        /// <summary>
        /// Gets a column: 0 last month, 1 current month, 2 last year, 3 current year.
        /// </summary>
        public FinanceColumn GetColumn(int index)
        {
            if (index < 0 || index >= ColumnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown finance column.");
            }

            return _columns[index];
        }
    }
}
