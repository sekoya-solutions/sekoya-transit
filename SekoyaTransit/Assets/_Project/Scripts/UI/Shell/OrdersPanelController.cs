#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Route (orders) section of the vehicles panel (Issue #178): edits the draft route of the selected vehicle and
    /// applies it through a callback. It has no panel of its own; it is visible while the vehicles panel is open.
    /// </summary>
    public sealed class OrdersPanelController
    {
        private readonly Label _title;
        private readonly Label _hint;
        private readonly Label _selected;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Button _apply;
        private readonly Label _status;
        private readonly VehicleOrdersDraft _draft;
        private readonly Func<VehicleListKind, int, IReadOnlyList<int>> _getStationIds;
        private readonly Func<VehicleListKind, int, IReadOnlyList<int>, bool> _tryApply;
        private readonly Func<bool> _isVisible;
        private readonly ShellTextCache _text;
        private readonly Func<CultureInfo> _culture;
        private readonly Action _applyHandler;
        private readonly List<RowBinding> _rows = new List<RowBinding>();

        /// <summary>
        /// Initializes the controller over the orders elements. <paramref name="draft"/> is shared with the vehicles
        /// list; <paramref name="isVisible"/> tells whether the vehicles panel is open.
        /// </summary>
        public OrdersPanelController(
            Label title,
            Label hint,
            Label selected,
            ScrollView list,
            Label empty,
            Button apply,
            Label status,
            VehicleOrdersDraft draft,
            Func<VehicleListKind, int, IReadOnlyList<int>> getStationIds,
            Func<VehicleListKind, int, IReadOnlyList<int>, bool> tryApply,
            Func<bool> isVisible,
            ShellTextCache text,
            Func<CultureInfo> culture)
        {
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _hint = hint ?? throw new ArgumentNullException(nameof(hint));
            _selected = selected ?? throw new ArgumentNullException(nameof(selected));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _getStationIds = getStationIds ?? throw new ArgumentNullException(nameof(getStationIds));
            _tryApply = tryApply ?? throw new ArgumentNullException(nameof(tryApply));
            _isVisible = isVisible ?? throw new ArgumentNullException(nameof(isVisible));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _culture = culture ?? throw new ArgumentNullException(nameof(culture));
            _applyHandler = OnApplyClicked;
            _apply.clicked += _applyHandler;
        }

        /// <summary>
        /// Gets whether a vehicle is selected for route editing while the vehicles panel is open.
        /// </summary>
        public bool IsEditing => _isVisible() && _draft.HasSelection;

        /// <summary>
        /// Starts editing the route of a vehicle (a click on its row).
        /// </summary>
        public void SelectVehicle(VehicleListKind kind, int vehicleId)
        {
            _draft.SelectVehicle(kind, vehicleId, _getStationIds(kind, vehicleId));
            ClearStatus();
            Refresh(forceRebuild: true);
        }

        /// <summary>
        /// Appends a station to the draft route (map click while editing). The station kind must match the vehicle kind.
        /// </summary>
        /// <returns>True when the station was appended.</returns>
        public bool TryAppendStation(VehicleListKind stationKind, int stationId)
        {
            if (!IsEditing || !_draft.Kind.HasValue || _draft.Kind.Value != stationKind)
            {
                return false;
            }

            if (!_draft.TryAppend(stationId))
            {
                return false;
            }

            ClearStatus();
            Refresh(forceRebuild: true);
            return true;
        }

        /// <summary>
        /// Clears the apply result message.
        /// </summary>
        public void ClearStatus()
        {
            _status.text = string.Empty;
        }

        /// <summary>
        /// Repaints the section while the vehicles panel is open.
        /// </summary>
        public void Refresh(bool forceRebuild)
        {
            if (!_isVisible())
            {
                return;
            }

            _title.text = _text.Get(GameUiKeys.OrdersTitle);
            _hint.text = _text.Get(GameUiKeys.OrdersHint);
            _apply.text = _text.Get(GameUiKeys.OrdersApply);
            _empty.text = _text.Get(GameUiKeys.OrdersEmpty);
            if (!_draft.HasSelection || !_draft.VehicleId.HasValue)
            {
                ClearRows();
                _list.Clear();
                _list.style.display = DisplayStyle.None;
                _empty.style.display = DisplayStyle.None;
                _apply.SetEnabled(false);
                _selected.text = _text.Get(GameUiKeys.OrdersNoneSelected);
                return;
            }

            CultureInfo culture = _culture();
            _apply.SetEnabled(true);
            _selected.text = OrdersViewModel.FormatSelected(_draft.VehicleId.Value, _text, culture);
            IReadOnlyList<int> stops = _draft.Stops;
            if (!forceRebuild && _rows.Count == stops.Count)
            {
                for (int index = 0; index < stops.Count; index++)
                {
                    _rows[index].Summary.text = OrdersViewModel.FormatStop(index, stops[index], _text, culture);
                }

                _empty.style.display = stops.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _list.style.display = stops.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                return;
            }

            Rebuild(stops, culture);
        }

        /// <summary>
        /// Releases rows and callbacks.
        /// </summary>
        public void Dispose()
        {
            _apply.clicked -= _applyHandler;
            ClearRows();
        }

        private void OnApplyClicked()
        {
            if (!_draft.HasSelection || !_draft.VehicleId.HasValue)
            {
                return;
            }

            int vehicleId = _draft.VehicleId.Value;
            VehicleListKind kind = _draft.Kind!.Value;
            bool accepted = _tryApply(kind, vehicleId, _draft.ToStationIdArray());
            _status.text = _text.Get(accepted ? GameUiKeys.OrdersApplied : GameUiKeys.OrdersRejected);
            if (accepted)
            {
                _draft.SelectVehicle(kind, vehicleId, _getStationIds(kind, vehicleId));
                Refresh(forceRebuild: true);
            }
        }

        private void Rebuild(IReadOnlyList<int> stops, CultureInfo culture)
        {
            ClearRows();
            _list.Clear();
            if (stops.Count == 0)
            {
                _empty.style.display = DisplayStyle.Flex;
                _list.style.display = DisplayStyle.None;
                return;
            }

            _empty.style.display = DisplayStyle.None;
            _list.style.display = DisplayStyle.Flex;
            for (int index = 0; index < stops.Count; index++)
            {
                int stopIndex = index;
                var row = new VisualElement();
                row.AddToClassList("orders-row");

                var summary = new Label(OrdersViewModel.FormatStop(stopIndex, stops[index], _text, culture));
                summary.AddToClassList("orders-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                var actions = new VisualElement();
                actions.AddToClassList("orders-row-actions");

                var up = new Button { text = _text.Get(GameUiKeys.OrdersUp) };
                up.AddToClassList("orders-row-action");
                Action upHandler = () =>
                {
                    if (_draft.TryMoveUp(stopIndex))
                    {
                        Refresh(forceRebuild: true);
                    }
                };
                up.clicked += upHandler;
                up.SetEnabled(stopIndex > 0);
                actions.Add(up);

                var down = new Button { text = _text.Get(GameUiKeys.OrdersDown) };
                down.AddToClassList("orders-row-action");
                Action downHandler = () =>
                {
                    if (_draft.TryMoveDown(stopIndex))
                    {
                        Refresh(forceRebuild: true);
                    }
                };
                down.clicked += downHandler;
                down.SetEnabled(stopIndex < stops.Count - 1);
                actions.Add(down);

                var remove = new Button { text = _text.Get(GameUiKeys.OrdersRemove) };
                remove.AddToClassList("orders-row-action");
                Action removeHandler = () =>
                {
                    if (_draft.TryRemoveAt(stopIndex))
                    {
                        ClearStatus();
                        Refresh(forceRebuild: true);
                    }
                };
                remove.clicked += removeHandler;
                actions.Add(remove);

                row.Add(actions);
                _list.Add(row);
                _rows.Add(new RowBinding(summary, up, down, remove, upHandler, downHandler, removeHandler));
            }
        }

        private void ClearRows()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                binding.Up.clicked -= binding.UpHandler;
                binding.Down.clicked -= binding.DownHandler;
                binding.Remove.clicked -= binding.RemoveHandler;
            }

            _rows.Clear();
        }

        private sealed class RowBinding
        {
            public RowBinding(
                Label summary,
                Button up,
                Button down,
                Button remove,
                Action upHandler,
                Action downHandler,
                Action removeHandler)
            {
                Summary = summary;
                Up = up;
                Down = down;
                Remove = remove;
                UpHandler = upHandler;
                DownHandler = downHandler;
                RemoveHandler = removeHandler;
            }

            public Label Summary { get; }

            public Button Up { get; }

            public Button Down { get; }

            public Button Remove { get; }

            public Action UpHandler { get; }

            public Action DownHandler { get; }

            public Action RemoveHandler { get; }
        }
    }
}
