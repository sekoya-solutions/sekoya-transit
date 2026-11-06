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
    /// of that mode (Buy; a train with a chosen number of wagons, Issue #326). Adding is disabled while the route has
    /// fewer than two stops.
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
        private readonly Dictionary<string, int> _wagonCounts = new Dictionary<string, int>(StringComparer.Ordinal);
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
            _wagonCounts.Clear();
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
                int wagonCount = GetWagonCount(option);
                VisualElement row = AddRow(
                    _addList,
                    Text.Get(option.NameKey),
                    string.Format(
                        culture,
                        Text.Get(RoutesUiKeys.VehiclesBuy),
                        CashFormat.FormatAmount(option.GetPrice(wagonCount), culture)),
                    option.CanAfford(wagonCount),
                    b => b.BuyVehicle(routeId, typeId, wagonCount));
                if (option.IsTrain)
                {
                    AddWagonSelector(row, option, wagonCount, culture);
                }
            }
        }

        private int GetWagonCount(VehiclePurchaseOption option)
        {
            return _wagonCounts.TryGetValue(option.TypeId, out int chosen)
                ? option.ClampWagons(chosen)
                : option.MinWagons;
        }

        private VisualElement AddRow(ScrollView list, string label, string buttonText, bool enabled, Action<RoutesBindings> action)
        {
            var card = new VisualElement();
            card.AddToClassList("route-vehicle-row");
            var line = new VisualElement();
            line.AddToClassList("route-vehicle-line");
            var name = new Label(label);
            name.AddToClassList("route-vehicle-name");
            line.Add(name);
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
            line.Add(CreateButton(buttonText, enabled, handler));
            card.Add(line);
            list.Add(card);
            return card;
        }

        // The wagon count of a train type (Issue #326): minus, count and plus, from 1 to the locomotive's maximum. The
        // choice lives in this window only and resets when it opens for a route.
        private void AddWagonSelector(VisualElement card, VehiclePurchaseOption option, int wagonCount, CultureInfo culture)
        {
            string typeId = option.TypeId;
            var line = new VisualElement();
            line.AddToClassList("route-vehicle-line");
            line.AddToClassList("route-vehicle-wagons");
            var label = new Label(Text.Get(RoutesUiKeys.VehiclesWagons));
            label.AddToClassList("route-vehicle-name");
            line.Add(label);
            var stepper = new VisualElement();
            stepper.AddToClassList("route-vehicle-stepper");
            stepper.Add(CreateButton(
                "-",
                wagonCount > option.MinWagons,
                () => SetWagonCount(typeId, option.ClampWagons(wagonCount - 1))));
            var count = new Label(wagonCount.ToString(culture));
            count.AddToClassList("route-vehicle-stepper__count");
            stepper.Add(count);
            stepper.Add(CreateButton(
                "+",
                wagonCount < option.MaxWagons,
                () => SetWagonCount(typeId, option.ClampWagons(wagonCount + 1))));
            line.Add(stepper);
            card.Add(line);
        }

        private void SetWagonCount(string typeId, int wagonCount)
        {
            _wagonCounts[typeId] = wagonCount;
            Refresh(force: true);
        }

        private Button CreateButton(string text, bool enabled, Action handler)
        {
            var button = new Button { text = text };
            button.AddToClassList("routes-button");
            button.SetEnabled(enabled);
            button.clicked += handler;
            _buttons.Add(new ButtonBinding(button, handler));
            return button;
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
