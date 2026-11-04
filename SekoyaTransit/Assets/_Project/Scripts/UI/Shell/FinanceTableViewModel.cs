#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// How a finance cell is coloured (Issue #296).
    /// </summary>
    public enum FinanceCellTone
    {
        /// <summary>Zero.</summary>
        Neutral = 0,

        /// <summary>A gain (green).</summary>
        Positive = 1,

        /// <summary>A loss (red).</summary>
        Negative = 2,

        /// <summary>The figure is not available yet (muted text, no cash icon).</summary>
        Unavailable = 3
    }

    /// <summary>
    /// One value cell of the finance table: the signed amount text and its tone.
    /// </summary>
    public readonly struct FinanceCell : IEquatable<FinanceCell>
    {
        /// <summary>
        /// Initializes a cell.
        /// </summary>
        public FinanceCell(string text, FinanceCellTone tone)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Tone = tone;
        }

        /// <summary>
        /// Gets the text ("+350", "-250", "0" or "Not available yet").
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the tone.
        /// </summary>
        public FinanceCellTone Tone { get; }

        /// <summary>
        /// Gets whether the cash icon follows the amount (every cell except "not available yet").
        /// </summary>
        public bool ShowsCashIcon
        {
            get
            {
                return Tone != FinanceCellTone.Unavailable;
            }
        }

        /// <inheritdoc />
        public bool Equals(FinanceCell other)
        {
            return Tone == other.Tone && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is FinanceCell other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Tone * 397) ^ StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// One display row of the finance table: a localized label and four cells.
    /// </summary>
    public sealed class FinanceTableRow : IEquatable<FinanceTableRow>
    {
        private readonly FinanceCell[] _cells;

        /// <summary>
        /// Initializes a row.
        /// </summary>
        public FinanceTableRow(string label, IReadOnlyList<FinanceCell> cells, bool startsSection, bool isTotal)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            if (cells is null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            _cells = new FinanceCell[cells.Count];
            for (int index = 0; index < _cells.Length; index++)
            {
                _cells[index] = cells[index];
            }

            StartsSection = startsSection;
            IsTotal = isTotal;
        }

        /// <summary>
        /// Gets the localized label.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the cells, one per column.
        /// </summary>
        public IReadOnlyList<FinanceCell> Cells
        {
            get
            {
                return _cells;
            }
        }

        /// <summary>
        /// Gets whether a separator line is drawn above the row (it opens a new section).
        /// </summary>
        public bool StartsSection { get; }

        /// <summary>
        /// Gets whether the row is a profit or total row (drawn bold).
        /// </summary>
        public bool IsTotal { get; }

        /// <inheritdoc />
        public bool Equals(FinanceTableRow? other)
        {
            if (other is null
                || StartsSection != other.StartsSection
                || IsTotal != other.IsTotal
                || !string.Equals(Label, other.Label, StringComparison.Ordinal)
                || _cells.Length != other._cells.Length)
            {
                return false;
            }

            for (int index = 0; index < _cells.Length; index++)
            {
                if (!_cells[index].Equals(other._cells[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as FinanceTableRow);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Label);
        }
    }

    /// <summary>
    /// Turns a <see cref="FinanceTable"/> into display rows for the finance window (Issue #296). The lines are data: to
    /// add a line (for example construction costs later), add one entry to the line list.
    /// </summary>
    public static class FinanceTableViewModel
    {
        private static readonly FinanceLine[] _lines =
        {
            new FinanceLine(GoalsUiKeys.FinanceTruckCost, figures => -figures.TruckCost, false, false),
            new FinanceLine(GoalsUiKeys.FinanceTruckBenefit, figures => figures.TruckBenefit, false, false),
            new FinanceLine(
                GoalsUiKeys.FinanceTruckProfit,
                figures => figures.TruckBenefit - figures.TruckCost,
                false,
                true),
            new FinanceLine(GoalsUiKeys.FinanceTrainCost, figures => -figures.TrainCost, true, false),
            new FinanceLine(GoalsUiKeys.FinanceTrainBenefit, figures => figures.TrainBenefit, false, false),
            new FinanceLine(
                GoalsUiKeys.FinanceTrainProfit,
                figures => figures.TrainBenefit - figures.TrainCost,
                false,
                true),
            new FinanceLine(GoalsUiKeys.FinanceTotalCost, figures => -figures.TotalCost, true, false),
            new FinanceLine(GoalsUiKeys.FinanceTotalBenefit, figures => figures.TotalBenefit, false, false),
            new FinanceLine(
                GoalsUiKeys.FinanceTotalVehiclesProfit,
                figures => figures.TotalBenefit - figures.TotalCost,
                false,
                true),
            new FinanceLine(GoalsUiKeys.FinanceTruckBought, figures => -figures.TruckBought, true, false),
            new FinanceLine(GoalsUiKeys.FinanceTrainBought, figures => -figures.TrainBought, false, false),
            new FinanceLine(GoalsUiKeys.FinanceVehiclesSold, figures => figures.VehiclesSold, false, false),
            new FinanceLine(GoalsUiKeys.FinanceVehiclesUnlocked, figures => -figures.VehiclesUnlocked, false, false),
            new FinanceLine(GoalsUiKeys.FinanceContractProfit, figures => figures.ContractProfit, false, false),
            new FinanceLine(GoalsUiKeys.FinanceTotalProfit, figures => figures.TotalProfit, false, true)
        };

        private static readonly string[] _columnKeys =
        {
            GoalsUiKeys.FinanceColumnLastMonth,
            GoalsUiKeys.FinanceColumnCurrentMonth,
            GoalsUiKeys.FinanceColumnLastYear,
            GoalsUiKeys.FinanceColumnCurrentYear
        };

        /// <summary>
        /// Gets the number of table lines.
        /// </summary>
        public static int LineCount
        {
            get
            {
                return _lines.Length;
            }
        }

        /// <summary>
        /// Returns the four column headings.
        /// </summary>
        public static string[] BuildHeaders(ShellTextCache text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var headers = new string[_columnKeys.Length];
            for (int index = 0; index < headers.Length; index++)
            {
                headers[index] = text.Get(_columnKeys[index]);
            }

            return headers;
        }

        /// <summary>
        /// Builds one row per line, with a signed, coloured cell per column.
        /// </summary>
        public static IReadOnlyList<FinanceTableRow> BuildRows(
            FinanceTable table,
            ShellTextCache text,
            CultureInfo culture)
        {
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            var rows = new List<FinanceTableRow>(_lines.Length);
            for (int lineIndex = 0; lineIndex < _lines.Length; lineIndex++)
            {
                FinanceLine line = _lines[lineIndex];
                var cells = new FinanceCell[FinanceTable.ColumnCount];
                for (int column = 0; column < cells.Length; column++)
                {
                    cells[column] = BuildCell(table.GetColumn(column), line, text, culture);
                }

                rows.Add(new FinanceTableRow(text.Get(line.LabelKey), cells, line.StartsSection, line.IsTotal));
            }

            return rows;
        }

        private static FinanceCell BuildCell(
            FinanceColumn column,
            FinanceLine line,
            ShellTextCache text,
            CultureInfo culture)
        {
            if (!column.IsAvailable)
            {
                return new FinanceCell(text.Get(GoalsUiKeys.FinanceNotAvailable), FinanceCellTone.Unavailable);
            }

            long amount = line.Value(column.Figures);
            if (amount == 0L)
            {
                return new FinanceCell(CashFormat.FormatAmount(0L, culture), FinanceCellTone.Neutral);
            }

            string formatted = CashFormat.FormatAmount(Math.Abs(amount), culture);
            return amount > 0L
                ? new FinanceCell("+" + formatted, FinanceCellTone.Positive)
                : new FinanceCell("-" + formatted, FinanceCellTone.Negative);
        }

        private readonly struct FinanceLine
        {
            public FinanceLine(string labelKey, Func<FinanceFigures, long> value, bool startsSection, bool isTotal)
            {
                LabelKey = labelKey;
                Value = value;
                StartsSection = startsSection;
                IsTotal = isTotal;
            }

            public string LabelKey { get; }

            public Func<FinanceFigures, long> Value { get; }

            public bool StartsSection { get; }

            public bool IsTotal { get; }
        }
    }
}
