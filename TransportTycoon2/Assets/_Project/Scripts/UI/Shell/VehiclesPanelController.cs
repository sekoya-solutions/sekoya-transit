#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Callbacks the vehicles panel raises toward Presentation and toward the orders section (Issue #178).
    /// </summary>
    public sealed class VehiclesPanelActions
    {
        /// <summary>
        /// Initializes the callback set.
        /// </summary>
        public VehiclesPanelActions(
            Action<string> buyVehicleType,
            Action<VehicleListKind, int> sellVehicle,
            Action<int> addRailWagon,
            Action<int> sellRailWagon,
            Action<VehicleListKind, int> selectForOrders,
            Action rowsRebuilt,
            Action selectionLost)
        {
            BuyVehicleType = buyVehicleType ?? throw new ArgumentNullException(nameof(buyVehicleType));
            SellVehicle = sellVehicle ?? throw new ArgumentNullException(nameof(sellVehicle));
            AddRailWagon = addRailWagon ?? throw new ArgumentNullException(nameof(addRailWagon));
            SellRailWagon = sellRailWagon ?? throw new ArgumentNullException(nameof(sellRailWagon));
            SelectForOrders = selectForOrders ?? throw new ArgumentNullException(nameof(selectForOrders));
            RowsRebuilt = rowsRebuilt ?? throw new ArgumentNullException(nameof(rowsRebuilt));
            SelectionLost = selectionLost ?? throw new ArgumentNullException(nameof(selectionLost));
        }

        /// <summary>
        /// Gets the buy callback (vehicle type id).
        /// </summary>
        public Action<string> BuyVehicleType { get; }

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
        /// Gets the callback for a click on a vehicle row (start editing its route).
        /// </summary>
        public Action<VehicleListKind, int> SelectForOrders { get; }

        /// <summary>
        /// Gets the callback that runs after the vehicle rows were rebuilt (the orders section repaints).
        /// </summary>
        public Action RowsRebuilt { get; }

        /// <summary>
        /// Gets the callback that runs when the vehicle being edited no longer exists.
        /// </summary>
        public Action SelectionLost { get; }
    }

    /// <summary>
    /// Vehicles list panel (Issue #178): the buy options for the selected station kind, one row per vehicle with
    /// sell and wagon buttons, and the selection that feeds the route (orders) section.
    /// </summary>
    public sealed class VehiclesPanelController : ShellPanelController
    {
        private readonly Label _buyHint;
        private readonly ScrollView _buyList;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<VehicleListItem>> _vehiclesProvider;
        private readonly Func<VehiclePurchaseUiState> _purchaseProvider;
        private readonly Func<VehicleListKind, int, IReadOnlyList<int>> _orderStationIdsProvider;
        private readonly VehicleOrdersDraft _draft;
        private readonly VehiclesPanelActions _actions;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private readonly List<BuyBinding> _buyRows = new List<BuyBinding>();
        private int _lastVehicleFingerprint = int.MinValue;
        private int _lastPurchaseFingerprint = int.MinValue;

        /// <summary>
        /// Initializes the controller over the vehicles panel elements. <paramref name="draft"/> is the route draft
        /// shared with the orders section.
        /// </summary>
        public VehiclesPanelController(
            VisualElement panel,
            Label title,
            Label buyHint,
            ScrollView buyList,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<VehicleListItem>> vehiclesProvider,
            Func<VehiclePurchaseUiState> purchaseProvider,
            Func<VehicleListKind, int, IReadOnlyList<int>> orderStationIdsProvider,
            VehicleOrdersDraft draft,
            VehiclesPanelActions actions,
            ICargoIconSource? cargoIcons,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "vehicles-panel--hidden", title, GameUiKeys.VehiclesTitle, text, culture)
        {
            _buyHint = buyHint ?? throw new ArgumentNullException(nameof(buyHint));
            _buyList = buyList ?? throw new ArgumentNullException(nameof(buyList));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _vehiclesProvider = vehiclesProvider ?? throw new ArgumentNullException(nameof(vehiclesProvider));
            _purchaseProvider = purchaseProvider ?? throw new ArgumentNullException(nameof(purchaseProvider));
            _orderStationIdsProvider = orderStationIdsProvider
                ?? throw new ArgumentNullException(nameof(orderStationIdsProvider));
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _cargoIcons = cargoIcons;
        }

        /// <summary>
        /// Repaints the buy options while the panel is open; the buttons are rebuilt when <paramref name="forceRebuild"/>
        /// is set or the options changed, otherwise only their text and enabled state update.
        /// </summary>
        public void RefreshPurchase(bool forceRebuild)
        {
            if (!IsOpen)
            {
                return;
            }

            CultureInfo culture = Culture();
            VehiclePurchaseUiState purchase = _purchaseProvider();
            if (purchase.Selection == VehiclePurchaseSelection.None || purchase.Options.Count == 0)
            {
                _buyHint.text = Text.Get(GameUiKeys.VehiclesBuyHint);
                _buyHint.style.display = DisplayStyle.Flex;
            }
            else
            {
                _buyHint.style.display = DisplayStyle.None;
            }

            int fingerprint = VehiclesViewModel.ComputePurchaseFingerprint(purchase);
            if (forceRebuild || fingerprint != _lastPurchaseFingerprint)
            {
                _lastPurchaseFingerprint = fingerprint;
                RebuildBuyOptionRows(purchase.Options, culture);
                return;
            }

            for (int index = 0; index < purchase.Options.Count; index++)
            {
                VehiclePurchaseOption option = purchase.Options[index];
                _buyRows[index].Button.text = VehiclesViewModel.FormatBuyOption(option, Text, culture);
                _buyRows[index].Button.SetEnabled(option.CanAfford);
            }
        }

        /// <summary>
        /// Adds or removes the selected modifier on each row and refreshes the "selected" prefix of the row text.
        /// </summary>
        public void RefreshSelectionHighlight()
        {
            IReadOnlyList<VehicleListItem> vehicles = _vehiclesProvider();
            CultureInfo culture = Culture();
            VehiclePurchaseUiState purchase = _purchaseProvider();
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                binding.Row.EnableInClassList(
                    "vehicles-row--selected",
                    IsSelectedForOrders(binding.Kind, binding.VehicleId));
                bool canEdit = CanEditRoute(binding.Kind, binding.VehicleId, purchase);
                binding.Row.EnableInClassList("vehicles-row--locked", !canEdit);
                binding.Row.style.display = VehicleRouteEditRules.IsListed(purchase.Selection, canEdit)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                // Keep summary text in sync with selection prefix without rebuilding all rows.
                for (int vehicleIndex = 0; vehicleIndex < vehicles.Count; vehicleIndex++)
                {
                    VehicleListItem vehicle = vehicles[vehicleIndex];
                    if (vehicle.Kind == binding.Kind && vehicle.Id == binding.VehicleId)
                    {
                        binding.Summary.SetLine(FormatRow(vehicle, culture));
                        break;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            ClearRows();
            ClearBuyRows();
            _lastVehicleFingerprint = int.MinValue;
            _lastPurchaseFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            _empty.text = Text.Get(GameUiKeys.VehiclesEmpty);
            RefreshPurchase(force);
            RefreshVehicles(force);
        }

        private void RefreshVehicles(bool forceRebuild)
        {
            if (!IsOpen)
            {
                return;
            }

            CultureInfo culture = Culture();
            IReadOnlyList<VehicleListItem> vehicles = _vehiclesProvider();
            EnsureSelectedVehicleStillExists(vehicles);
            EnsureSelectedVehicleMatchesStation(_purchaseProvider().Selection);
            int fingerprint = VehiclesViewModel.ComputeVehicleFingerprint(vehicles);
            if (forceRebuild || fingerprint != _lastVehicleFingerprint)
            {
                _lastVehicleFingerprint = fingerprint;
                RebuildRows(vehicles, culture);
                _actions.RowsRebuilt();
                return;
            }

            VehiclePurchaseUiState purchase = _purchaseProvider();
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                _rows[index].Summary.SetLine(FormatRow(item, culture));
                if (_rows[index].AddWagon != null)
                {
                    _rows[index].AddWagon!.text = VehiclesViewModel.FormatAddWagon(
                        purchase.WagonPurchasePrice,
                        Text,
                        culture);
                    _rows[index].AddWagon!.SetEnabled(item.WagonCount < item.MaxWagons && purchase.CanAffordWagon);
                }
            }

            RefreshSelectionHighlight();
        }

        private void EnsureSelectedVehicleStillExists(IReadOnlyList<VehicleListItem> vehicles)
        {
            if (!_draft.VehicleId.HasValue)
            {
                return;
            }

            for (int index = 0; index < vehicles.Count; index++)
            {
                if (IsSelectedForOrders(vehicles[index].Kind, vehicles[index].Id))
                {
                    return;
                }
            }

            _draft.ClearSelection();
            _actions.SelectionLost();
        }

        // Selecting a station of the other kind ends the route edit, so its row hides like the others (Issue #193).
        private void EnsureSelectedVehicleMatchesStation(VehiclePurchaseSelection selectedStationKind)
        {
            if (!_draft.Kind.HasValue
                || VehicleRouteEditRules.KeepsSelection(_draft.Kind.Value, selectedStationKind))
            {
                return;
            }

            _draft.ClearSelection();
            _actions.SelectionLost();
        }

        // The vehicle being edited stays editable while map clicks select stations of its kind (Issue #181).
        private bool CanEditRoute(VehicleListKind kind, int vehicleId, VehiclePurchaseUiState purchase)
        {
            if (IsSelectedForOrders(kind, vehicleId))
            {
                return true;
            }

            if (purchase.Selection == VehiclePurchaseSelection.None)
            {
                return false;
            }

            return VehicleRouteEditRules.CanEditRoute(
                kind,
                _orderStationIdsProvider(kind, vehicleId),
                purchase.Selection,
                purchase.SelectedStationId);
        }

        private bool IsSelectedForOrders(VehicleListKind kind, int vehicleId)
        {
            return _draft.Kind.HasValue
                && _draft.Kind.Value == kind
                && _draft.VehicleId.HasValue
                && _draft.VehicleId.Value == vehicleId;
        }

        private TextLine FormatRow(VehicleListItem item, CultureInfo culture)
        {
            return VehiclesViewModel.FormatRow(item, IsSelectedForOrders(item.Kind, item.Id), Text, culture);
        }

        private void RebuildBuyOptionRows(IReadOnlyList<VehiclePurchaseOption> options, CultureInfo culture)
        {
            ClearBuyRows();
            _buyList.Clear();
            for (int index = 0; index < options.Count; index++)
            {
                VehiclePurchaseOption option = options[index];
                string typeId = option.TypeId;
                var button = new Button { text = VehiclesViewModel.FormatBuyOption(option, Text, culture) };
                button.AddToClassList("vehicles-buy-option");
                button.SetEnabled(option.CanAfford);
                Action clickHandler = () =>
                {
                    _actions.BuyVehicleType(typeId);
                    _lastVehicleFingerprint = int.MinValue;
                    _lastPurchaseFingerprint = int.MinValue;
                    RefreshVehicles(forceRebuild: true);
                    RefreshPurchase(forceRebuild: true);
                };
                button.clicked += clickHandler;
                _buyList.Add(button);
                _buyRows.Add(new BuyBinding(button, clickHandler));
            }
        }

        private void RebuildRows(IReadOnlyList<VehicleListItem> vehicles, CultureInfo culture)
        {
            ClearRows();
            _list.Clear();
            if (vehicles.Count == 0)
            {
                _empty.style.display = DisplayStyle.Flex;
                return;
            }

            _empty.style.display = DisplayStyle.None;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                var row = new VisualElement();
                row.AddToClassList("vehicles-row");

                var summary = new TextLineView(_cargoIcons);
                summary.SetLine(FormatRow(item, culture));
                summary.AddToClassList("vehicles-row-summary");
                row.Add(summary);

                VehicleListKind kind = item.Kind;
                int vehicleId = item.Id;
                EventCallback<ClickEvent> selectHandler = evt =>
                {
                    if (evt.target is Button || !CanEditRoute(kind, vehicleId, _purchaseProvider()))
                    {
                        return;
                    }

                    _actions.SelectForOrders(kind, vehicleId);
                    RefreshSelectionHighlight();
                };
                row.RegisterCallback(selectHandler);

                Button? addWagon = null;
                Action? addWagonHandler = null;
                Button? sellWagon = null;
                Action? sellWagonHandler = null;
                if (kind == VehicleListKind.Rail)
                {
                    VehiclePurchaseUiState purchase = _purchaseProvider();
                    addWagon = new Button
                    {
                        text = VehiclesViewModel.FormatAddWagon(purchase.WagonPurchasePrice, Text, culture)
                    };
                    addWagon.AddToClassList("vehicles-row-sell");
                    addWagon.SetEnabled(item.WagonCount < item.MaxWagons && purchase.CanAffordWagon);
                    addWagonHandler = () =>
                    {
                        _actions.AddRailWagon(vehicleId);
                        _lastVehicleFingerprint = int.MinValue;
                        RefreshVehicles(forceRebuild: true);
                    };
                    addWagon.clicked += addWagonHandler;
                    row.Add(addWagon);

                    sellWagon = new Button { text = Text.Get(GameUiKeys.VehiclesSellWagon) };
                    sellWagon.AddToClassList("vehicles-row-sell");
                    sellWagon.SetEnabled(item.WagonCount > 0);
                    sellWagonHandler = () =>
                    {
                        _actions.SellRailWagon(vehicleId);
                        _lastVehicleFingerprint = int.MinValue;
                        RefreshVehicles(forceRebuild: true);
                    };
                    sellWagon.clicked += sellWagonHandler;
                    row.Add(sellWagon);
                }

                var sell = new Button { text = Text.Get(GameUiKeys.VehiclesSell) };
                sell.AddToClassList("vehicles-row-sell");
                Action sellHandler = () =>
                {
                    if (IsSelectedForOrders(kind, vehicleId))
                    {
                        _draft.ClearSelection();
                    }

                    _actions.SellVehicle(kind, vehicleId);
                    _lastVehicleFingerprint = int.MinValue;
                    RefreshVehicles(forceRebuild: true);
                };
                sell.clicked += sellHandler;
                row.Add(sell);
                _list.Add(row);
                _rows.Add(
                    new RowBinding(
                        kind,
                        vehicleId,
                        row,
                        summary,
                        sell,
                        sellHandler,
                        selectHandler,
                        addWagon,
                        addWagonHandler,
                        sellWagon,
                        sellWagonHandler));
            }

            RefreshSelectionHighlight();
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

                binding.Row.UnregisterCallback(binding.SelectHandler);
            }

            _rows.Clear();
        }

        private void ClearBuyRows()
        {
            for (int index = 0; index < _buyRows.Count; index++)
            {
                BuyBinding binding = _buyRows[index];
                binding.Button.clicked -= binding.ClickHandler;
            }

            _buyRows.Clear();
        }

        private sealed class BuyBinding
        {
            public BuyBinding(Button button, Action clickHandler)
            {
                Button = button;
                ClickHandler = clickHandler;
            }

            public Button Button { get; }

            public Action ClickHandler { get; }
        }

        private sealed class RowBinding
        {
            public RowBinding(
                VehicleListKind kind,
                int vehicleId,
                VisualElement row,
                TextLineView summary,
                Button sell,
                Action sellHandler,
                EventCallback<ClickEvent> selectHandler,
                Button? addWagon,
                Action? addWagonHandler,
                Button? sellWagon,
                Action? sellWagonHandler)
            {
                Kind = kind;
                VehicleId = vehicleId;
                Row = row;
                Summary = summary;
                Sell = sell;
                SellHandler = sellHandler;
                SelectHandler = selectHandler;
                AddWagon = addWagon;
                AddWagonHandler = addWagonHandler;
                SellWagon = sellWagon;
                SellWagonHandler = sellWagonHandler;
            }

            public VehicleListKind Kind { get; }

            public int VehicleId { get; }

            public VisualElement Row { get; }

            public TextLineView Summary { get; }

            public Button Sell { get; }

            public Action SellHandler { get; }

            public EventCallback<ClickEvent> SelectHandler { get; }

            public Button? AddWagon { get; }

            public Action? AddWagonHandler { get; }

            public Button? SellWagon { get; }

            public Action? SellWagonHandler { get; }
        }
    }
}
