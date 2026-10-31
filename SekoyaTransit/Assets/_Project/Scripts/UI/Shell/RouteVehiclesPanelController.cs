#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The narrow vehicle window of one route (Issue #229): the route's vehicles with Remove (it unassigns, never
    /// sells), and "Add a vehicle", which lists the unassigned vehicles of the route's mode (Add) and the unlocked types
    /// of that mode (Buy, a train with one wagon). Adding is disabled while the route has fewer than two stops.
    /// </summary>
    public sealed class RouteVehiclesPanelController : ShellWindowController
    {
        private readonly Label _title;
        private readonly Label _header;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Label _invalid;
        private readonly Button _addToggle;
        private readonly VisualElement _addSection;
        private readonly Label _addHeader;
        private readonly ScrollView _addList;
        private readonly Label _noneAvailable;
        private readonly Func<RoutesBindings?> _bindings;
        private readonly Action _onRouteMissing;
        private readonly List<ButtonBinding> _buttons = new List<ButtonBinding>();
        private readonly Action _addToggleHandler;
        private int _lastFingerprint = int.MinValue;
        private bool _addOpen;

        public RouteVehiclesPanelController(
            VisualElement panel,
            Label title,
            Button close,
            Label header,
            ScrollView list,
            Label empty,
            Label invalid,
            Button addToggle,
            VisualElement addSection,
            Label addHeader,
            ScrollView addList,
            Label noneAvailable,
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
            _invalid = invalid ?? throw new ArgumentNullException(nameof(invalid));
            _addToggle = addToggle ?? throw new ArgumentNullException(nameof(addToggle));
            _addSection = addSection ?? throw new ArgumentNullException(nameof(addSection));
            _addHeader = addHeader ?? throw new ArgumentNullException(nameof(addHeader));
            _addList = addList ?? throw new ArgumentNullException(nameof(addList));
            _noneAvailable = noneAvailable ?? throw new ArgumentNullException(nameof(noneAvailable));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _onRouteMissing = onRouteMissing ?? throw new ArgumentNullException(nameof(onRouteMissing));
            _addToggleHandler = ToggleAddSection;
            _addToggle.clicked += _addToggleHandler;
        }

        /// <summary>
        /// Gets the route being edited, or null when no route is shown.
        /// </summary>
        public int? RouteId { get; private set; }

        /// <summary>
        /// Shows the vehicles of <paramref name="routeId"/>; call before <see cref="ShellPanelController.SetOpen"/>.
        /// </summary>
        public void ShowRoute(int routeId)
        {
            RouteId = routeId;
            _addOpen = false;
            _lastFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void DisposeContent()
        {
            _addToggle.clicked -= _addToggleHandler;
            ClearButtons();
        }

        /// <inheritdoc />
        protected override void RefreshWindowContent(bool force)
        {
            RoutesBindings? bindings = _bindings();
            RouteDetail? detail = bindings != null && RouteId.HasValue ? bindings.Detail(RouteId.Value) : null;
            if (detail == null)
            {
                _onRouteMissing();
                return;
            }

            _title.text = detail.Name;
            _header.text = Text.Get(RoutesUiKeys.VehiclesHeader);
            _empty.text = Text.Get(RoutesUiKeys.VehiclesEmpty);
            _empty.style.display = detail.Vehicles.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _invalid.text = Text.Get(RoutesUiKeys.VehiclesInvalidRoute);
            _invalid.style.display = detail.IsValid ? DisplayStyle.None : DisplayStyle.Flex;
            _addToggle.text = Text.Get(RoutesUiKeys.VehiclesAddButton);
            _addToggle.SetEnabled(detail.IsValid);
            bool showAdd = _addOpen && detail.IsValid;
            _addSection.style.display = showAdd ? DisplayStyle.Flex : DisplayStyle.None;
            _addHeader.text = Text.Get(RoutesUiKeys.VehiclesAvailable);
            _noneAvailable.text = Text.Get(RoutesUiKeys.VehiclesNoneAvailable);
            _noneAvailable.style.display = detail.UnassignedVehicles.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            int fingerprint = detail.ComputeFingerprint() ^ (showAdd ? 0x5bd1e995 : 0);
            if (!force && fingerprint == _lastFingerprint)
            {
                return;
            }

            _lastFingerprint = fingerprint;
            Rebuild(detail, Culture());
        }

        private void Rebuild(RouteDetail detail, CultureInfo culture)
        {
            ClearButtons();
            int routeId = detail.RouteId;
            for (int index = 0; index < detail.Vehicles.Count; index++)
            {
                RouteVehicleItem vehicle = detail.Vehicles[index];
                int vehicleId = vehicle.VehicleId;
                AddRow(
                    _list,
                    RoutesViewModel.FormatVehicle(vehicle, Text, culture),
                    Text.Get(RoutesUiKeys.VehiclesRemove),
                    true,
                    b => b.UnassignVehicle(routeId, vehicleId));
            }

            for (int index = 0; index < detail.UnassignedVehicles.Count; index++)
            {
                RouteVehicleItem vehicle = detail.UnassignedVehicles[index];
                int vehicleId = vehicle.VehicleId;
                AddRow(
                    _addList,
                    RoutesViewModel.FormatVehicle(vehicle, Text, culture),
                    Text.Get(RoutesUiKeys.VehiclesAdd),
                    true,
                    b => b.AssignVehicle(routeId, vehicleId));
            }

            for (int index = 0; index < detail.BuyOptions.Count; index++)
            {
                VehiclePurchaseOption option = detail.BuyOptions[index];
                string typeId = option.TypeId;
                AddRow(
                    _addList,
                    Text.Get(option.NameKey),
                    string.Format(culture, Text.Get(RoutesUiKeys.VehiclesBuy), CashFormat.FormatAmount(option.PurchasePrice, culture)),
                    option.CanAfford,
                    b => b.BuyVehicle(routeId, typeId));
            }
        }

        private void AddRow(ScrollView list, string label, string buttonText, bool enabled, Action<RoutesBindings> action)
        {
            var row = new VisualElement();
            row.AddToClassList("route-vehicle-row");
            var name = new Label(label);
            name.AddToClassList("route-vehicle-name");
            row.Add(name);
            var button = new Button { text = buttonText };
            button.AddToClassList("routes-button");
            button.SetEnabled(enabled);
            Action handler = () =>
            {
                RoutesBindings? bindings = _bindings();
                if (bindings == null)
                {
                    return;
                }

                action(bindings);
                Refresh(force: true);
            };
            button.clicked += handler;
            row.Add(button);
            list.Add(row);
            _buttons.Add(new ButtonBinding(button, handler));
        }

        private void ToggleAddSection()
        {
            _addOpen = !_addOpen;
            Refresh(force: true);
        }

        private void ClearButtons()
        {
            for (int index = 0; index < _buttons.Count; index++)
            {
                _buttons[index].Button.clicked -= _buttons[index].Handler;
            }

            _buttons.Clear();
            _list.Clear();
            _addList.Clear();
        }

        private readonly struct ButtonBinding
        {
            public ButtonBinding(Button button, Action handler)
            {
                Button = button;
                Handler = handler;
            }

            public Button Button { get; }

            public Action Handler { get; }
        }
    }
}
