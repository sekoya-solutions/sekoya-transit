#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Callbacks the vehicles panel raises toward Presentation (Issue #178, routes in Issue #229).
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
            Action manageRoutes)
        {
            SellVehicle = sellVehicle ?? throw new ArgumentNullException(nameof(sellVehicle));
            AddRailWagon = addRailWagon ?? throw new ArgumentNullException(nameof(addRailWagon));
            SellRailWagon = sellRailWagon ?? throw new ArgumentNullException(nameof(sellRailWagon));
            ManageRoutes = manageRoutes ?? throw new ArgumentNullException(nameof(manageRoutes));
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
        /// Gets the "Manage routes" callback (opens the routes window on the routes of the selected station).
        /// </summary>
        public Action ManageRoutes { get; }
    }

    /// <summary>
    /// Vehicles list panel (Issue #178): one row per owned vehicle with its route, sell and wagon buttons. While a
    /// station is selected on the map it lists the vehicles of the routes that stop there and offers "Manage routes"
    /// (Issue #229); vehicles are bought from the routes window.
    /// </summary>
    public sealed class VehiclesPanelController : ShellPanelController
    {
        private readonly Label _stationHint;
        private readonly Button _manageRoutes;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<VehicleListItem>> _vehiclesProvider;
        private readonly Func<VehiclesPanelState> _stateProvider;
        private readonly VehiclesPanelActions _actions;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private readonly Action _manageRoutesHandler;
        private int _lastVehicleFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the vehicles panel elements.
        /// </summary>
        public VehiclesPanelController(
            VisualElement panel,
            Label title,
            Label stationHint,
            Button manageRoutes,
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
            _manageRoutes = manageRoutes ?? throw new ArgumentNullException(nameof(manageRoutes));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _vehiclesProvider = vehiclesProvider ?? throw new ArgumentNullException(nameof(vehiclesProvider));
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _cargoIcons = cargoIcons;
            _manageRoutesHandler = () => _actions.ManageRoutes();
            _manageRoutes.clicked += _manageRoutesHandler;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _manageRoutes.clicked -= _manageRoutesHandler;
            ClearRows();
            _lastVehicleFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            VehiclesPanelState state = _stateProvider();
            _empty.text = Text.Get(GameUiKeys.VehiclesEmpty);
            _stationHint.text = Text.Get(RoutesUiKeys.VehiclesStationHint);
            _manageRoutes.text = Text.Get(RoutesUiKeys.VehiclesManageRoutes);
            DisplayStyle stationDisplay = state.HasSelectedStation ? DisplayStyle.Flex : DisplayStyle.None;
            _stationHint.style.display = stationDisplay;
            _manageRoutes.style.display = stationDisplay;
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
                _rows[index].Summary.SetLine(VehiclesViewModel.FormatRow(item, Text, culture));
                if (_rows[index].AddWagon != null)
                {
                    _rows[index].AddWagon!.text = VehiclesViewModel.FormatAddWagon(state.WagonPurchasePrice, Text, culture);
                    _rows[index].AddWagon!.SetEnabled(item.WagonCount < item.MaxWagons && state.CanAffordWagon);
                }
            }
        }

        private void RebuildRows(IReadOnlyList<VehicleListItem> vehicles, VehiclesPanelState state, CultureInfo culture)
        {
            ClearRows();
            _list.Clear();
            _empty.style.display = vehicles.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                var row = new VisualElement();
                row.AddToClassList("vehicles-row");

                var summary = new TextLineView(_cargoIcons);
                summary.SetLine(VehiclesViewModel.FormatRow(item, Text, culture));
                summary.AddToClassList("vehicles-row-summary");
                row.Add(summary);

                VehicleListKind kind = item.Kind;
                int vehicleId = item.Id;
                Button? addWagon = null;
                Action? addWagonHandler = null;
                Button? sellWagon = null;
                Action? sellWagonHandler = null;
                if (kind == VehicleListKind.Rail)
                {
                    addWagon = new Button { text = VehiclesViewModel.FormatAddWagon(state.WagonPurchasePrice, Text, culture) };
                    addWagon.AddToClassList("vehicles-row-sell");
                    addWagon.SetEnabled(item.WagonCount < item.MaxWagons && state.CanAffordWagon);
                    addWagonHandler = () => Act(() => _actions.AddRailWagon(vehicleId));
                    addWagon.clicked += addWagonHandler;
                    row.Add(addWagon);

                    sellWagon = new Button { text = Text.Get(GameUiKeys.VehiclesSellWagon) };
                    sellWagon.AddToClassList("vehicles-row-sell");
                    sellWagon.SetEnabled(item.WagonCount > 0);
                    sellWagonHandler = () => Act(() => _actions.SellRailWagon(vehicleId));
                    sellWagon.clicked += sellWagonHandler;
                    row.Add(sellWagon);
                }

                var sell = new Button { text = Text.Get(GameUiKeys.VehiclesSell) };
                sell.AddToClassList("vehicles-row-sell");
                Action sellHandler = () => Act(() => _actions.SellVehicle(kind, vehicleId));
                sell.clicked += sellHandler;
                row.Add(sell);
                _list.Add(row);
                _rows.Add(new RowBinding(summary, sell, sellHandler, addWagon, addWagonHandler, sellWagon, sellWagonHandler));
            }
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
            }

            _rows.Clear();
        }

        private sealed class RowBinding
        {
            public RowBinding(
                TextLineView summary,
                Button sell,
                Action sellHandler,
                Button? addWagon,
                Action? addWagonHandler,
                Button? sellWagon,
                Action? sellWagonHandler)
            {
                Summary = summary;
                Sell = sell;
                SellHandler = sellHandler;
                AddWagon = addWagon;
                AddWagonHandler = addWagonHandler;
                SellWagon = sellWagon;
                SellWagonHandler = sellWagonHandler;
            }

            public TextLineView Summary { get; }

            public Button Sell { get; }

            public Action SellHandler { get; }

            public Button? AddWagon { get; }

            public Action? AddWagonHandler { get; }

            public Button? SellWagon { get; }

            public Action? SellWagonHandler { get; }
        }
    }
}
