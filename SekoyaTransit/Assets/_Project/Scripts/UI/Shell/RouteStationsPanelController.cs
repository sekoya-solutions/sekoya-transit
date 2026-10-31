#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The narrow station window of one route (Issue #229): a card per stop with up and down arrows, Remove and
    /// "Wait for full"; clicking a station on the map appends it, and a refused click shows a red message. Removing the
    /// stop that would leave a route with vehicles invalid asks first.
    /// </summary>
    public sealed class RouteStationsPanelController : ShellWindowController
    {
        private readonly Label _title;
        private readonly Label _header;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Label _hint;
        private readonly Label _warning;
        private readonly VisualElement _confirm;
        private readonly Label _confirmText;
        private readonly Button _confirmYes;
        private readonly Button _confirmNo;
        private readonly Func<RoutesBindings?> _bindings;
        private readonly Action _onRouteMissing;
        private readonly List<CardBinding> _cards = new List<CardBinding>();
        private readonly Action _confirmYesHandler;
        private readonly Action _confirmNoHandler;
        private int _lastFingerprint = int.MinValue;
        private string? _warningKey;
        private int _pendingRemoveIndex = -1;

        public RouteStationsPanelController(
            VisualElement panel,
            Label title,
            Button close,
            Label header,
            ScrollView list,
            Label empty,
            Label hint,
            Label warning,
            VisualElement confirm,
            Label confirmText,
            Button confirmYes,
            Button confirmNo,
            Func<RoutesBindings?> bindings,
            Action onCloseRequested,
            Action onRouteMissing,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "route-side-panel--hidden", title, RoutesUiKeys.Title, close, RoutesUiKeys.Close, onCloseRequested, text, culture)
        {
            _title = title;
            _header = header ?? throw new ArgumentNullException(nameof(header));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _hint = hint ?? throw new ArgumentNullException(nameof(hint));
            _warning = warning ?? throw new ArgumentNullException(nameof(warning));
            _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            _confirmText = confirmText ?? throw new ArgumentNullException(nameof(confirmText));
            _confirmYes = confirmYes ?? throw new ArgumentNullException(nameof(confirmYes));
            _confirmNo = confirmNo ?? throw new ArgumentNullException(nameof(confirmNo));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _onRouteMissing = onRouteMissing ?? throw new ArgumentNullException(nameof(onRouteMissing));
            _confirmYesHandler = ConfirmRemove;
            _confirmNoHandler = CancelRemove;
            _confirmYes.clicked += _confirmYesHandler;
            _confirmNo.clicked += _confirmNoHandler;
            HideConfirm();
        }

        /// <summary>
        /// Gets the route being edited, or null when no route is shown.
        /// </summary>
        public int? RouteId { get; private set; }

        /// <summary>
        /// Shows the stops of <paramref name="routeId"/>; call before <see cref="ShellPanelController.SetOpen"/>.
        /// </summary>
        public void ShowRoute(int routeId)
        {
            RouteId = routeId;
            _warningKey = null;
            _lastFingerprint = int.MinValue;
            HideConfirm();
        }

        /// <summary>
        /// Reports the outcome of a map click on a station: a refusal shows the red message, a success clears it.
        /// </summary>
        public void ShowStopResult(RouteStopAddResult result)
        {
            _warningKey = RoutesViewModel.StopErrorKey(result);
            Refresh(force: true);
        }

        /// <inheritdoc />
        protected override void DisposeContent()
        {
            _confirmYes.clicked -= _confirmYesHandler;
            _confirmNo.clicked -= _confirmNoHandler;
            ClearCards();
        }

        /// <inheritdoc />
        protected override void RefreshWindowContent(bool force)
        {
            RouteDetail? detail = CurrentDetail();
            if (detail == null)
            {
                _onRouteMissing();
                return;
            }

            CultureInfo culture = Culture();
            _title.text = detail.Name;
            _header.text = Text.Get(RoutesUiKeys.StationsHeader);
            _hint.text = Text.Get(RoutesUiKeys.StationsHint);
            _empty.text = Text.Get(RoutesUiKeys.StationsEmpty);
            _empty.style.display = detail.Stops.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _warning.text = _warningKey != null ? Text.Get(_warningKey) : string.Empty;
            _warning.style.display = _warningKey != null ? DisplayStyle.Flex : DisplayStyle.None;
            _confirmText.text = Text.Get(RoutesUiKeys.StationsRemoveConfirm);
            _confirmYes.text = Text.Get(RoutesUiKeys.StationsRemoveYes);
            _confirmNo.text = Text.Get(RoutesUiKeys.ConfirmNo);
            int fingerprint = detail.ComputeFingerprint();
            if (!force && fingerprint == _lastFingerprint)
            {
                return;
            }

            _lastFingerprint = fingerprint;
            ClearCards();
            for (int index = 0; index < detail.Stops.Count; index++)
            {
                AddCard(detail.RouteId, index, detail.Stops[index], culture);
            }
        }

        private RouteDetail? CurrentDetail()
        {
            RoutesBindings? bindings = _bindings();
            return bindings != null && RouteId.HasValue ? bindings.Detail(RouteId.Value) : null;
        }

        private void AddCard(int routeId, int stopIndex, RouteStopItem stop, CultureInfo culture)
        {
            var card = new VisualElement();
            card.AddToClassList("route-stop-card");

            var top = new VisualElement();
            top.AddToClassList("route-stop-line");
            var name = new Label(RoutesViewModel.FormatStop(stop, Text, culture));
            name.AddToClassList("route-stop-name");
            top.Add(name);
            Button up = SmallButton(Text.Get(RoutesUiKeys.StationsUp), stop.CanMoveUp);
            Action upHandler = () => Act(b => b.MoveStop(routeId, stopIndex, true));
            up.clicked += upHandler;
            top.Add(up);
            card.Add(top);

            var bottom = new VisualElement();
            bottom.AddToClassList("route-stop-line");
            var wait = new Toggle(Text.Get(RoutesUiKeys.StationsWaitForFull));
            wait.SetValueWithoutNotify(stop.WaitForFull);
            wait.AddToClassList("route-stop-wait");
            EventCallback<ChangeEvent<bool>> waitHandler =
                evt => Act(b => b.SetWaitForFull(routeId, stopIndex, evt.newValue));
            wait.RegisterValueChangedCallback(waitHandler);
            bottom.Add(wait);
            Button down = SmallButton(Text.Get(RoutesUiKeys.StationsDown), stop.CanMoveDown);
            Action downHandler = () => Act(b => b.MoveStop(routeId, stopIndex, false));
            down.clicked += downHandler;
            bottom.Add(down);
            card.Add(bottom);

            var remove = new Button { text = Text.Get(RoutesUiKeys.StationsRemove) };
            remove.AddToClassList("routes-button");
            remove.AddToClassList("route-stop-remove");
            bool needsConfirmation = stop.RemoveNeedsConfirmation;
            Action removeHandler = () => RequestRemove(stopIndex, needsConfirmation);
            remove.clicked += removeHandler;
            card.Add(remove);

            _list.Add(card);
            _cards.Add(new CardBinding(up, upHandler, down, downHandler, remove, removeHandler, wait, waitHandler));
        }

        private static Button SmallButton(string text, bool enabled)
        {
            var button = new Button { text = text };
            button.AddToClassList("routes-button");
            button.AddToClassList("route-stop-arrow");
            button.SetEnabled(enabled);
            return button;
        }

        private void Act(Action<RoutesBindings> action)
        {
            RoutesBindings? bindings = _bindings();
            if (bindings == null)
            {
                return;
            }

            action(bindings);
            _warningKey = null;
            HideConfirm();
            Refresh(force: true);
        }

        private void RequestRemove(int stopIndex, bool needsConfirmation)
        {
            if (!needsConfirmation)
            {
                int index = stopIndex;
                Act(b => b.RemoveStop(RouteId ?? -1, index));
                return;
            }

            _pendingRemoveIndex = stopIndex;
            _confirm.style.display = DisplayStyle.Flex;
        }

        private void ConfirmRemove()
        {
            int index = _pendingRemoveIndex;
            if (index >= 0 && RouteId.HasValue)
            {
                int routeId = RouteId.Value;
                Act(b => b.RemoveStop(routeId, index));
            }

            HideConfirm();
        }

        private void CancelRemove()
        {
            HideConfirm();
        }

        private void HideConfirm()
        {
            _pendingRemoveIndex = -1;
            _confirm.style.display = DisplayStyle.None;
        }

        private void ClearCards()
        {
            for (int index = 0; index < _cards.Count; index++)
            {
                CardBinding card = _cards[index];
                card.Up.clicked -= card.UpHandler;
                card.Down.clicked -= card.DownHandler;
                card.Remove.clicked -= card.RemoveHandler;
                card.Wait.UnregisterValueChangedCallback(card.WaitHandler);
            }

            _cards.Clear();
            _list.Clear();
        }

        private sealed class CardBinding
        {
            public CardBinding(
                Button up,
                Action upHandler,
                Button down,
                Action downHandler,
                Button remove,
                Action removeHandler,
                Toggle wait,
                EventCallback<ChangeEvent<bool>> waitHandler)
            {
                Up = up;
                UpHandler = upHandler;
                Down = down;
                DownHandler = downHandler;
                Remove = remove;
                RemoveHandler = removeHandler;
                Wait = wait;
                WaitHandler = waitHandler;
            }

            public Button Up { get; }

            public Action UpHandler { get; }

            public Button Down { get; }

            public Action DownHandler { get; }

            public Button Remove { get; }

            public Action RemoveHandler { get; }

            public Toggle Wait { get; }

            public EventCallback<ChangeEvent<bool>> WaitHandler { get; }
        }
    }
}
