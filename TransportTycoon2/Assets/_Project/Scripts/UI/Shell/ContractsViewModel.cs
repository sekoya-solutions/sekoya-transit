#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// One display row of the contracts panel; the tile is where the camera centers when the row is clicked.
    /// </summary>
    public readonly struct ContractRow : IEquatable<ContractRow>
    {
        /// <summary>
        /// Initializes a row.
        /// </summary>
        public ContractRow(int contractId, TextLine line, int tileX, int tileY)
        {
            ContractId = contractId;
            Line = line ?? throw new ArgumentNullException(nameof(line));
            TileX = tileX;
            TileY = tileY;
        }

        /// <summary>
        /// Gets the contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the formatted row (text and the cargo icon).
        /// </summary>
        public TextLine Line { get; }

        /// <summary>
        /// Gets the target tile column.
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the target tile row.
        /// </summary>
        public int TileY { get; }

        /// <inheritdoc />
        public bool Equals(ContractRow other)
        {
            return ContractId == other.ContractId
                && Line.Equals(other.Line)
                && TileX == other.TileX
                && TileY == other.TileY;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ContractRow other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ContractId;
                hash = (hash * 397) ^ (Line?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ TileX;
                return (hash * 397) ^ TileY;
            }
        }
    }

    /// <summary>
    /// Turns open-contract items into display rows (Issue #66): cargo, target, delivered / required (delivered is
    /// capped at the amount), deadline, and bonus. An empty list means the panel shows its empty state.
    /// </summary>
    public static class ContractsViewModel
    {
        /// <summary>
        /// Builds one row per contract, in input order.
        /// </summary>
        public static IReadOnlyList<ContractRow> BuildRows(
            IReadOnlyList<ContractListItem> items,
            ShellTextCache text,
            CultureInfo culture)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            var rows = new ContractRow[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                ContractListItem item = items[index];
                TextLine cargo = CargoStackFormat.Name(item.CargoId);
                TextLine target = TextLine.Plain(item.TargetNumber.ToString(culture));
                TextLine delivered = TextLine.Plain(
                    CashFormat.FormatAmount(Math.Min(item.Delivered, item.Required), culture));
                TextLine required = TextLine.Plain(CashFormat.FormatAmount(item.Required, culture));
                TextLine deadline = TextLine.Plain(item.Deadline);
                TextLine bonus = TextLine.Plain(CashFormat.FormatAmount(item.Bonus, culture));
                TextLine line = item.TargetKind == ContractTargetKind.Town
                    ? TextLine.Format(
                        text.Get(GoalsUiKeys.ContractsRowTown),
                        cargo,
                        target,
                        delivered,
                        required,
                        deadline,
                        bonus)
                    : TextLine.Format(
                        text.Get(GoalsUiKeys.ContractsRowIndustry),
                        cargo,
                        TextLine.Plain(text.Get(item.TargetNameKey ?? string.Empty)),
                        target,
                        delivered,
                        required,
                        deadline,
                        bonus);
                rows[index] = new ContractRow(item.ContractId, line, item.TileX, item.TileY);
            }

            return rows;
        }
    }
}
