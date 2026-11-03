#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Callbacks the vehicles panel raises toward Presentation (Issue #178, routes in Issue #229, route links in
    /// Issue #280).
    /// </summary>
    public sealed class VehiclesPanelActions
    {
        /// <summary>
        /// Initializes the callback set.
        /// </summary>
        public VehiclesPanelActions(
            Action<VehicleListKind, int> sellVehicle,
            Action<int> addRailWagon,
            Action<int> sellRailWagon,
            Action<int> openRoute)
        {
            SellVehicle = sellVehicle ?? throw new ArgumentNullException(nameof(sellVehicle));
            AddRailWagon = addRailWagon ?? throw new ArgumentNullException(nameof(addRailWagon));
            SellRailWagon = sellRailWagon ?? throw new ArgumentNullException(nameof(sellRailWagon));
            OpenRoute = openRoute ?? throw new ArgumentNullException(nameof(openRoute));
        }

        /// <summary>
        /// Gets the sell callback (kind, vehicle id).
        /// </summary>
        public Action<VehicleListKind, int> SellVehicle { get; }

        /// <summary>
        /// Gets the add-wagon callback (rail vehicle id).
        /// </summary>
        public Action<int> AddRailWagon { get; }

        /// <summary>
        /// Gets the sell-wagon callback (rail vehicle id).
        /// </summary>
        public Action<int> SellRailWagon { get; }

        /// <summary>
        /// Gets the route link callback (route id): opens the routes window with that route highlighted.
        /// </summary>
        public Action<int> OpenRoute { get; }
    }

    /// <summary>
    /// Vehicles list panel (Issue #178, reworked in Issue #280): one card per owned vehicle with its cargo, route link,
    /// last month finances (green when profitable, red otherwise) and sell and wagon buttons. Unassigned vehicles come
    /// last under their own heading. While a station is selected on the map it lists only the vehicles of the routes
    /// that stop there; vehicles are bought from the routes window.
    /// </summary>
    public sealed class VehiclesPanelController : ShellPanelController
    {
        private readonly Label _stationHint;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<VehicleListItem>> _vehiclesProvider;
        private readonly Func<VehiclesPanelState> _stateProvider;
        private readonly VehiclesPanelActions _actions;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private int _lastVehicleFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the vehicles panel elements.
        /// </summary>
        public VehiclesPanelController(
            VisualElement panel,
            Label title,
            Label stationHint,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<VehicleListItem>> vehiclesProvider,
            Func<VehiclesPanelState> stateProvider,
            VehiclesPanelActions actions,
            ICargoIconSource? cargoIcons,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "vehicles-panel--hidden", title, GameUiKeys.VehiclesTitle, text, culture)
        {
            _stationHint = stationHint ?? throw new ArgumentNullException(nameof(stationHint));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _vehiclesProvider = vehiclesProvider ?? throw new ArgumentNullException(nameof(vehiclesProvider));
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _cargoIcons = cargoIcons;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            ClearRows();
            _lastVehicleFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            VehiclesPanelState state = _stateProvider();
            _empty.text = Text.Get(GameUiKeys.VehiclesEmpty);
            _stationHint.text = Text.Get(RoutesUiKeys.VehiclesStationHint);
            _stationHint.style.display = state.HasSelectedStation ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshVehicles(state, force);
        }

        private void RefreshVehicles(VehiclesPanelState state, bool forceRebuild)
        {
            CultureInfo culture = Culture();
            IReadOnlyList<VehicleListItem> vehicles = _vehiclesProvider();
            int fingerprint = VehiclesViewModel.ComputeVehicleFingerprint(vehicles);
            if (forceRebuild || fingerprint != _lastVehicleFingerprint)
            {
                _lastVehicleFingerprint = fingerprint;
                RebuildRows(vehicles, state, culture);
                return;
            }

            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                _rows[index].Title.text = VehiclesViewModel.FormatTitle(item, Text, culture);
                _rows[index].Cargo.SetLine(VehiclesViewModel.FormatCargo(item, Text, culture));
                if (_rows[index].AddWagon != null)
                {
                    _rows[index].AddWagonLine!.SetLine(VehiclesViewModel.FormatAddWagon(state.WagonPurchasePrice, Text, culture));
                    _rows[index].AddWagon!.SetEnabled(item.WagonCount < item.MaxWagons && state.CanAffordWagon);
                }
            }
        }

        private void RebuildRows(IReadOnlyList<VehicleListItem> vehicles, VehiclesPanelState state, CultureInfo culture)
        {
            ClearRows();
            _list.Clear();
            _empty.style.display = vehicles.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            bool unassignedHeaderAdded = false;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                // Unassigned vehicles come last; they get their own heading so they read as a separate list.
                if (!unassignedHeaderAdded && item.Status == VehicleListStatusKind.Unassigned)
                {
                    var header = new Label(Text.Get(RoutesUiKeys.VehiclesUnassignedHeader));
                    header.AddToClassList("vehicles-section-header");
                    _list.Add(header);
                    unassignedHeaderAdded = true;
                }

                var row = new VisualElement();
                row.AddToClassList("vehicles-row");
                string? cardClass = VehiclesViewModel.CardClass(item);
                if (cardClass != null)
                {
                    row.AddToClassList(cardClass);
                }

                var info = new VisualElement();
                info.AddToClassList("vehicles-row-info");
                row.Add(info);

                var title = new Label(VehiclesViewModel.FormatTitle(item, Text, culture));
                title.AddToClassList("vehicles-row-title");
                info.Add(title);

                var cargo = new TextLineView(_cargoIcons, Text);
                cargo.SetLine(VehiclesViewModel.FormatCargo(item, Text, culture));
                cargo.AddToClassList("vehicles-row-summary");
                info.Add(cargo);

                Label? routeLink = null;
                EventCallback<ClickEvent>? routeHandler = null;
                info.Add(BuildRouteLine(item, out routeLink, out routeHandler));

                var finances = new TextLineView(_cargoIcons, Text);
                finances.SetLine(VehiclesViewModel.FormatFinances(item, Text, culture));
                finances.AddToClassList("vehicles-row-summary");
                finances.AddToClassList("vehicles-row-finance");
                info.Add(finances);

                TextLine benefitLine = VehiclesViewModel.FormatBenefit(item, Text, culture);
                if (!benefitLine.IsEmpty)
                {
                    var benefit = new TextLineView(_cargoIcons, Text);
                    benefit.SetLine(benefitLine);
                    benefit.AddToClassList("vehicles-row-summary");
                    benefit.AddToClassList("vehicles-row-finance");
                    info.Add(benefit);
                }

                var actions = new VisualElement();
                actions.AddToClassList("vehicles-row-actions");
                row.Add(actions);

                VehicleListKind kind = item.Kind;
                int vehicleId = item.Id;
                Button? addWagon = null;
                TextLineView? addWagonLine = null;
                Action? addWagonHandler = null;
                Button? sellWagon = null;
                Action? sellWagonHandler = null;
                if (kind == VehicleListKind.Rail)
                {
                    addWagon = CreateButton(VehiclesViewModel.FormatAddWagon(state.WagonPurchasePrice, Text, culture), out addWagonLine);
                    addWagon.AddToClassList("vehicles-row-sell");
                    addWagon.SetEnabled(item.WagonCount < item.MaxWagons && state.CanAffordWagon);
                    addWagonHandler = () => Act(() => _actions.AddRailWagon(vehicleId));
                    addWagon.clicked += addWagonHandler;
                    actions.Add(addWagon);

                    sellWagon = CreateButton(VehiclesViewModel.FormatSellWagon(state.WagonSellValue, Text, culture), out _);
                    sellWagon.AddToClassList("vehicles-row-sell");
                    sellWagon.SetEnabled(item.WagonCount > 0);
                    sellWagonHandler = () => Act(() => _actions.SellRailWagon(vehicleId));
                    sellWagon.clicked += sellWagonHandler;
                    actions.Add(sellWagon);
                }

                Button sell = CreateButton(VehiclesViewModel.FormatSell(item, Text, culture), out _);
                sell.AddToClassList("vehicles-row-sell");
                Action sellHandler = () => Act(() => _actions.SellVehicle(kind, vehicleId));
                sell.clicked += sellHandler;
                actions.Add(sell);
                _list.Add(row);
                _rows.Add(new RowBinding(
                    title,
                    cargo,
                    addWagonLine,
                    sell,
                    sellHandler,
                    addWagon,
                    addWagonHandler,
                    sellWagon,
                    sellWagonHandler,
                    routeLink,
                    routeHandler));
            }
        }

        // A button whose label is a line with the cash icon; the label ignores the pointer so the button gets every click.
        private Button CreateButton(TextLine line, out TextLineView view)
        {
            var button = new Button();
            view = new TextLineView(_cargoIcons, Text);
            view.SetLine(line);
            button.Add(view);
            return button;
        }

        // "Route: <name>" where the name is a link that opens the routes window on that route, or "Route: None".
        private VisualElement BuildRouteLine(
            VehicleListItem item,
            out Label? routeLink,
            out EventCallback<ClickEvent>? routeHandler)
        {
            var line = new VisualElement();
            line.AddToClassList("vehicles-row-route");
            routeLink = null;
            routeHandler = null;
            if (item.RouteName == null)
            {
                var none = new Label(Text.Get(RoutesUiKeys.VehiclesRouteNone));
                none.AddToClassList("vehicles-row-summary");
                line.Add(none);
                return line;
            }

            var prefix = new Label(Text.Get(RoutesUiKeys.VehiclesRoute) + " ");
            prefix.AddToClassList("vehicles-row-summary");
            line.Add(prefix);

            var link = new Label(item.RouteName);
            link.AddToClassList("vehicles-row-summary");
            link.AddToClassList("vehicles-route-link");
            int routeId = item.RouteId;
            if (routeId != VehicleListItem.NoRouteId)
            {
                EventCallback<ClickEvent> handler = _ => _actions.OpenRoute(routeId);
                link.RegisterCallback(handler);
                routeLink = link;
                routeHandler = handler;
            }

            line.Add(link);
            return line;
        }

        private void Act(Action action)
        {
            action();
            _lastVehicleFingerprint = int.MinValue;
            Refresh(force: true);
        }

        private void ClearRows()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                binding.Sell.clicked -= binding.SellHandler;
                if (binding.AddWagon != null && binding.AddWagonHandler != null)
                {
                    binding.AddWagon.clicked -= binding.AddWagonHandler;
                }

                if (binding.SellWagon != null && binding.SellWagonHandler != null)
                {
                    binding.SellWagon.clicked -= binding.SellWagonHandler;
                }

                if (binding.RouteLink != null && binding.RouteHandler != null)
                {
                    binding.RouteLink.UnregisterCallback(binding.RouteHandler);
                }
            }

            _rows.Clear();
        }

        private sealed class RowBinding
        {
            public RowBinding(
                Label title,
                TextLineView cargo,
                TextLineView? addWagonLine,
                Button sell,
                Action sellHandler,
                Button? addWagon,
                Action? addWagonHandler,
                Button? sellWagon,
                Action? sellWagonHandler,
                Label? routeLink,
                EventCallback<ClickEvent>? routeHandler)
            {
                Title = title;
                Cargo = cargo;
                AddWagonLine = addWagonLine;
                Sell = sell;
                SellHandler = sellHandler;
                AddWagon = addWagon;
                AddWagonHandler = addWagonHandler;
                SellWagon = sellWagon;
                SellWagonHandler = sellWagonHandler;
                RouteLink = routeLink;
                RouteHandler = routeHandler;
            }

            public Label Title { get; }

            public TextLineView Cargo { get; }

            public TextLineView? AddWagonLine { get; }

            public Button Sell { get; }

            public Action SellHandler { get; }

            public Button? AddWagon { get; }

            public Action? AddWagonHandler { get; }

            public Button? SellWagon { get; }

            public Action? SellWagonHandler { get; }

            public Label? RouteLink { get; }

            public EventCallback<ClickEvent>? RouteHandler { get; }
        }
    }
}
