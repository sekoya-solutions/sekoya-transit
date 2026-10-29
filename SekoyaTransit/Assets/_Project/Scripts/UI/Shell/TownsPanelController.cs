#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
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
        private readonly ICargoIconSource? _cargoIcons;
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
            ICargoIconSource? cargoIcons,
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
            _cargoIcons = cargoIcons;
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

        /// <summary>
        /// Scrolls the selected town row to the top of the list, when a town is selected and listed.
        /// </summary>
        public void ScrollSelectionToTop()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                if (_selection.IsTownSelected(binding.TownId))
                {
                    ListScroll.ScrollRowToTop(_list, binding.Row);
                    return;
                }
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

                var needLabels = new List<TextLineView>();
                IReadOnlyList<TownNeedListLine> needs = item.Needs;
                for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                {
                    var needLabel = new TextLineView(_cargoIcons);
                    needLabel.SetLine(TownsViewModel.FormatNeedLine(needs[needIndex], Text, culture));
                    needLabel.AddToClassList("towns-row-line");
                    row.Add(needLabel);
                    needLabels.Add(needLabel);
                }

                var passengerLabel = new TextLineView(_cargoIcons);
                passengerLabel.SetLine(TownsViewModel.FormatPassengerLine(item, Text, culture));
                passengerLabel.AddToClassList("towns-row-line");
                row.Add(passengerLabel);

                var mailLabel = new TextLineView(_cargoIcons);
                mailLabel.SetLine(TownsViewModel.FormatMailLine(item, Text, culture));
                mailLabel.AddToClassList("towns-row-line");
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
                binding.NeedLabels[needIndex].SetLine(TownsViewModel.FormatNeedLine(needs[needIndex], Text, culture));
            }

            binding.PassengerLabel.SetLine(TownsViewModel.FormatPassengerLine(item, Text, culture));
            binding.MailLabel.SetLine(TownsViewModel.FormatMailLine(item, Text, culture));
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
                List<TextLineView> needLabels,
                TextLineView passengerLabel,
                TextLineView mailLabel,
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

            public IReadOnlyList<TextLineView> NeedLabels { get; }

            public TextLineView PassengerLabel { get; }

            public TextLineView MailLabel { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }
    }
}
