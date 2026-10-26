#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// All category totals of one calendar year for the finance panel (Issue #66).
    /// </summary>
    public sealed class FinanceYearRecap
    {
        private readonly FinanceRecapLine[] _lines;

        /// <summary>
        /// Initializes a year. A year of 0 means "not started yet".
        /// </summary>
        public FinanceYearRecap(int year, IReadOnlyList<FinanceRecapLine> lines)
        {
            if (year < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "Year cannot be negative.");
            }

            if (lines is null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            Year = year;
            _lines = new FinanceRecapLine[lines.Count];
            long income = 0L;
            long spending = 0L;
            for (int index = 0; index < lines.Count; index++)
            {
                _lines[index] = lines[index];
                if (lines[index].IsIncome)
                {
                    income = checked(income + lines[index].Amount);
                }
                else
                {
                    spending = checked(spending + lines[index].Amount);
                }
            }

            TotalIncome = income;
            TotalSpending = spending;
        }

        /// <summary>
        /// Gets the calendar year, or 0 before the first tick.
        /// </summary>
        public int Year { get; }

        /// <summary>
        /// Gets the category lines in display order.
        /// </summary>
        public IReadOnlyList<FinanceRecapLine> Lines
        {
            get
            {
                return _lines;
            }
        }

        /// <summary>
        /// Gets the sum of all income lines.
        /// </summary>
        public long TotalIncome { get; }

        /// <summary>
        /// Gets the sum of all spending lines.
        /// </summary>
        public long TotalSpending { get; }

        /// <summary>
        /// Gets income minus spending (negative when the company lost money).
        /// </summary>
        public long Net
        {
            get
            {
                return checked(TotalIncome - TotalSpending);
            }
        }
    }
}
