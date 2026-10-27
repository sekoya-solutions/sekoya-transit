#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Towns list panel (Issue #178): one row per town; clicking a row selects the town and centers the camera.
    /// </summary>
    public sealed class TownsPanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<TownListItem>> _provider;
        private readonly ListSelection _selection;
        private readonly Action<int> _onRowSelected;
        private readonly Action _onSelectionChanged;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private int _lastFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the towns panel elements. <paramref name="onSelectionChanged"/> runs after a
        /// row click changed <paramref name="selection"/> (so the industries highlight can follow), then
        /// <paramref name="onRowSelected"/> receives the town id.
        /// </summary>
        public TownsPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<TownListItem>> provider,
            ListSelection selection,
            Action<int> onRowSelected,
            Action onSelectionChanged,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "towns-panel--hidden", title, GameUiKeys.TownsTitle, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _onRowSelected = onRowSelected ?? throw new ArgumentNullException(nameof(onRowSelected));
            _onSelectionChanged = onSelectionChanged ?? throw new ArgumentNullException(nameof(onSelectionChanged));
        }

        /// <summary>
        /// Adds or removes the selected modifier on each row from the current selection.
        /// </summary>
        public void RefreshSelectionHighlight()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                binding.Row.EnableInClassList("towns-row--selected", _selection.IsTownSelected(binding.TownId));
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            ClearRows();
            _lastFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            _empty.text = Text.Get(GameUiKeys.TownsEmpty);
            IReadOnlyList<TownListItem> towns = _provider();
            int fingerprint = TownsViewModel.ComputeFingerprint(towns);
            if (force || fingerprint != _lastFingerprint)
            {
                _lastFingerprint = fingerprint;
                Rebuild(towns);
                return;
            }

            for (int index = 0; index < towns.Count; index++)
            {
                UpdateRow(_rows[index], towns[index]);
            }

            RefreshSelectionHighlight();
        }

        private void Rebuild(IReadOnlyList<TownListItem> towns)
        {
            ClearRows();
            _list.Clear();
            if (towns.Count == 0)
            {
                _empty.style.display = DisplayStyle.Flex;
                return;
            }

            _empty.style.display = DisplayStyle.None;
            CultureInfo culture = Culture();
            for (int index = 0; index < towns.Count; index++)
            {
                TownListItem item = towns[index];
                int townId = item.TownId;
                var row = new VisualElement();
                row.AddToClassList("towns-row");

                var summary = new Label(TownsViewModel.FormatSummary(item, Text, culture));
                summary.AddToClassList("towns-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                var needLabels = new List<Label>();
                IReadOnlyList<TownNeedListLine> needs = item.Needs;
                for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                {
                    var needLabel = new Label(TownsViewModel.FormatNeedLine(needs[needIndex], Text, culture));
                    needLabel.AddToClassList("towns-row-line");
                    needLabel.pickingMode = PickingMode.Ignore;
                    row.Add(needLabel);
                    needLabels.Add(needLabel);
                }

                var passengerLabel = new Label(TownsViewModel.FormatPassengerLine(item, Text, culture));
                passengerLabel.AddToClassList("towns-row-line");
                passengerLabel.pickingMode = PickingMode.Ignore;
                row.Add(passengerLabel);

                var mailLabel = new Label(TownsViewModel.FormatMailLine(item, Text, culture));
                mailLabel.AddToClassList("towns-row-line");
                mailLabel.pickingMode = PickingMode.Ignore;
                row.Add(mailLabel);

                EventCallback<ClickEvent> clickHandler = _ =>
                {
                    _selection.SelectTown(townId);
                    _onSelectionChanged();
                    _onRowSelected(townId);
                };
                row.RegisterCallback(clickHandler);
                row.EnableInClassList("towns-row--selected", _selection.IsTownSelected(townId));

                _list.Add(row);
                _rows.Add(new RowBinding(townId, row, summary, needLabels, passengerLabel, mailLabel, clickHandler));
            }
        }

        private void UpdateRow(RowBinding binding, TownListItem item)
        {
            CultureInfo culture = Culture();
            binding.Summary.text = TownsViewModel.FormatSummary(item, Text, culture);
            IReadOnlyList<TownNeedListLine> needs = item.Needs;
            for (int needIndex = 0; needIndex < binding.NeedLabels.Count; needIndex++)
            {
                binding.NeedLabels[needIndex].text = TownsViewModel.FormatNeedLine(needs[needIndex], Text, culture);
            }

            binding.PassengerLabel.text = TownsViewModel.FormatPassengerLine(item, Text, culture);
            binding.MailLabel.text = TownsViewModel.FormatMailLine(item, Text, culture);
        }

        private void ClearRows()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                binding.Row.UnregisterCallback(binding.ClickHandler);
            }

            _rows.Clear();
        }

        private sealed class RowBinding
        {
            public RowBinding(
                int townId,
                VisualElement row,
                Label summary,
                List<Label> needLabels,
                Label passengerLabel,
                Label mailLabel,
                EventCallback<ClickEvent> clickHandler)
            {
                TownId = townId;
                Row = row;
                Summary = summary;
                NeedLabels = needLabels;
                PassengerLabel = passengerLabel;
                MailLabel = mailLabel;
                ClickHandler = clickHandler;
            }

            public int TownId { get; }

            public VisualElement Row { get; }

            public Label Summary { get; }

            public IReadOnlyList<Label> NeedLabels { get; }

            public Label PassengerLabel { get; }

            public Label MailLabel { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }
    }
}
