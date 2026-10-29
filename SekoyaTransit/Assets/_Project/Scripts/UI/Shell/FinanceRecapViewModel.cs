#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Kind of one finance panel row.
    /// </summary>
    public enum FinanceRecapRowKind
    {
        /// <summary>Year heading ("This year (1900)").</summary>
        Heading = 0,

        /// <summary>One category line.</summary>
        Line = 1,

        /// <summary>Total of all income lines.</summary>
        TotalIncome = 2,

        /// <summary>Total of all spending lines.</summary>
        TotalSpending = 3,

        /// <summary>Income minus spending.</summary>
        NetResult = 4
    }

    /// <summary>
    /// One display row of the finance panel: a localized label and a formatted amount.
    /// </summary>
    public readonly struct FinanceRecapRow : IEquatable<FinanceRecapRow>
    {
        /// <summary>
        /// Initializes a row.
        /// </summary>
        public FinanceRecapRow(FinanceRecapRowKind kind, string label, string value)
        {
            Kind = kind;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets the row kind.
        /// </summary>
        public FinanceRecapRowKind Kind { get; }

        /// <summary>
        /// Gets the localized label.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the formatted amount (empty for headings).
        /// </summary>
        public string Value { get; }

        /// <inheritdoc />
        public bool Equals(FinanceRecapRow other)
        {
            return Kind == other.Kind
                && string.Equals(Label, other.Label, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is FinanceRecapRow other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Label ?? string.Empty);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// Turns a <see cref="FinanceRecap"/> into display rows for the finance panel (Issue #66): per year a heading,
    /// income lines, total income, spending lines, total spending, and the net result.
    /// </summary>
    public static class FinanceRecapViewModel
    {
        /// <summary>
        /// Builds the rows for this year followed by last year.
        /// </summary>
        public static IReadOnlyList<FinanceRecapRow> BuildRows(
            FinanceRecap recap,
            ShellTextCache text,
            CultureInfo culture)
        {
            if (recap is null)
            {
                throw new ArgumentNullException(nameof(recap));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            var rows = new List<FinanceRecapRow>();
            AppendYear(rows, recap.Current, GoalsUiKeys.FinanceHeadingCurrent, text, culture);
            AppendYear(rows, recap.Previous, GoalsUiKeys.FinanceHeadingPrevious, text, culture);
            return rows;
        }

        private static void AppendYear(
            List<FinanceRecapRow> rows,
            FinanceYearRecap year,
            string headingKey,
            ShellTextCache text,
            CultureInfo culture)
        {
            string yearText = year.Year == 0 ? "-" : year.Year.ToString(CultureInfo.InvariantCulture);
            rows.Add(
                new FinanceRecapRow(
                    FinanceRecapRowKind.Heading,
                    string.Format(culture, text.Get(headingKey), yearText),
                    string.Empty));
            AppendLines(rows, year, income: true, text, culture);
            rows.Add(
                new FinanceRecapRow(
                    FinanceRecapRowKind.TotalIncome,
                    text.Get(GoalsUiKeys.FinanceTotalIncome),
                    CashFormat.FormatAmount(year.TotalIncome, culture)));
            AppendLines(rows, year, income: false, text, culture);
            rows.Add(
                new FinanceRecapRow(
                    FinanceRecapRowKind.TotalSpending,
                    text.Get(GoalsUiKeys.FinanceTotalSpending),
                    CashFormat.FormatAmount(year.TotalSpending, culture)));
            rows.Add(
                new FinanceRecapRow(
                    FinanceRecapRowKind.NetResult,
                    text.Get(GoalsUiKeys.FinanceNetResult),
                    CashFormat.FormatAmount(year.Net, culture)));
        }

        private static void AppendLines(
            List<FinanceRecapRow> rows,
            FinanceYearRecap year,
            bool income,
            ShellTextCache text,
            CultureInfo culture)
        {
            for (int index = 0; index < year.Lines.Count; index++)
            {
                FinanceRecapLine line = year.Lines[index];
                if (line.IsIncome != income)
                {
                    continue;
                }

                rows.Add(
                    new FinanceRecapRow(
                        FinanceRecapRowKind.Line,
                        text.Get(line.LabelKey),
                        CashFormat.FormatAmount(line.Amount, culture)));
            }
        }
    }
}
