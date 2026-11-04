#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Money recorded per <see cref="FinanceCategory"/> and <see cref="FinanceMode"/> over one period: a calendar year
    /// (Issue #66) or a calendar month (Issue #296).
    /// </summary>
    public sealed class FinanceYearTotals
    {
        private readonly long[] _amounts;

        /// <summary>
        /// Creates empty totals.
        /// </summary>
        public FinanceYearTotals()
        {
            _amounts = new long[DetailedLength];
        }

        /// <summary>
        /// Creates totals from one non-negative amount per category, in <see cref="FinanceCategory"/> order. The amounts
        /// count as <see cref="FinanceMode.General"/>.
        /// </summary>
        public FinanceYearTotals(long[] amounts)
        {
#pragma warning disable CA1510
            if (amounts == null)
            {
                throw new ArgumentNullException(nameof(amounts));
            }
#pragma warning restore CA1510

            if (amounts.Length != FinanceCategories.Count)
            {
                throw new ArgumentException(
                    $"Expected {FinanceCategories.Count} finance amounts but got {amounts.Length}.",
                    nameof(amounts));
            }

            _amounts = new long[DetailedLength];
            for (int index = 0; index < amounts.Length; index++)
            {
                if (amounts[index] < 0L)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(amounts),
                        amounts[index],
                        "A finance amount cannot be negative.");
                }

                _amounts[IndexOf((FinanceCategory)index, FinanceMode.General)] = amounts[index];
            }
        }

        /// <summary>
        /// Gets the number of values of the detailed form (one per category and mode).
        /// </summary>
        public static int DetailedLength
        {
            get
            {
                return FinanceCategories.Count * FinanceCategories.ModeCount;
            }
        }

        /// <summary>
        /// Gets the sum of all income categories.
        /// </summary>
        public long TotalIncome
        {
            get
            {
                return Sum(income: true);
            }
        }

        /// <summary>
        /// Gets the sum of all spending categories.
        /// </summary>
        public long TotalSpending
        {
            get
            {
                return Sum(income: false);
            }
        }

        /// <summary>
        /// Gets total income minus total spending.
        /// </summary>
        public long Net
        {
            get
            {
                return checked(TotalIncome - TotalSpending);
            }
        }

        /// <summary>
        /// Gets whether every category is zero.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                for (int index = 0; index < _amounts.Length; index++)
                {
                    if (_amounts[index] != 0L)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Creates totals from one non-negative amount per category and mode, category-major (see
        /// <see cref="ToDetailedArray"/>).
        /// </summary>
        public static FinanceYearTotals FromDetailed(long[] amounts)
        {
#pragma warning disable CA1510
            if (amounts == null)
            {
                throw new ArgumentNullException(nameof(amounts));
            }
#pragma warning restore CA1510

            if (amounts.Length != DetailedLength)
            {
                throw new ArgumentException(
                    $"Expected {DetailedLength} finance amounts but got {amounts.Length}.",
                    nameof(amounts));
            }

            var totals = new FinanceYearTotals();
            for (int index = 0; index < amounts.Length; index++)
            {
                if (amounts[index] < 0L)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(amounts),
                        amounts[index],
                        "A finance amount cannot be negative.");
                }

                totals._amounts[index] = amounts[index];
            }

            return totals;
        }

        /// <summary>
        /// Gets the total recorded for a category over all modes.
        /// </summary>
        public long Get(FinanceCategory category)
        {
            long total = 0L;
            for (int mode = 0; mode < FinanceCategories.ModeCount; mode++)
            {
                total = checked(total + _amounts[IndexOf(category, (FinanceMode)mode)]);
            }

            return total;
        }

        /// <summary>
        /// Gets the total recorded for a category and mode.
        /// </summary>
        public long Get(FinanceCategory category, FinanceMode mode)
        {
            return _amounts[IndexOf(category, mode)];
        }

        /// <summary>
        /// Returns the amounts in <see cref="FinanceCategory"/> order, each summed over all modes.
        /// </summary>
        public long[] ToArray()
        {
            var amounts = new long[FinanceCategories.Count];
            for (int index = 0; index < amounts.Length; index++)
            {
                amounts[index] = Get((FinanceCategory)index);
            }

            return amounts;
        }

        /// <summary>
        /// Returns the amounts per category and mode, category-major: index = category * mode count + mode.
        /// </summary>
        public long[] ToDetailedArray()
        {
            return (long[])_amounts.Clone();
        }

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public FinanceYearTotals Copy()
        {
            return FromDetailed(_amounts);
        }

        internal void Add(FinanceCategory category, FinanceMode mode, long amount)
        {
            int index = IndexOf(category, mode);
            _amounts[index] = checked(_amounts[index] + amount);
        }

        internal void Clear()
        {
            Array.Clear(_amounts, 0, _amounts.Length);
        }

        internal void CopyFrom(FinanceYearTotals other)
        {
            Array.Copy(other._amounts, _amounts, _amounts.Length);
        }

        private long Sum(bool income)
        {
            long total = 0L;
            for (int category = 0; category < FinanceCategories.Count; category++)
            {
                if (FinanceCategories.IsIncome((FinanceCategory)category) == income)
                {
                    total = checked(total + Get((FinanceCategory)category));
                }
            }

            return total;
        }

        private static int IndexOf(FinanceCategory category, FinanceMode mode)
        {
            if (!FinanceCategories.IsDefined(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown finance category.");
            }

            if (!FinanceCategories.IsModeDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown finance mode.");
            }

            return ((int)category * FinanceCategories.ModeCount) + (int)mode;
        }
    }
}
