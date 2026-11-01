#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// One entry of the active contracts box: the sentence, the progress or claim line, and the time left.
    /// </summary>
    public sealed class ActiveContractRow : IEquatable<ActiveContractRow>
    {
        /// <summary>
        /// Initializes a row. <paramref name="timeLeft"/> is empty for a completed contract.
        /// </summary>
        public ActiveContractRow(
            int contractId,
            bool completed,
            TextLine description,
            TextLine progress,
            TextLine timeLeft,
            int tileX,
            int tileY)
        {
            ContractId = contractId;
            Completed = completed;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            TimeLeft = timeLeft ?? throw new ArgumentNullException(nameof(timeLeft));
            TileX = tileX;
            TileY = tileY;
        }

        /// <summary>
        /// Gets the contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets whether the reward waits to be claimed (a click then opens the contracts window).
        /// </summary>
        public bool Completed { get; }

        /// <summary>
        /// Gets the sentence "Deliver [icon] to Town".
        /// </summary>
        public TextLine Description { get; }

        /// <summary>
        /// Gets the progress line, or the "Completed! Click to claim Reward" line.
        /// </summary>
        public TextLine Progress { get; }

        /// <summary>
        /// Gets the "Time left: 92 days" line (empty for a completed contract).
        /// </summary>
        public TextLine TimeLeft { get; }

        /// <summary>
        /// Gets the target tile column.
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the target tile row.
        /// </summary>
        public int TileY { get; }

        /// <inheritdoc />
        public bool Equals(ActiveContractRow? other)
        {
            return other is not null
                && ContractId == other.ContractId
                && Completed == other.Completed
                && Description.Equals(other.Description)
                && Progress.Equals(other.Progress)
                && TimeLeft.Equals(other.TimeLeft)
                && TileX == other.TileX
                && TileY == other.TileY;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as ActiveContractRow);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((ContractId * 397) ^ (Completed ? 1 : 0)) * 397 ^ Progress.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Turns the active contracts into the rows of the box at the top left of the map (Issue #266).
    /// </summary>
    public static class ActiveContractsViewModel
    {
        /// <summary>
        /// Builds one row per item, in input order.
        /// </summary>
        public static IReadOnlyList<ActiveContractRow> BuildRows(
            IReadOnlyList<ActiveContractItem> items,
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

            var rows = new ActiveContractRow[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                ActiveContractItem item = items[index];
                TextLine description = TextLine.Format(
                    text.Get(GoalsUiKeys.ActiveContractsDeliver),
                    TextLine.Icon(item.CargoId),
                    TextLine.Plain(item.TownName));
                TextLine progress;
                TextLine timeLeft;
                if (item.Completed)
                {
                    progress = TextLine.Plain(text.Get(GoalsUiKeys.ActiveContractsCompleted));
                    timeLeft = TextLine.Empty;
                }
                else
                {
                    progress = TextLine.Plain(
                        string.Format(
                            culture,
                            text.Get(GoalsUiKeys.ActiveContractsProgress),
                            CashFormat.FormatAmount(Math.Min(item.Delivered, item.Required), culture),
                            CashFormat.FormatAmount(item.Required, culture)));
                    timeLeft = TextLine.Plain(
                        string.Format(
                            culture,
                            text.Get(item.DaysLeft == 1 ? GoalsUiKeys.ActiveContractsTimeLeftOne : GoalsUiKeys.ActiveContractsTimeLeft),
                            item.DaysLeft.ToString(culture)));
                }

                rows[index] = new ActiveContractRow(
                    item.ContractId,
                    item.Completed,
                    description,
                    progress,
                    timeLeft,
                    item.TileX,
                    item.TileY);
            }

            return rows;
        }
    }
}
