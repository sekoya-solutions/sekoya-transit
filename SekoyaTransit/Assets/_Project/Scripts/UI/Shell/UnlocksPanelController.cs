#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Vehicles unlock window (Issue #178, reworked in Issue #286): a full-screen window with one column per vehicle
    /// category (trucks, trains, boats, planes) and one card per vehicle type with its sprite, stats, and a buy button
    /// while it can be bought. Cards of types that are not available yet are greyed out.
    /// </summary>
    public sealed class UnlocksPanelController : ShellWindowController
    {
        private readonly ScrollView _list;
        private readonly Func<IReadOnlyList<VehicleUnlockListItem>> _provider;
        private readonly Action<string> _onUnlock;
        private readonly Action _onUnlocked;
        private readonly List<BuyBinding> _buyBindings = new List<BuyBinding>();
        private int _lastFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the unlocks window elements. <paramref name="onUnlock"/> receives the type id
        /// of the clicked buy button; <paramref name="onUnlocked"/> then runs once the window repainted (the buy
        /// options of the vehicles panel depend on it).
        /// </summary>
        public UnlocksPanelController(
            VisualElement panel,
            Label title,
            Button close,
            ScrollView list,
            Func<IReadOnlyList<VehicleUnlockListItem>> provider,
            Action<string> onUnlock,
            Action onUnlocked,
            Action onCloseRequested,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(
                panel,
                "unlocks-panel--hidden",
                title,
                GameUiKeys.UnlocksTitle,
                close,
                GameUiKeys.UnlocksClose,
                onCloseRequested,
                text,
                culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _onUnlock = onUnlock ?? throw new ArgumentNullException(nameof(onUnlock));
            _onUnlocked = onUnlocked ?? throw new ArgumentNullException(nameof(onUnlocked));
        }

        /// <inheritdoc />
        protected override void DisposeContent()
        {
            ClearBindings();
            _list.Clear();
            _lastFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void RefreshWindowContent(bool force)
        {
            IReadOnlyList<VehicleUnlockListItem> unlocks = _provider();
            int fingerprint = UnlocksViewModel.ComputeFingerprint(unlocks);
            if (!force && fingerprint == _lastFingerprint)
            {
                return;
            }

            _lastFingerprint = fingerprint;
            Rebuild(UnlocksViewModel.BuildColumns(unlocks, Text, Culture()));
        }

        private void Rebuild(IReadOnlyList<UnlockColumnRow> columns)
        {
            ClearBindings();
            _list.Clear();
            var row = new VisualElement();
            row.AddToClassList("unlocks-columns");
            for (int index = 0; index < columns.Count; index++)
            {
                row.Add(BuildColumn(columns[index]));
            }

            _list.Add(row);
        }

        private VisualElement BuildColumn(UnlockColumnRow column)
        {
            var element = new VisualElement();
            element.AddToClassList("unlocks-column");
            var title = new Label(column.Title);
            title.AddToClassList("unlocks-column__title");
            element.Add(title);
            for (int index = 0; index < column.Cards.Count; index++)
            {
                element.Add(BuildCard(column.Cards[index]));
            }

            return element;
        }

        private VisualElement BuildCard(UnlockCardRow card)
        {
            var element = new VisualElement();
            element.AddToClassList("unlocks-card");
            if (card.IsGreyedOut)
            {
                element.AddToClassList("unlocks-card--locked");
            }

            var header = new Label(card.Header);
            header.AddToClassList("unlocks-card__header");
            element.Add(header);

            var body = new VisualElement();
            body.AddToClassList("unlocks-card__body");
            var sprite = new VisualElement();
            sprite.AddToClassList("unlocks-card__sprite");
            sprite.AddToClassList(
                card.Sprite == UnlockSprite.Locomotive
                    ? "unlocks-card__sprite--locomotive"
                    : "unlocks-card__sprite--truck");
            body.Add(sprite);
            var stats = new VisualElement();
            stats.AddToClassList("unlocks-card__stats");
            for (int index = 0; index < card.StatLines.Count; index++)
            {
                var line = new Label(card.StatLines[index]);
                line.AddToClassList("unlocks-card__stat");
                stats.Add(line);
            }

            body.Add(stats);
            element.Add(body);

            if (card.Kind == UnlockCardKind.Buyable && card.TypeId != null)
            {
                string typeId = card.TypeId;
                var buy = new Button { text = card.Footer };
                buy.AddToClassList("unlocks-card__buy");
                buy.SetEnabled(card.CanBuy);
                Action handler = () =>
                {
                    _onUnlock(typeId);
                    Refresh(force: true);
                    _onUnlocked();
                };
                buy.clicked += handler;
                _buyBindings.Add(new BuyBinding(buy, handler));
                element.Add(buy);
            }
            else if (card.Footer.Length > 0)
            {
                var footer = new Label(card.Footer);
                footer.AddToClassList("unlocks-card__footer");
                element.Add(footer);
            }

            return element;
        }

        private void ClearBindings()
        {
            for (int index = 0; index < _buyBindings.Count; index++)
            {
                _buyBindings[index].Button.clicked -= _buyBindings[index].Handler;
            }

            _buyBindings.Clear();
        }

        private readonly struct BuyBinding
        {
            public BuyBinding(Button button, Action handler)
            {
                Button = button;
                Handler = handler;
            }

            public Button Button { get; }

            public Action Handler { get; }
        }
    }
}
