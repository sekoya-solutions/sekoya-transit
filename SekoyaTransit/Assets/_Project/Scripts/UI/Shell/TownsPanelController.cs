#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Towns panel (Issue #178, Issue #242): a collapsible rules text built from the growth data, then one card per
    /// town with its population and last month's growth, its growth level, its passengers and mail, and its needs
    /// grouped by level and colored by last month's supply. Clicking a card selects the town and centers the camera.
    /// </summary>
    public sealed class TownsPanelController : ShellPanelController
    {
        private readonly Foldout _rules;
        private readonly Label _rulesIntro;
        private readonly TextLineView _rulesPartlyMet;
        private readonly TextLineView _rulesFullyMet;
        private readonly TextLineView _rulesAllMet;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<TownListItem>> _provider;
        private readonly ListSelection _selection;
        private readonly Action<int> _onRowSelected;
        private readonly Action _onSelectionChanged;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private TownGrowthRulesInfo? _growthRules;
        private int _lastFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the towns panel elements. <paramref name="onSelectionChanged"/> runs after a
        /// row click changed <paramref name="selection"/> (so the industries highlight can follow), then
        /// <paramref name="onRowSelected"/> receives the town id.
        /// </summary>
        public TownsPanelController(
            VisualElement panel,
            Label title,
            Foldout rules,
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
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _onRowSelected = onRowSelected ?? throw new ArgumentNullException(nameof(onRowSelected));
            _onSelectionChanged = onSelectionChanged ?? throw new ArgumentNullException(nameof(onSelectionChanged));
            _cargoIcons = cargoIcons;

            _rulesIntro = new Label();
            _rulesIntro.AddToClassList("towns-rules-line");
            _rulesPartlyMet = CreateRulesLine();
            _rulesFullyMet = CreateRulesLine();
            _rulesAllMet = CreateRulesLine();
            _rules.Add(_rulesIntro);
            _rules.Add(_rulesPartlyMet);
            _rules.Add(_rulesFullyMet);
            _rules.Add(_rulesAllMet);
            _rules.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Sets the growth values the rules text shows; null hides the rules.
        /// </summary>
        public void SetGrowthRules(TownGrowthRulesInfo? rules)
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
            RefreshRules();
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

        private TextLineView CreateRulesLine()
        {
            var line = new TextLineView(_cargoIcons, Text);
            line.AddToClassList("towns-rules-line");
            return line;
        }

        private void RefreshRules()
        {
            TownGrowthRulesInfo? rules = _growthRules;
            if (rules == null)
            {
                _rules.style.display = DisplayStyle.None;
                return;
            }

            CultureInfo culture = Culture();
            _rules.style.display = DisplayStyle.Flex;
            _rules.text = Text.Get(GameUiKeys.TownsRulesToggle);
            _rulesIntro.text = Text.Get(GameUiKeys.TownsRulesIntro);
            _rulesPartlyMet.SetLine(TownsViewModel.FormatPartlyMetRule(rules, Text, culture));
            _rulesFullyMet.SetLine(TownsViewModel.FormatFullyMetRule(rules, Text, culture));
            _rulesAllMet.SetLine(TownsViewModel.FormatAllMetRule(rules, Text, culture));
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
            for (int index = 0; index < towns.Count; index++)
            {
                TownListItem item = towns[index];
                int townId = item.TownId;
                var row = new VisualElement();
                row.AddToClassList("towns-row");

                TextLineView summary = AddLine(row, "towns-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                TextLineView growthLevel = AddLine(row, "towns-row-line");
                TextLineView passengerLabel = AddLine(row, "towns-row-line");
                TextLineView mailLabel = AddLine(row, "towns-row-line");

                var needLabels = new List<TextLineView>();
                var levelHeaders = new List<Label>();
                var notAvailableLabels = new List<Label>();
                for (int level = TownNeedListLine.MinLevel; level <= TownNeedListLine.MaxLevel; level++)
                {
                    var header = new Label();
                    header.AddToClassList("towns-row-header");
                    row.Add(header);
                    levelHeaders.Add(header);

                    IReadOnlyList<TownNeedListLine> needs = item.Needs;
                    bool any = false;
                    for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                    {
                        if (needs[needIndex].Level == level)
                        {
                            needLabels.Add(AddLine(row, "towns-row-need"));
                            any = true;
                        }
                    }

                    if (!any)
                    {
                        var notAvailable = new Label();
                        notAvailable.AddToClassList("towns-row-need");
                        row.Add(notAvailable);
                        notAvailableLabels.Add(notAvailable);
                    }
                }

                EventCallback<ClickEvent> clickHandler = _ =>
                {
                    _selection.SelectTown(townId);
                    _onSelectionChanged();
                    _onRowSelected(townId);
                };
                row.RegisterCallback(clickHandler);
                row.EnableInClassList("towns-row--selected", _selection.IsTownSelected(townId));

                _list.Add(row);
                var binding = new RowBinding(
                    townId,
                    row,
                    summary,
                    growthLevel,
                    levelHeaders,
                    needLabels,
                    notAvailableLabels,
                    passengerLabel,
                    mailLabel,
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

        private void UpdateRow(RowBinding binding, TownListItem item)
        {
            CultureInfo culture = Culture();
            binding.Summary.SetLine(TownsViewModel.FormatSummary(item, Text, culture));
            binding.GrowthLevel.SetLine(TownsViewModel.FormatGrowthLevel(item, Text, culture));
            for (int index = 0; index < binding.LevelHeaders.Count; index++)
            {
                binding.LevelHeaders[index].text = Text.Get(TownsViewModel.GetNeedsHeaderKey(index + 1));
            }

            for (int index = 0; index < binding.NotAvailableLabels.Count; index++)
            {
                binding.NotAvailableLabels[index].text = Text.Get(GameUiKeys.TownsNeedsNotAvailable);
            }

            // Need labels follow the need order within each level, raw first, as built in Rebuild.
            int labelIndex = 0;
            IReadOnlyList<TownNeedListLine> needs = item.Needs;
            for (int level = TownNeedListLine.MinLevel; level <= TownNeedListLine.MaxLevel; level++)
            {
                for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                {
                    TownNeedListLine need = needs[needIndex];
                    if (need.Level != level || labelIndex >= binding.NeedLabels.Count)
                    {
                        continue;
                    }

                    TextLineView label = binding.NeedLabels[labelIndex++];
                    label.SetLine(TownsViewModel.FormatNeedLine(need, Text, culture));
                    label.EnableInClassList(
                        TownsViewModel.PartlyMetUssClass,
                        need.Status == TownNeedLineStatus.PartlyMet);
                    label.EnableInClassList(
                        TownsViewModel.FullyMetUssClass,
                        need.Status == TownNeedLineStatus.FullyMet);
                }
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
                TextLineView summary,
                TextLineView growthLevel,
                List<Label> levelHeaders,
                List<TextLineView> needLabels,
                List<Label> notAvailableLabels,
                TextLineView passengerLabel,
                TextLineView mailLabel,
                EventCallback<ClickEvent> clickHandler)
            {
                TownId = townId;
                Row = row;
                Summary = summary;
                GrowthLevel = growthLevel;
                LevelHeaders = levelHeaders;
                NeedLabels = needLabels;
                NotAvailableLabels = notAvailableLabels;
                PassengerLabel = passengerLabel;
                MailLabel = mailLabel;
                ClickHandler = clickHandler;
            }

            public int TownId { get; }

            public VisualElement Row { get; }

            public TextLineView Summary { get; }

            public TextLineView GrowthLevel { get; }

            public IReadOnlyList<Label> LevelHeaders { get; }

            public IReadOnlyList<TextLineView> NeedLabels { get; }

            public IReadOnlyList<Label> NotAvailableLabels { get; }

            public TextLineView PassengerLabel { get; }

            public TextLineView MailLabel { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }
    }
}
