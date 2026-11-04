#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Company money by category and vehicle mode for the current and the previous calendar year (Issue #66) and for the
    /// current and the previous calendar month (Issue #296). Every cash change of <see cref="GeneratedWorld"/> is
    /// recorded here, so the change in cash over any period equals total income minus total spending for that period.
    /// </summary>
    public sealed class FinanceLedger
    {
        private readonly FinanceYearTotals _current = new FinanceYearTotals();
        private readonly FinanceYearTotals _previous = new FinanceYearTotals();
        private readonly FinanceYearTotals _currentMonth = new FinanceYearTotals();
        private readonly FinanceYearTotals _previousMonth = new FinanceYearTotals();

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
        /// Gets whether the previous year was observed (the year rolled over at least once), so its totals are real
        /// figures and not "not available yet".
        /// </summary>
        public bool HasPreviousYear { get; private set; }

        /// <summary>
        /// Gets the month the monthly totals belong to as <c>year * 12 + month</c>, or 0 before the first tick
        /// observes the calendar.
        /// </summary>
        public int MonthIndex { get; private set; }

        /// <summary>
        /// Gets the totals of the current calendar month.
        /// </summary>
        public FinanceYearTotals CurrentMonth
        {
            get
            {
                return _currentMonth;
            }
        }

        /// <summary>
        /// Gets the totals of the previous calendar month (empty when there was none).
        /// </summary>
        public FinanceYearTotals PreviousMonth
        {
            get
            {
                return _previousMonth;
            }
        }

        /// <summary>
        /// Gets whether the current month was observed from its first day, so it will close as a full month.
        /// </summary>
        public bool HasFullCurrentMonth { get; private set; }

        /// <summary>
        /// Gets whether the previous month was a full month of data (Issue #296), so its totals are real figures.
        /// </summary>
        public bool HasPreviousMonth { get; private set; }

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
            HasPreviousYear = true;
            return true;
        }

        /// <summary>
        /// Moves to the month of <paramref name="year"/>/<paramref name="month"/>: when it is a later month than the
        /// current one the current month totals become the previous month totals and reset. The first observation only
        /// records the month; it counts as a full month only when it starts on <paramref name="day"/> 1 (a save from
        /// before format 32 starts mid-month). Returns whether the totals rolled over.
        /// </summary>
        public bool AdvanceToMonth(int year, int month, int day)
        {
            if (year < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "The finance year cannot be negative.");
            }

            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "The finance month must be 1 to 12.");
            }

            int index = checked((year * 12) + month);
            if (index == MonthIndex)
            {
                return false;
            }

            if (MonthIndex == 0)
            {
                MonthIndex = index;
                HasFullCurrentMonth = day == 1;
                return false;
            }

            if (index < MonthIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "The finance month cannot go backwards.");
            }

            if (index == MonthIndex + 1)
            {
                _previousMonth.CopyFrom(_currentMonth);
                HasPreviousMonth = HasFullCurrentMonth;
            }
            else
            {
                _previousMonth.Clear();
                HasPreviousMonth = false;
            }

            _currentMonth.Clear();
            HasFullCurrentMonth = true;
            MonthIndex = index;
            return true;
        }

        /// <summary>
        /// Replaces the yearly ledger state when restoring a save from before format 32: no monthly data, and the
        /// previous year counts as observed when it holds money.
        /// </summary>
        public void Restore(int year, FinanceYearTotals current, FinanceYearTotals previous)
        {
#pragma warning disable CA1510
            if (previous == null)
            {
                throw new ArgumentNullException(nameof(previous));
            }
#pragma warning restore CA1510

            Restore(
                year,
                current,
                previous,
                !previous.IsEmpty,
                0,
                new FinanceYearTotals(),
                new FinanceYearTotals(),
                false,
                false);
        }

        /// <summary>
        /// Replaces the whole ledger state when restoring a save.
        /// </summary>
        public void Restore(
            int year,
            FinanceYearTotals current,
            FinanceYearTotals previous,
            bool hasPreviousYear,
            int monthIndex,
            FinanceYearTotals currentMonth,
            FinanceYearTotals previousMonth,
            bool hasFullCurrentMonth,
            bool hasPreviousMonth)
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

            if (currentMonth == null)
            {
                throw new ArgumentNullException(nameof(currentMonth));
            }

            if (previousMonth == null)
            {
                throw new ArgumentNullException(nameof(previousMonth));
            }
#pragma warning restore CA1510

            if (year < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "The finance year cannot be negative.");
            }

            if (monthIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monthIndex), monthIndex, "The finance month cannot be negative.");
            }

            Year = year;
            _current.CopyFrom(current);
            _previous.CopyFrom(previous);
            HasPreviousYear = hasPreviousYear;
            MonthIndex = monthIndex;
            _currentMonth.CopyFrom(currentMonth);
            _previousMonth.CopyFrom(previousMonth);
            HasFullCurrentMonth = hasFullCurrentMonth;
            HasPreviousMonth = hasPreviousMonth;
        }

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public FinanceLedger Copy()
        {
            var copy = new FinanceLedger();
            copy.Restore(
                Year,
                _current,
                _previous,
                HasPreviousYear,
                MonthIndex,
                _currentMonth,
                _previousMonth,
                HasFullCurrentMonth,
                HasPreviousMonth);
            return copy;
        }

        internal void Record(FinanceCategory category, long amount, FinanceMode mode)
        {
            _current.Add(category, mode, amount);
            _currentMonth.Add(category, mode, amount);
        }
    }
}
