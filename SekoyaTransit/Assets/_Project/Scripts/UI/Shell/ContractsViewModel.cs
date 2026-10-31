#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// One button of a contract row.
    /// </summary>
    public readonly struct ContractRowButton : IEquatable<ContractRowButton>
    {
        /// <summary>
        /// Initializes a button.
        /// </summary>
        public ContractRowButton(ContractUiAction action, string label, bool enabled)
        {
            Action = action;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Enabled = enabled;
        }

        /// <summary>
        /// Gets what the button does.
        /// </summary>
        public ContractUiAction Action { get; }

        /// <summary>
        /// Gets the button text.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets whether the button can be clicked.
        /// </summary>
        public bool Enabled { get; }

        /// <inheritdoc />
        public bool Equals(ContractRowButton other)
        {
            return Action == other.Action
                && string.Equals(Label, other.Label, StringComparison.Ordinal)
                && Enabled == other.Enabled;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ContractRowButton other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return ((int)Action * 397) ^ StringComparer.Ordinal.GetHashCode(Label);
        }
    }

    /// <summary>
    /// One display row of the contracts panel: the sentence, the status line, the buttons, and the tile the camera
    /// centers on when the sentence is clicked.
    /// </summary>
    public sealed class ContractRow : IEquatable<ContractRow>
    {
        /// <summary>
        /// Initializes a row. <paramref name="locked"/> greys out a proposal that cannot be accepted yet.
        /// </summary>
        public ContractRow(
            int contractId,
            ContractUiStatus status,
            bool locked,
            TextLine description,
            string detail,
            IReadOnlyList<ContractRowButton> buttons,
            int tileX,
            int tileY)
        {
            ContractId = contractId;
            Status = status;
            Locked = locked;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
            Buttons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            TileX = tileX;
            TileY = tileY;
        }

        /// <summary>
        /// Gets the contract id.
        /// </summary>
        public int ContractId { get; }

        /// <summary>
        /// Gets the status (the row color).
        /// </summary>
        public ContractUiStatus Status { get; }

        /// <summary>
        /// Gets whether the row is greyed out.
        /// </summary>
        public bool Locked { get; }

        /// <summary>
        /// Gets the sentence "Deliver 250 [icon] wood to Town in 12 months".
        /// </summary>
        public TextLine Description { get; }

        /// <summary>
        /// Gets the status line: the reward, the progress and deadline, or the result.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// Gets the buttons, left to right.
        /// </summary>
        public IReadOnlyList<ContractRowButton> Buttons { get; }

        /// <summary>
        /// Gets the target tile column.
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the target tile row.
        /// </summary>
        public int TileY { get; }

        /// <inheritdoc />
        public bool Equals(ContractRow? other)
        {
            if (other is null || ContractId != other.ContractId || Status != other.Status || Locked != other.Locked
                || !Description.Equals(other.Description)
                || !string.Equals(Detail, other.Detail, StringComparison.Ordinal)
                || TileX != other.TileX || TileY != other.TileY || Buttons.Count != other.Buttons.Count)
            {
                return false;
            }

            for (int index = 0; index < Buttons.Count; index++)
            {
                if (!Buttons[index].Equals(other.Buttons[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as ContractRow);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ContractId;
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ (Locked ? 1 : 0);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Detail);
            }
        }
    }

    /// <summary>
    /// Turns the contracts into display rows (Issues #66 and #227): the sentence with the cargo icon, the status line,
    /// and the buttons each status offers. A proposal that cannot be accepted yet is greyed out and its Accept button
    /// disabled; Decline always works.
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
                TextLine description = TextLine.Format(
                    text.Get(GoalsUiKeys.ContractsRowDeliver),
                    TextLine.Plain(CashFormat.FormatAmount(item.Required, culture)),
                    CargoStackFormat.Name(item.CargoId),
                    TextLine.Plain(item.TownName),
                    TextLine.Plain(item.DurationMonths.ToString(culture)));
                string reward = CashFormat.FormatAmount(item.Reward, culture);
                string delivered = CashFormat.FormatAmount(Math.Min(item.Delivered, item.Required), culture);
                string required = CashFormat.FormatAmount(item.Required, culture);
                string detail;
                var buttons = new List<ContractRowButton>(2);
                switch (item.Status)
                {
                    case ContractUiStatus.Proposal:
                        detail = string.Format(culture, text.Get(GoalsUiKeys.ContractsRowReward), reward);
                        buttons.Add(
                            new ContractRowButton(
                                ContractUiAction.Accept,
                                text.Get(GoalsUiKeys.ContractsAccept),
                                item.CanAccept));
                        buttons.Add(
                            new ContractRowButton(
                                ContractUiAction.Decline,
                                text.Get(GoalsUiKeys.ContractsDecline),
                                true));
                        break;
                    case ContractUiStatus.Accepted:
                        detail = string.Format(
                            culture,
                            text.Get(GoalsUiKeys.ContractsRowAccepted),
                            delivered,
                            required,
                            item.DateText);
                        buttons.Add(
                            new ContractRowButton(ContractUiAction.Cancel, text.Get(GoalsUiKeys.ContractsCancel), true));
                        break;
                    case ContractUiStatus.Completed:
                        detail = string.Format(
                            culture,
                            text.Get(GoalsUiKeys.ContractsRowCompleted),
                            delivered,
                            required,
                            item.DateText);
                        buttons.Add(
                            new ContractRowButton(
                                ContractUiAction.ClaimReward,
                                string.Format(culture, text.Get(GoalsUiKeys.ContractsClaim), reward),
                                true));
                        break;
                    case ContractUiStatus.Failed:
                        detail = string.Format(
                            culture,
                            text.Get(GoalsUiKeys.ContractsRowFailed),
                            delivered,
                            required,
                            item.DateText);
                        buttons.Add(
                            new ContractRowButton(ContractUiAction.Remove, text.Get(GoalsUiKeys.ContractsRemove), true));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(items), item.Status, "Unknown contract status.");
                }

                bool locked = item.Status == ContractUiStatus.Proposal && !item.CanAccept;
                rows[index] = new ContractRow(
                    item.ContractId,
                    item.Status,
                    locked,
                    description,
                    detail,
                    buttons,
                    item.TileX,
                    item.TileY);
            }

            return rows;
        }

        /// <summary>
        /// Returns the header line with the contracts in the list against the list maximum ("Available: 4/6").
        /// </summary>
        public static string FormatAvailable(ContractsPanelData data, ShellTextCache text, CultureInfo culture)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return string.Format(
                culture,
                text.Get(GoalsUiKeys.ContractsAvailable),
                data.Items.Count.ToString(culture),
                data.MaxContracts.ToString(culture));
        }

        /// <summary>
        /// Returns the header line with the accepted count against the accepted maximum ("In progress: 3/3").
        /// </summary>
        public static string FormatInProgress(ContractsPanelData data, ShellTextCache text, CultureInfo culture)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return string.Format(
                culture,
                text.Get(GoalsUiKeys.ContractsInProgress),
                data.AcceptedCount.ToString(culture),
                data.MaxAccepted.ToString(culture));
        }

        /// <summary>
        /// Returns the line under the list: when the next contract appears, or that the list is full.
        /// </summary>
        public static string FormatFooter(ContractsPanelData data, ShellTextCache text)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return text.Get(data.IsFull ? GoalsUiKeys.ContractsFooterFull : GoalsUiKeys.ContractsFooterNext);
        }
    }
}
