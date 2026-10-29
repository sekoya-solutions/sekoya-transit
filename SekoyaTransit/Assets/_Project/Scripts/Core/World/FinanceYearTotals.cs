#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Money recorded per <see cref="FinanceCategory"/> over one calendar year (Issue #66).
    /// </summary>
    public sealed class FinanceYearTotals
    {
        private readonly long[] _amounts;

        /// <summary>
        /// Creates empty totals.
        /// </summary>
        public FinanceYearTotals()
        {
            _amounts = new long[FinanceCategories.Count];
        }

        /// <summary>
        /// Creates totals from one non-negative amount per category, in <see cref="FinanceCategory"/> order.
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

            _amounts = new long[FinanceCategories.Count];
            for (int index = 0; index < amounts.Length; index++)
            {
                if (amounts[index] < 0L)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(amounts),
                        amounts[index],
                        "A finance amount cannot be negative.");
                }

                _amounts[index] = amounts[index];
            }
        }

        /// <summary>
        /// Gets the total recorded for a category.
        /// </summary>
        public long Get(FinanceCategory category)
        {
            return _amounts[IndexOf(category)];
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
        /// Returns a copy of the amounts in <see cref="FinanceCategory"/> order.
        /// </summary>
        public long[] ToArray()
        {
            return (long[])_amounts.Clone();
        }

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public FinanceYearTotals Copy()
        {
            return new FinanceYearTotals(_amounts);
        }

        internal void Add(FinanceCategory category, long amount)
        {
            int index = IndexOf(category);
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
            for (int index = 0; index < _amounts.Length; index++)
            {
                if (FinanceCategories.IsIncome((FinanceCategory)index) == income)
                {
                    total = checked(total + _amounts[index]);
                }
            }

            return total;
        }

        private static int IndexOf(FinanceCategory category)
        {
            if (!FinanceCategories.IsDefined(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown finance category.");
            }

            return (int)category;
        }
    }
}
