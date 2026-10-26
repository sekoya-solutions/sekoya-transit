#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Company money by category for the current and the previous calendar year (Issue #66). Every cash change
    /// of <see cref="GeneratedWorld"/> is recorded here, so the change in cash over any period equals total
    /// income minus total spending for that period.
    /// </summary>
    public sealed class FinanceLedger
    {
        private readonly FinanceYearTotals _current = new FinanceYearTotals();
        private readonly FinanceYearTotals _previous = new FinanceYearTotals();

        /// <summary>
        /// Gets the calendar year the current totals belong to, or 0 before the first tick observes the calendar.
        /// </summary>
        public int Year { get; private set; }

        /// <summary>
        /// Gets the totals of the current calendar year.
        /// </summary>
        public FinanceYearTotals Current
        {
            get
            {
                return _current;
            }
        }

        /// <summary>
        /// Gets the totals of the previous calendar year (empty when there was none).
        /// </summary>
        public FinanceYearTotals Previous
        {
            get
            {
                return _previous;
            }
        }

        /// <summary>
        /// Moves to <paramref name="year"/>: when it is the year after <see cref="Year"/> the current totals become
        /// the previous totals and the current totals reset. The first observation only records the year. Returns
        /// whether the totals rolled over.
        /// </summary>
        public bool AdvanceTo(int year)
        {
            if (year < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "The finance year cannot be negative.");
            }

            if (year == Year)
            {
                return false;
            }

            if (Year == 0)
            {
                Year = year;
                return false;
            }

            if (year < Year)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "The finance year cannot go backwards.");
            }

            if (year == Year + 1)
            {
                _previous.CopyFrom(_current);
            }
            else
            {
                _previous.Clear();
            }

            _current.Clear();
            Year = year;
            return true;
        }

        /// <summary>
        /// Replaces the ledger state when restoring a save.
        /// </summary>
        public void Restore(int year, FinanceYearTotals current, FinanceYearTotals previous)
        {
#pragma warning disable CA1510
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (previous == null)
            {
                throw new ArgumentNullException(nameof(previous));
            }
#pragma warning restore CA1510

            if (year < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "The finance year cannot be negative.");
            }

            Year = year;
            _current.CopyFrom(current);
            _previous.CopyFrom(previous);
        }

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public FinanceLedger Copy()
        {
            var copy = new FinanceLedger();
            copy.Restore(Year, _current, _previous);
            return copy;
        }

        internal void Record(FinanceCategory category, long amount)
        {
            _current.Add(category, amount);
        }
    }
}
