#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// One finance category total for the finance panel (Issue #66). Presentation builds these from Core.
    /// </summary>
    public readonly struct FinanceRecapLine : IEquatable<FinanceRecapLine>
    {
        /// <summary>
        /// Initializes a line.
        /// </summary>
        public FinanceRecapLine(string labelKey, long amount, bool isIncome)
        {
            if (string.IsNullOrWhiteSpace(labelKey))
            {
                throw new ArgumentException("Label key cannot be null or whitespace.", nameof(labelKey));
            }

            if (amount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
            }

            LabelKey = labelKey;
            Amount = amount;
            IsIncome = isIncome;
        }

        /// <summary>
        /// Gets the localization key of the category name.
        /// </summary>
        public string LabelKey { get; }

        /// <summary>
        /// Gets the recorded total.
        /// </summary>
        public long Amount { get; }

        /// <summary>
        /// Gets whether the line is income (otherwise spending).
        /// </summary>
        public bool IsIncome { get; }

        /// <inheritdoc />
        public bool Equals(FinanceRecapLine other)
        {
            return string.Equals(LabelKey, other.LabelKey, StringComparison.Ordinal)
                && Amount == other.Amount
                && IsIncome == other.IsIncome;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is FinanceRecapLine other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(LabelKey ?? string.Empty);
                hash = (hash * 397) ^ Amount.GetHashCode();
                return (hash * 397) ^ IsIncome.GetHashCode();
            }
        }
    }
}
