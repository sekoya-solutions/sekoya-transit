#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Vehicle unlocks panel (Issue #178): one row per vehicle type with an unlock button while it can be unlocked.
    /// </summary>
    public sealed class UnlocksPanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<VehicleUnlockListItem>> _provider;
        private readonly Action<string> _onUnlock;
        private readonly Action _onUnlocked;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private int _lastFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the unlocks panel elements. <paramref name="onUnlock"/> receives the type id
        /// of the clicked unlock button; <paramref name="onUnlocked"/> then runs once the list repainted (the buy
        /// options of the vehicles panel depend on it).
        /// </summary>
        public UnlocksPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<VehicleUnlockListItem>> provider,
            Action<string> onUnlock,
            Action onUnlocked,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "unlocks-panel--hidden", title, GameUiKeys.UnlocksTitle, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _onUnlock = onUnlock ?? throw new ArgumentNullException(nameof(onUnlock));
            _onUnlocked = onUnlocked ?? throw new ArgumentNullException(nameof(onUnlocked));
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
            _empty.text = Text.Get(GameUiKeys.UnlocksEmpty);
            IReadOnlyList<VehicleUnlockListItem> unlocks = _provider();
            int fingerprint = UnlocksViewModel.ComputeFingerprint(unlocks);
            if (force || fingerprint != _lastFingerprint)
            {
                _lastFingerprint = fingerprint;
                Rebuild(unlocks);
                return;
            }

            for (int index = 0; index < unlocks.Count; index++)
            {
                UpdateRow(_rows[index], unlocks[index]);
            }
        }

        private void Rebuild(IReadOnlyList<VehicleUnlockListItem> unlocks)
        {
            ClearRows();
            _list.Clear();
            if (unlocks.Count == 0)
            {
                _empty.style.display = DisplayStyle.Flex;
                return;
            }

            _empty.style.display = DisplayStyle.None;
            CultureInfo culture = Culture();
            for (int index = 0; index < unlocks.Count; index++)
            {
                VehicleUnlockListItem item = unlocks[index];
                string typeId = item.TypeId;
                var row = new VisualElement();
                row.AddToClassList("unlocks-row");

                var summary = new Label(UnlocksViewModel.FormatSummary(item, Text, culture));
                summary.AddToClassList("unlocks-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                Button? action = null;
                Action? actionHandler = null;
                if (item.Status == VehicleUnlockRowStatus.Unlockable)
                {
                    action = new Button { text = UnlocksViewModel.FormatAction(item, Text, culture) };
                    action.AddToClassList("unlocks-row-action");
                    action.SetEnabled(item.CanAffordUnlock);
                    actionHandler = () =>
                    {
                        _onUnlock(typeId);
                        Refresh(force: true);
                        _onUnlocked();
                    };
                    action.clicked += actionHandler;
                    row.Add(action);
                }

                _list.Add(row);
                _rows.Add(new RowBinding(summary, action, actionHandler));
            }
        }

        private void UpdateRow(RowBinding binding, VehicleUnlockListItem item)
        {
            CultureInfo culture = Culture();
            binding.Summary.text = UnlocksViewModel.FormatSummary(item, Text, culture);
            if (binding.Action != null)
            {
                binding.Action.text = UnlocksViewModel.FormatAction(item, Text, culture);
                binding.Action.SetEnabled(item.Status == VehicleUnlockRowStatus.Unlockable && item.CanAffordUnlock);
            }
        }

        private void ClearRows()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                if (binding.Action != null && binding.ActionHandler != null)
                {
                    binding.Action.clicked -= binding.ActionHandler;
                }
            }

            _rows.Clear();
        }

        private sealed class RowBinding
        {
            public RowBinding(Label summary, Button? action, Action? actionHandler)
            {
                Summary = summary;
                Action = action;
                ActionHandler = actionHandler;
            }

            public Label Summary { get; }

            public Button? Action { get; }

            public Action? ActionHandler { get; }
        }
    }
}
