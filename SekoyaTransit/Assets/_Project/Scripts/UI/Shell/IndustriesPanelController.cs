#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Industries panel (Issue #178, Issue #282): a collapsible rules text built from the growth data, then one card per
    /// industry with its growth level, stock, last month's production and delivery, and its progress toward the next
    /// level. Clicking a card selects the industry and centers the camera.
    /// </summary>
    public sealed class IndustriesPanelController : ShellPanelController
    {
        private readonly Foldout _rules;
        private readonly Label _rulesIntro;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<IndustryListItem>> _provider;
        private readonly ListSelection _selection;
        private readonly Action<string, int> _onRowSelected;
        private readonly Action _onSelectionChanged;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private IndustryGrowthRulesInfo? _growthRules;
        private int _lastFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the industries panel elements. <paramref name="onSelectionChanged"/> runs
        /// after a row click changed <paramref name="selection"/> (so the towns highlight can follow), then
        /// <paramref name="onRowSelected"/> receives the industry id and instance index.
        /// </summary>
        public IndustriesPanelController(
            VisualElement panel,
            Label title,
            Foldout rules,
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
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _onRowSelected = onRowSelected ?? throw new ArgumentNullException(nameof(onRowSelected));
            _onSelectionChanged = onSelectionChanged ?? throw new ArgumentNullException(nameof(onSelectionChanged));
            _cargoIcons = cargoIcons;

            _rulesIntro = new Label();
            _rulesIntro.AddToClassList("industries-rules-line");
            _rules.Add(_rulesIntro);
            _rules.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Sets the growth values the rules text shows; null hides the rules.
        /// </summary>
        public void SetGrowthRules(IndustryGrowthRulesInfo? rules)
        {
            _growthRules = rules;
            RefreshRules();
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

        /// <summary>
        /// Scrolls the selected industry row to the top of the list, when an industry is selected and listed.
        /// </summary>
        public void ScrollSelectionToTop()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                if (_selection.IsIndustrySelected(binding.IndustryId, binding.InstanceIndex))
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
            _empty.text = Text.Get(GameUiKeys.IndustriesEmpty);
            RefreshRules();
            IReadOnlyList<IndustryListItem> industries = _provider();
            int fingerprint = IndustriesViewModel.ComputeFingerprint(industries);
            if (force || fingerprint != _lastFingerprint)
            {
                _lastFingerprint = fingerprint;
                Rebuild(industries);
                RefreshSelectionHighlight();
                return;
            }

            for (int index = 0; index < industries.Count; index++)
            {
                UpdateRow(_rows[index], industries[index]);
            }

            RefreshSelectionHighlight();
        }

        private void RefreshRules()
        {
            IndustryGrowthRulesInfo? rules = _growthRules;
            if (rules == null)
            {
                _rules.style.display = DisplayStyle.None;
                return;
            }

            _rules.style.display = DisplayStyle.Flex;
            _rules.text = Text.Get(GameUiKeys.IndustriesRulesToggle);
            _rulesIntro.text = IndustriesViewModel.FormatRules(rules, Text, Culture()).ToPlainText();
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
            for (int index = 0; index < industries.Count; index++)
            {
                IndustryListItem item = industries[index];
                string industryId = item.IndustryId;
                int instanceIndex = item.InstanceIndex;
                var row = new VisualElement();
                row.AddToClassList("industries-row");

                TextLineView summary = AddLine(row, "industries-row-summary");
                TextLineView stock = AddLine(row, "industries-row-line");
                TextLineView production = AddLine(row, "industries-row-line");
                TextLineView delivered = AddLine(row, "industries-row-line");
                TextLineView progress = AddLine(row, "industries-row-line");

                EventCallback<ClickEvent> clickHandler = _ =>
                {
                    _selection.SelectIndustry(industryId, instanceIndex);
                    _onSelectionChanged();
                    _onRowSelected(industryId, instanceIndex);
                };
                row.RegisterCallback(clickHandler);

                _list.Add(row);
                var binding = new RowBinding(
                    industryId,
                    instanceIndex,
                    row,
                    summary,
                    stock,
                    production,
                    delivered,
                    progress,
                    clickHandler);
                _rows.Add(binding);
                UpdateRow(binding, item);
            }
        }

        private TextLineView AddLine(VisualElement row, string ussClass)
        {
            var line = new TextLineView(_cargoIcons, Text);
            line.AddToClassList(ussClass);
            row.Add(line);
            return line;
        }

        private void UpdateRow(RowBinding binding, IndustryListItem item)
        {
            CultureInfo culture = Culture();
            binding.Summary.SetLine(IndustriesViewModel.FormatSummary(item, Text, culture));
            binding.Stock.SetLine(IndustriesViewModel.FormatStockLine(item, Text, culture));
            binding.Production.SetLine(IndustriesViewModel.FormatProductionLine(item, Text, culture));
            binding.Delivered.SetLine(IndustriesViewModel.FormatDeliveredLine(item, Text, culture));
            TextLine progress = IndustriesViewModel.FormatProgressLine(item, Text, culture);
            binding.Progress.SetLine(progress);
            binding.Progress.style.display = progress.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
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
                TextLineView stock,
                TextLineView production,
                TextLineView delivered,
                TextLineView progress,
                EventCallback<ClickEvent> clickHandler)
            {
                IndustryId = industryId;
                InstanceIndex = instanceIndex;
                Row = row;
                Summary = summary;
                Stock = stock;
                Production = production;
                Delivered = delivered;
                Progress = progress;
                ClickHandler = clickHandler;
            }

            public string IndustryId { get; }

            public int InstanceIndex { get; }

            public VisualElement Row { get; }

            public TextLineView Summary { get; }

            public TextLineView Stock { get; }

            public TextLineView Production { get; }

            public TextLineView Delivered { get; }

            public TextLineView Progress { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }
    }
}
