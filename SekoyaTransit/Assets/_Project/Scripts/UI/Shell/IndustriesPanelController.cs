#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Industries list panel (Issue #178): one row per industry instance; clicking a row selects it and centers the
    /// camera.
    /// </summary>
    public sealed class IndustriesPanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<IndustryListItem>> _provider;
        private readonly ListSelection _selection;
        private readonly Action<string, int> _onRowSelected;
        private readonly Action _onSelectionChanged;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private int _lastFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the industries panel elements. <paramref name="onSelectionChanged"/> runs after
        /// a row click changed <paramref name="selection"/> (so the towns highlight can follow), then
        /// <paramref name="onRowSelected"/> receives the industry id and instance index.
        /// </summary>
        public IndustriesPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<IndustryListItem>> provider,
            ListSelection selection,
            Action<string, int> onRowSelected,
            Action onSelectionChanged,
            ICargoIconSource? cargoIcons,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "industries-panel--hidden", title, GameUiKeys.IndustriesTitle, text, culture)
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
                binding.Row.EnableInClassList(
                    "industries-row--selected",
                    _selection.IsIndustrySelected(binding.IndustryId, binding.InstanceIndex));
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
            _empty.text = Text.Get(GameUiKeys.IndustriesEmpty);
            IReadOnlyList<IndustryListItem> industries = _provider();
            int fingerprint = IndustriesViewModel.ComputeFingerprint(industries);
            if (force || fingerprint != _lastFingerprint)
            {
                _lastFingerprint = fingerprint;
                Rebuild(industries);
                RefreshSelectionHighlight();
                return;
            }

            CultureInfo culture = Culture();
            for (int index = 0; index < industries.Count; index++)
            {
                _rows[index].Summary.SetLine(IndustriesViewModel.FormatRow(industries[index], Text, culture));
            }

            RefreshSelectionHighlight();
        }

        private void Rebuild(IReadOnlyList<IndustryListItem> industries)
        {
            ClearRows();
            _list.Clear();
            if (industries.Count == 0)
            {
                _empty.style.display = DisplayStyle.Flex;
                return;
            }

            _empty.style.display = DisplayStyle.None;
            CultureInfo culture = Culture();
            for (int index = 0; index < industries.Count; index++)
            {
                IndustryListItem item = industries[index];
                string industryId = item.IndustryId;
                int instanceIndex = item.InstanceIndex;
                var row = new VisualElement();
                row.AddToClassList("industries-row");

                var summary = new TextLineView(_cargoIcons);
                summary.SetLine(IndustriesViewModel.FormatRow(item, Text, culture));
                summary.AddToClassList("industries-row-summary");
                row.Add(summary);

                EventCallback<ClickEvent> clickHandler = _ =>
                {
                    _selection.SelectIndustry(industryId, instanceIndex);
                    _onSelectionChanged();
                    _onRowSelected(industryId, instanceIndex);
                };
                row.RegisterCallback(clickHandler);

                _list.Add(row);
                _rows.Add(new RowBinding(industryId, instanceIndex, row, summary, clickHandler));
            }
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
                string industryId,
                int instanceIndex,
                VisualElement row,
                TextLineView summary,
                EventCallback<ClickEvent> clickHandler)
            {
                IndustryId = industryId;
                InstanceIndex = instanceIndex;
                Row = row;
                Summary = summary;
                ClickHandler = clickHandler;
            }

            public string IndustryId { get; }

            public int InstanceIndex { get; }

            public VisualElement Row { get; }

            public TextLineView Summary { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }
    }
}
