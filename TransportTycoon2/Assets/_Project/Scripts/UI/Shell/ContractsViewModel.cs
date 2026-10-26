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
        public ContractRow(int contractId, string text, int tileX, int tileY)
        {
            ContractId = contractId;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            TileX = tileX;
            TileY = tileY;
        }

        /// <summary>
        /// Gets the contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the formatted row text.
        /// </summary>
        public string Text { get; }

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
                && string.Equals(Text, other.Text, StringComparison.Ordinal)
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
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);
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
                string delivered = CashFormat.FormatAmount(Math.Min(item.Delivered, item.Required), culture);
                string required = CashFormat.FormatAmount(item.Required, culture);
                string bonus = CashFormat.FormatAmount(item.Bonus, culture);
                string line = item.TargetKind == ContractTargetKind.Town
                    ? string.Format(
                        culture,
                        text.Get(GoalsUiKeys.ContractsRowTown),
                        item.CargoName,
                        item.TargetNumber.ToString(culture),
                        delivered,
                        required,
                        item.Deadline,
                        bonus)
                    : string.Format(
                        culture,
                        text.Get(GoalsUiKeys.ContractsRowIndustry),
                        item.CargoName,
                        text.Get(item.TargetNameKey ?? string.Empty),
                        item.TargetNumber.ToString(culture),
                        delivered,
                        required,
                        item.Deadline,
                        bonus);
                rows[index] = new ContractRow(item.ContractId, line, item.TileX, item.TileY);
            }

            return rows;
        }
    }
}
