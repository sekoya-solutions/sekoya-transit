#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// MapPlay HUD shell: localized cash, hint, build toolbar, vehicles, industries, and towns panels.
    /// Does not mutate simulation state; Presentation supplies data and command callbacks.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapPlayShell : MonoBehaviour
    {
        private static readonly BuildToolId[] ToolbarTools =
        {
            BuildToolId.Select,
            BuildToolId.Road,
            BuildToolId.DemolishRoad,
            BuildToolId.Rail,
            BuildToolId.DemolishRail,
            BuildToolId.Station,
            BuildToolId.DemolishStation,
            BuildToolId.RailStation,
            BuildToolId.DemolishRailStation,
        };

        private Label? _cashLabel;
        private Label? _hintLabel;
        private Button? _vehiclesToggle;
        private Button? _industriesToggle;
        private Button? _townsToggle;
        private VisualElement? _vehiclesPanel;
        private VisualElement? _industriesPanel;
        private VisualElement? _townsPanel;
        private Label? _vehiclesTitle;
        private Label? _industriesTitle;
        private Label? _townsTitle;
        private Button? _vehiclesBuy;
        private ScrollView? _vehiclesList;
        private ScrollView? _industriesList;
        private ScrollView? _townsList;
        private Label? _vehiclesEmpty;
        private Label? _industriesEmpty;
        private Label? _townsEmpty;
        private Label? _ordersTitle;
        private Label? _ordersHint;
        private Label? _ordersSelected;
        private ScrollView? _ordersList;
        private Label? _ordersEmpty;
        private Button? _ordersApply;
        private Label? _ordersStatus;
        private VisualElement? _buildToolbar;
        private VisualElement? _hudPanel;
        private VisualElement? _root;
        private readonly Dictionary<BuildToolId, Button> _toolButtons = new Dictionary<BuildToolId, Button>();
        private readonly Dictionary<BuildToolId, Action> _toolClickHandlers = new Dictionary<BuildToolId, Action>();
        private readonly List<VehicleRowBindings> _vehicleRows = new List<VehicleRowBindings>();
        private readonly List<OrderRowBindings> _orderRows = new List<OrderRowBindings>();
        private readonly List<IndustryRowBindings> _industryRows = new List<IndustryRowBindings>();
        private readonly List<TownRowBindings> _townRowBindings = new List<TownRowBindings>();
        private readonly Dictionary<string, string> _industryNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly VehicleOrdersDraft _ordersDraft = new VehicleOrdersDraft();
        private bool _pointerOverChrome;
        private int _blockMapInputUntilFrame = -1;
        private Func<long>? _cashProvider;
        private Action<BuildToolId>? _onToolChosen;
        private Func<IReadOnlyList<VehicleListItem>>? _vehiclesProvider;
        private Func<IReadOnlyList<IndustryListItem>>? _industriesProvider;
        private Func<IReadOnlyList<TownListItem>>? _townsProvider;
        private Action<int>? _onTownRowSelected;
        private Action<string, int>? _onIndustryRowSelected;
        private Action? _onBuyVehicle;
        private Action<VehicleListKind, int>? _onSellVehicle;
        private Action<int>? _onAddRailWagon;
        private Action<int>? _onSellRailWagon;
        private Func<VehicleListKind, int, IReadOnlyList<int>>? _getVehicleOrderStationIds;
        private Func<VehicleListKind, int, IReadOnlyList<int>, bool>? _tryApplyVehicleOrders;
        private string _cashTemplate = "Cash: {0}";
        private string _rowTemplate = "#{0} · {1} · Cargo {2}/{3} · Income {4}";
        private string _industryRowTemplate = "{0} #{1} · L{2} · Stock {3}/{4} · Produced {5} · Transported {6}";
        private string _townSummaryTemplate = "Town #{0} · Pop {1} · Growth {2}/{3}";
        private string _townNeedLineTemplate = "{0} {1}/{2}";
        private string _townPassengerLineTemplate = "Passengers · Generated {0} · Transported {1}";
        private string _townMailLineTemplate = "Mail · Generated {0} · Transported {1}";
        private string _ordersSelectedTemplate = "Editing vehicle #{0}";
        private string _ordersStopTemplate = "#{0} Station {1}";
        private string _statusParked = "Parked";
        private string _statusMoving = "Moving";
        private string _statusLoading = "Loading";
        private string _statusStuck = "Stuck";
        private string _sellLabel = "Sell";
        private string _addWagonLabel = "Add wagon";
        private string _sellWagonLabel = "Sell wagon";
        private string _rowRailTemplate = "#{0} · {1} · {2} · Wagons {3}/{4} · Cargo {5}/{6} · Income {7}";
        private string _kindRoadLabel = "Truck";
        private string _kindRailLabel = "Train";
        private string _ordersUpLabel = "Up";
        private string _ordersDownLabel = "Down";
        private string _ordersRemoveLabel = "Remove";
        private string _ordersAppliedMessage = "Route applied.";
        private string _ordersRejectedMessage = "Could not apply route (draft kept).";
        private string _ordersNoneSelectedMessage = "Select a vehicle to edit its route.";
        private string _ordersEmptyMessage = "No stops yet. Click stations on the map to append.";
        private CultureInfo _culture = CultureInfo.GetCultureInfo("en");
        private long _lastCash = long.MinValue;
        private BuildToolId _activeTool = BuildToolId.Select;
        private bool _localizationReady;
        private bool _uiReady;
        private bool _vehiclesPanelOpen;
        private bool _industriesPanelOpen;
        private bool _townsPanelOpen;
        private int _lastVehicleIdFingerprint = int.MinValue;
        private int _lastIndustryFingerprint = int.MinValue;
        private int _lastTownFingerprint = int.MinValue;
        private int? _selectedTownId;
        private string? _selectedIndustryId;
        private int _selectedIndustryInstance;

        /// <summary>
        /// Binds cash, tool-selection, town/industry/vehicle list, and vehicle-order callbacks from Presentation.
        /// </summary>
        public void Bind(
            Func<long> cashProvider,
            Action<BuildToolId> onToolChosen,
            Func<IReadOnlyList<VehicleListItem>> vehiclesProvider,
            Func<IReadOnlyList<IndustryListItem>> industriesProvider,
            Action onBuyVehicle,
            Action<VehicleListKind, int> onSellVehicle,
            Action<int> onAddRailWagon,
            Action<int> onSellRailWagon,
            Func<VehicleListKind, int, IReadOnlyList<int>> getVehicleOrderStationIds,
            Func<VehicleListKind, int, IReadOnlyList<int>, bool> tryApplyVehicleOrders,
            Func<IReadOnlyList<TownListItem>> townsProvider,
            Action<int> onTownRowSelected,
            Action<string, int> onIndustryRowSelected)
        {
            _cashProvider = cashProvider ?? throw new ArgumentNullException(nameof(cashProvider));
            _onToolChosen = onToolChosen ?? throw new ArgumentNullException(nameof(onToolChosen));
            _vehiclesProvider = vehiclesProvider ?? throw new ArgumentNullException(nameof(vehiclesProvider));
            _industriesProvider = industriesProvider ?? throw new ArgumentNullException(nameof(industriesProvider));
            _townsProvider = townsProvider ?? throw new ArgumentNullException(nameof(townsProvider));
            _onTownRowSelected = onTownRowSelected ?? throw new ArgumentNullException(nameof(onTownRowSelected));
            _onIndustryRowSelected = onIndustryRowSelected
                ?? throw new ArgumentNullException(nameof(onIndustryRowSelected));
            _onBuyVehicle = onBuyVehicle ?? throw new ArgumentNullException(nameof(onBuyVehicle));
            _onSellVehicle = onSellVehicle ?? throw new ArgumentNullException(nameof(onSellVehicle));
            _onAddRailWagon = onAddRailWagon ?? throw new ArgumentNullException(nameof(onAddRailWagon));
            _onSellRailWagon = onSellRailWagon ?? throw new ArgumentNullException(nameof(onSellRailWagon));
            _getVehicleOrderStationIds = getVehicleOrderStationIds
                ?? throw new ArgumentNullException(nameof(getVehicleOrderStationIds));
            _tryApplyVehicleOrders = tryApplyVehicleOrders
                ?? throw new ArgumentNullException(nameof(tryApplyVehicleOrders));
            _lastCash = long.MinValue;
            _lastVehicleIdFingerprint = int.MinValue;
            _lastIndustryFingerprint = int.MinValue;
            _lastTownFingerprint = int.MinValue;
            _selectedTownId = null;
            _selectedIndustryId = null;
            _selectedIndustryInstance = 0;
            _ordersDraft.ClearSelection();
        }

        /// <summary>
        /// Updates toolbar highlight and hint for the active tool (keyboard or UI).
        /// </summary>
        public void SetActiveBuildTool(BuildToolId tool)
        {
            _activeTool = tool;
            RefreshToolHighlight();
            RefreshHint();
        }

        /// <summary>
        /// Opens or closes the vehicles panel (shell button or keyboard L).
        /// </summary>
        public void ToggleVehiclesPanel()
        {
            SetVehiclesPanelOpen(!_vehiclesPanelOpen);
        }

        /// <summary>
        /// Opens or closes the industries panel (shell button or keyboard I).
        /// </summary>
        public void ToggleIndustriesPanel()
        {
            SetIndustriesPanelOpen(!_industriesPanelOpen);
        }

        /// <summary>
        /// Opens or closes the towns panel (shell button or keyboard T).
        /// </summary>
        public void ToggleTownsPanel()
        {
            SetTownsPanelOpen(!_townsPanelOpen);
        }

        /// <summary>
        /// Sets vehicles panel visibility. Opening closes the industries and towns panels.
        /// </summary>
        public void SetVehiclesPanelOpen(bool open)
        {
            if (open)
            {
                HideIndustriesPanelOnly();
                HideTownsPanelOnly();
            }

            _vehiclesPanelOpen = open;
            if (_vehiclesPanel == null)
            {
                return;
            }

            if (open)
            {
                _vehiclesPanel.RemoveFromClassList("vehicles-panel--hidden");
                _lastVehicleIdFingerprint = int.MinValue;
                RefreshVehicles(forceRebuild: true);
                RefreshOrders(forceRebuild: true);
            }
            else
            {
                _vehiclesPanel.AddToClassList("vehicles-panel--hidden");
            }
        }

        /// <summary>
        /// Sets industries panel visibility. Opening closes the vehicles and towns panels.
        /// </summary>
        public void SetIndustriesPanelOpen(bool open)
        {
            if (open)
            {
                HideVehiclesPanelOnly();
                HideTownsPanelOnly();
            }

            _industriesPanelOpen = open;
            if (_industriesPanel == null)
            {
                return;
            }

            if (open)
            {
                _industriesPanel.RemoveFromClassList("industries-panel--hidden");
                _lastIndustryFingerprint = int.MinValue;
                RefreshIndustries(forceRebuild: true);
            }
            else
            {
                _industriesPanel.AddToClassList("industries-panel--hidden");
            }
        }

        /// <summary>
        /// Sets towns panel visibility. Opening closes the vehicles and industries panels.
        /// </summary>
        public void SetTownsPanelOpen(bool open)
        {
            if (open)
            {
                HideVehiclesPanelOnly();
                HideIndustriesPanelOnly();
            }

            _townsPanelOpen = open;
            if (_townsPanel == null)
            {
                return;
            }

            if (open)
            {
                _townsPanel.RemoveFromClassList("towns-panel--hidden");
                _lastTownFingerprint = int.MinValue;
                RefreshTowns(forceRebuild: true);
            }
            else
            {
                _townsPanel.AddToClassList("towns-panel--hidden");
            }
        }

        /// <summary>
        /// Gets whether the vehicles panel is open.
        /// </summary>
        public bool IsVehiclesPanelOpen => _vehiclesPanelOpen;

        /// <summary>
        /// Gets whether the industries panel is open.
        /// </summary>
        public bool IsIndustriesPanelOpen => _industriesPanelOpen;

        /// <summary>
        /// Gets whether the towns panel is open.
        /// </summary>
        public bool IsTownsPanelOpen => _townsPanelOpen;

        /// <summary>
        /// Gets the town id selected in the towns list, if any.
        /// </summary>
        public int? SelectedTownId => _selectedTownId;

        /// <summary>
        /// Gets whether an industry row is selected in the industries list.
        /// </summary>
        public bool HasSelectedIndustry => _selectedIndustryId != null;

        /// <summary>
        /// Opens the towns panel and selects the given town row.
        /// </summary>
        public void SelectTownInList(int townId)
        {
            _selectedTownId = townId;
            _selectedIndustryId = null;
            _selectedIndustryInstance = 0;
            SetTownsPanelOpen(true);
            _lastTownFingerprint = int.MinValue;
            RefreshTowns(forceRebuild: true);
            if (_industriesPanelOpen)
            {
                RefreshIndustrySelectionHighlight();
            }
        }

        /// <summary>
        /// Opens the industries panel and selects the given industry instance row.
        /// </summary>
        public void SelectIndustryInList(string industryId, int instanceIndex)
        {
            _selectedIndustryId = industryId ?? throw new ArgumentNullException(nameof(industryId));
            _selectedIndustryInstance = instanceIndex;
            _selectedTownId = null;
            SetIndustriesPanelOpen(true);
            _lastIndustryFingerprint = int.MinValue;
            RefreshIndustries(forceRebuild: true);
            if (_townsPanelOpen)
            {
                RefreshTownSelectionHighlight();
            }
        }

        /// <summary>
        /// Clears town and industry list selection and refreshes row highlights when panels are open.
        /// </summary>
        public void ClearListSelection()
        {
            _selectedTownId = null;
            _selectedIndustryId = null;
            _selectedIndustryInstance = 0;
            if (_townsPanelOpen)
            {
                RefreshTownSelectionHighlight();
            }

            if (_industriesPanelOpen)
            {
                RefreshIndustrySelectionHighlight();
            }
        }

        /// <summary>
        /// Gets whether a vehicle is selected for route editing while the vehicles panel is open.
        /// </summary>
        public bool IsEditingVehicleOrders =>
            _vehiclesPanelOpen && _ordersDraft.HasSelection;

        /// <summary>
        /// Gets the vehicle id selected for route editing, or null when none / panel closed.
        /// </summary>
        public int? SelectedVehicleIdForOrders =>
            _vehiclesPanelOpen && _ordersDraft.VehicleId.HasValue ? _ordersDraft.VehicleId : null;

        /// <summary>
        /// Gets the vehicle kind selected for route editing, or null when none / panel closed.
        /// </summary>
        public VehicleListKind? SelectedVehicleKindForOrders =>
            _vehiclesPanelOpen && _ordersDraft.Kind.HasValue ? _ordersDraft.Kind : null;

        /// <summary>
        /// Appends a station to the selected vehicle's draft route (map click while editing).
        /// Station kind must match the selected vehicle kind (road↔road station, rail↔rail station).
        /// </summary>
        /// <returns>True when the station was appended.</returns>
        public bool TryAppendStationToSelectedOrders(VehicleListKind stationKind, int stationId)
        {
            if (!IsEditingVehicleOrders
                || !_ordersDraft.Kind.HasValue
                || _ordersDraft.Kind.Value != stationKind)
            {
                return false;
            }

            if (!_ordersDraft.TryAppend(stationId))
            {
                return false;
            }

            if (_ordersStatus != null)
            {
                _ordersStatus.text = string.Empty;
            }

            RefreshOrders(forceRebuild: true);
            return true;
        }

        /// <summary>
        /// Returns whether a screen-space pointer is over shell chrome (panels, toolbar, HUD controls).
        /// Used to block map clicks and camera scroll-zoom while scrolling UI Toolkit lists.
        /// </summary>
        /// <remarks>
        /// WebGL: do not use <see cref="RuntimePanelUtils.ScreenToPanel"/> geometry fallbacks — Y/canvas
        /// mismatches create invisible dead zones that mirror the HUD and toolbar. Chrome tracking uses
        /// UITK PointerEnter/Leave/Move on the interactive panels instead.
        /// </remarks>
        public bool IsScreenPositionOverInteractiveUi(Vector2 screenPosition)
        {
            // screenPosition is part of the camera/map callback contract; chrome uses UITK enter/leave.
            _ = screenPosition;
            if (!_uiReady || _root == null)
            {
                return false;
            }

            return _pointerOverChrome || Time.frameCount <= _blockMapInputUntilFrame;
        }

        private IEnumerator Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;
            _cashLabel = _root.Q<Label>("cash-label");
            _hintLabel = _root.Q<Label>("hint-label");
            _vehiclesToggle = _root.Q<Button>("vehicles-toggle");
            _industriesToggle = _root.Q<Button>("industries-toggle");
            _townsToggle = _root.Q<Button>("towns-toggle");
            _vehiclesPanel = _root.Q<VisualElement>("vehicles-panel");
            _industriesPanel = _root.Q<VisualElement>("industries-panel");
            _townsPanel = _root.Q<VisualElement>("towns-panel");
            _vehiclesTitle = _root.Q<Label>("vehicles-title");
            _industriesTitle = _root.Q<Label>("industries-title");
            _townsTitle = _root.Q<Label>("towns-title");
            _vehiclesBuy = _root.Q<Button>("vehicles-buy");
            _vehiclesList = _root.Q<ScrollView>("vehicles-list");
            _industriesList = _root.Q<ScrollView>("industries-list");
            _townsList = _root.Q<ScrollView>("towns-list");
            _vehiclesEmpty = _root.Q<Label>("vehicles-empty");
            _industriesEmpty = _root.Q<Label>("industries-empty");
            _townsEmpty = _root.Q<Label>("towns-empty");
            _ordersTitle = _root.Q<Label>("orders-title");
            _ordersHint = _root.Q<Label>("orders-hint");
            _ordersSelected = _root.Q<Label>("orders-selected");
            _ordersList = _root.Q<ScrollView>("orders-list");
            _ordersEmpty = _root.Q<Label>("orders-empty");
            _ordersApply = _root.Q<Button>("orders-apply");
            _ordersStatus = _root.Q<Label>("orders-status");
            _buildToolbar = _root.Q("build-toolbar");
            _hudPanel = _root.Q("hud-panel");
            if (_cashLabel == null
                || _hintLabel == null
                || _vehiclesToggle == null
                || _industriesToggle == null
                || _townsToggle == null
                || _vehiclesPanel == null
                || _industriesPanel == null
                || _townsPanel == null
                || _vehiclesTitle == null
                || _industriesTitle == null
                || _townsTitle == null
                || _vehiclesBuy == null
                || _vehiclesList == null
                || _industriesList == null
                || _townsList == null
                || _vehiclesEmpty == null
                || _industriesEmpty == null
                || _townsEmpty == null
                || _ordersTitle == null
                || _ordersHint == null
                || _ordersSelected == null
                || _ordersList == null
                || _ordersEmpty == null
                || _ordersApply == null
                || _ordersStatus == null
                || _buildToolbar == null
                || _hudPanel == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define cash, hint, vehicles (including orders), industries, towns, HUD, and toolbar elements.");
            }

            BindToolbarButtons(_root);
            _hudPanel.pickingMode = PickingMode.Position;
            _buildToolbar.pickingMode = PickingMode.Position;
            _vehiclesPanel.pickingMode = PickingMode.Position;
            _industriesPanel.pickingMode = PickingMode.Position;
            _townsPanel.pickingMode = PickingMode.Position;
            // TrickleDown on root so we see pointer targets under Ignore shell-root (map pass-through).
            _root.RegisterCallback<PointerMoveEvent>(OnShellPointerMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerDownEvent>(OnShellPointerDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerLeaveEvent>(OnShellPointerLeave);
            RegisterChromePointerTracking(_hudPanel);
            RegisterChromePointerTracking(_buildToolbar);
            RegisterChromePointerTracking(_vehiclesPanel);
            RegisterChromePointerTracking(_industriesPanel);
            RegisterChromePointerTracking(_townsPanel);
            _vehiclesToggle.clicked += OnVehiclesToggleClicked;
            _industriesToggle.clicked += OnIndustriesToggleClicked;
            _townsToggle.clicked += OnTownsToggleClicked;
            _vehiclesBuy.clicked += OnVehiclesBuyClicked;
            _ordersApply.clicked += OnOrdersApplyClicked;
            SetVehiclesPanelOpen(false);
            SetIndustriesPanelOpen(false);
            SetTownsPanelOpen(false);
            _uiReady = true;

            yield return LocalizationSettings.InitializationOperation;
            ApplyCultureFromLocale(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            RefreshStaticTexts();
            _localizationReady = true;
            RefreshCash(force: true);
            RefreshHint();
            RefreshToolHighlight();
            RefreshVehicles(forceRebuild: true);
            RefreshIndustries(forceRebuild: true);
            RefreshTowns(forceRebuild: true);
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            foreach (KeyValuePair<BuildToolId, Button> pair in _toolButtons)
            {
                if (_toolClickHandlers.TryGetValue(pair.Key, out Action handler))
                {
                    pair.Value.clicked -= handler;
                }
            }

            if (_vehiclesToggle != null)
            {
                _vehiclesToggle.clicked -= OnVehiclesToggleClicked;
            }

            if (_industriesToggle != null)
            {
                _industriesToggle.clicked -= OnIndustriesToggleClicked;
            }

            if (_townsToggle != null)
            {
                _townsToggle.clicked -= OnTownsToggleClicked;
            }

            if (_vehiclesBuy != null)
            {
                _vehiclesBuy.clicked -= OnVehiclesBuyClicked;
            }

            if (_ordersApply != null)
            {
                _ordersApply.clicked -= OnOrdersApplyClicked;
            }

            if (_root != null)
            {
                _root.UnregisterCallback<PointerMoveEvent>(OnShellPointerMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerDownEvent>(OnShellPointerDown, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerLeaveEvent>(OnShellPointerLeave);
            }

            UnregisterChromePointerTracking(_hudPanel);
            UnregisterChromePointerTracking(_buildToolbar);
            UnregisterChromePointerTracking(_vehiclesPanel);
            UnregisterChromePointerTracking(_industriesPanel);
            UnregisterChromePointerTracking(_townsPanel);

            ClearVehicleRows();
            ClearOrderRows();
            ClearIndustryRows();
            ClearTownRows();
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            _cashProvider = null;
            _onToolChosen = null;
            _vehiclesProvider = null;
            _industriesProvider = null;
            _townsProvider = null;
            _onTownRowSelected = null;
            _onIndustryRowSelected = null;
            _onBuyVehicle = null;
            _onSellVehicle = null;
            _onAddRailWagon = null;
            _onSellRailWagon = null;
            _getVehicleOrderStationIds = null;
            _tryApplyVehicleOrders = null;
            _ordersDraft.ClearSelection();
            _cashLabel = null;
            _hintLabel = null;
            _vehiclesToggle = null;
            _industriesToggle = null;
            _townsToggle = null;
            _vehiclesPanel = null;
            _industriesPanel = null;
            _townsPanel = null;
            _vehiclesTitle = null;
            _industriesTitle = null;
            _townsTitle = null;
            _vehiclesBuy = null;
            _vehiclesList = null;
            _industriesList = null;
            _vehiclesEmpty = null;
            _industriesEmpty = null;
            _townsList = null;
            _townsEmpty = null;
            _ordersTitle = null;
            _ordersHint = null;
            _ordersSelected = null;
            _ordersList = null;
            _ordersEmpty = null;
            _ordersApply = null;
            _ordersStatus = null;
            _buildToolbar = null;
            _hudPanel = null;
            _root = null;
            _pointerOverChrome = false;
            _blockMapInputUntilFrame = -1;
            _uiReady = false;
        }

        private void OnShellPointerMove(PointerMoveEvent evt)
        {
            _pointerOverChrome = IsUnderChrome(evt.target as VisualElement);
        }

        private void OnShellPointerDown(PointerDownEvent evt)
        {
            if (!IsUnderChrome(evt.target as VisualElement))
            {
                return;
            }

            _pointerOverChrome = true;
            // Block map click this frame and next (map Update may run before UITK click handlers).
            _blockMapInputUntilFrame = Time.frameCount + 1;
        }

        private void OnShellPointerLeave(PointerLeaveEvent evt)
        {
            if (evt.target == _root)
            {
                _pointerOverChrome = false;
            }
        }

        private void OnChromePointerEnter(PointerEnterEvent evt)
        {
            _pointerOverChrome = true;
        }

        private void OnChromePointerLeave(PointerLeaveEvent evt)
        {
            // Leaving a chrome panel for the map (or another non-chrome area) clears the block.
            // Moving between chrome children still fires leave on the child, not the panel.
            if (evt.target == _hudPanel
                || evt.target == _buildToolbar
                || evt.target == _vehiclesPanel
                || evt.target == _industriesPanel
                || evt.target == _townsPanel)
            {
                _pointerOverChrome = false;
            }
        }

        private void RegisterChromePointerTracking(VisualElement panel)
        {
            panel.RegisterCallback<PointerEnterEvent>(OnChromePointerEnter);
            panel.RegisterCallback<PointerLeaveEvent>(OnChromePointerLeave);
        }

        private void UnregisterChromePointerTracking(VisualElement? panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.UnregisterCallback<PointerEnterEvent>(OnChromePointerEnter);
            panel.UnregisterCallback<PointerLeaveEvent>(OnChromePointerLeave);
        }

        private bool IsUnderChrome(VisualElement? element)
        {
            VisualElement? current = element;
            while (current != null && current != _root)
            {
                if (current == _buildToolbar
                    || current == _hudPanel
                    || current == _vehiclesPanel
                    || current == _industriesPanel
                    || current == _townsPanel
                    || current is Button)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void Update()
        {
            RefreshCash(force: false);
            if (_vehiclesPanelOpen)
            {
                RefreshVehicles(forceRebuild: false);
            }

            if (_industriesPanelOpen)
            {
                RefreshIndustries(forceRebuild: false);
            }

            if (_townsPanelOpen)
            {
                RefreshTowns(forceRebuild: false);
            }
        }

        private void OnVehiclesToggleClicked()
        {
            ToggleVehiclesPanel();
        }

        private void OnIndustriesToggleClicked()
        {
            ToggleIndustriesPanel();
        }

        private void OnTownsToggleClicked()
        {
            ToggleTownsPanel();
        }

        private void OnVehiclesBuyClicked()
        {
            _onBuyVehicle?.Invoke();
            _lastVehicleIdFingerprint = int.MinValue;
            RefreshVehicles(forceRebuild: true);
        }

        private void HideVehiclesPanelOnly()
        {
            _vehiclesPanelOpen = false;
            _vehiclesPanel?.AddToClassList("vehicles-panel--hidden");
        }

        private void OnOrdersApplyClicked()
        {
            if (!_ordersDraft.HasSelection
                || !_ordersDraft.VehicleId.HasValue
                || _tryApplyVehicleOrders == null)
            {
                return;
            }

            int vehicleId = _ordersDraft.VehicleId.Value;
            VehicleListKind kind = _ordersDraft.Kind!.Value;
            bool accepted = _tryApplyVehicleOrders(kind, vehicleId, _ordersDraft.ToStationIdArray());
            if (_ordersStatus != null)
            {
                _ordersStatus.text = accepted ? _ordersAppliedMessage : _ordersRejectedMessage;
            }

            if (accepted && _getVehicleOrderStationIds != null)
            {
                _ordersDraft.SelectVehicle(kind, vehicleId, _getVehicleOrderStationIds(kind, vehicleId));
                RefreshOrders(forceRebuild: true);
            }
        }

        private void SelectVehicleForOrders(VehicleListKind kind, int vehicleId)
        {
            if (_getVehicleOrderStationIds == null)
            {
                return;
            }

            _ordersDraft.SelectVehicle(kind, vehicleId, _getVehicleOrderStationIds(kind, vehicleId));
            if (_ordersStatus != null)
            {
                _ordersStatus.text = string.Empty;
            }

            RefreshVehicleSelectionHighlight();
            RefreshOrders(forceRebuild: true);
        }

        private void HideIndustriesPanelOnly()
        {
            _industriesPanelOpen = false;
            _industriesPanel?.AddToClassList("industries-panel--hidden");
        }

        private void HideTownsPanelOnly()
        {
            _townsPanelOpen = false;
            _townsPanel?.AddToClassList("towns-panel--hidden");
        }

        private void BindToolbarButtons(VisualElement root)
        {
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            RegisterToolButton(root, "tool-select", BuildToolId.Select);
            RegisterToolButton(root, "tool-road", BuildToolId.Road);
            RegisterToolButton(root, "tool-demolish-road", BuildToolId.DemolishRoad);
            RegisterToolButton(root, "tool-rail", BuildToolId.Rail);
            RegisterToolButton(root, "tool-demolish-rail", BuildToolId.DemolishRail);
            RegisterToolButton(root, "tool-station", BuildToolId.Station);
            RegisterToolButton(root, "tool-demolish-station", BuildToolId.DemolishStation);
            RegisterToolButton(root, "tool-rail-station", BuildToolId.RailStation);
            RegisterToolButton(root, "tool-demolish-rail-station", BuildToolId.DemolishRailStation);
        }

        private void RegisterToolButton(VisualElement root, string name, BuildToolId tool)
        {
            Button? button = root.Q<Button>(name);
            if (button == null)
            {
                throw new InvalidOperationException("MapPlayShell UXML must define button " + name + ".");
            }

            Action handler = () => _onToolChosen?.Invoke(tool);
            button.clicked += handler;
            _toolButtons[tool] = button;
            _toolClickHandlers[tool] = handler;
        }

        private void OnLocaleChanged(Locale locale)
        {
            ApplyCultureFromLocale(locale);
            RefreshStaticTexts();
            RefreshCash(force: true);
            RefreshHint();
            _lastVehicleIdFingerprint = int.MinValue;
            _lastIndustryFingerprint = int.MinValue;
            _lastTownFingerprint = int.MinValue;
            RefreshVehicles(forceRebuild: true);
            RefreshIndustries(forceRebuild: true);
            RefreshTowns(forceRebuild: true);
        }

        private void ApplyCultureFromLocale(Locale? locale)
        {
            if (locale == null)
            {
                _culture = CultureInfo.GetCultureInfo("en");
                return;
            }

            try
            {
                _culture = CultureInfo.GetCultureInfo(locale.Identifier.Code);
            }
            catch (CultureNotFoundException)
            {
                _culture = CultureInfo.GetCultureInfo("en");
            }
        }

        private void RefreshStaticTexts()
        {
            ApplyLocalizedString(GameUiKeys.Cash, value =>
            {
                _cashTemplate = value;
                RefreshCash(force: true);
            });

            ApplyLocalizedString(GameUiKeys.VehiclesToggle, value =>
            {
                if (_vehiclesToggle != null)
                {
                    _vehiclesToggle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.IndustriesToggle, value =>
            {
                if (_industriesToggle != null)
                {
                    _industriesToggle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.VehiclesTitle, value =>
            {
                if (_vehiclesTitle != null)
                {
                    _vehiclesTitle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.IndustriesTitle, value =>
            {
                if (_industriesTitle != null)
                {
                    _industriesTitle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.VehiclesBuy, value =>
            {
                if (_vehiclesBuy != null)
                {
                    _vehiclesBuy.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.VehiclesEmpty, value =>
            {
                if (_vehiclesEmpty != null)
                {
                    _vehiclesEmpty.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.IndustriesEmpty, value =>
            {
                if (_industriesEmpty != null)
                {
                    _industriesEmpty.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.TownsToggle, value =>
            {
                if (_townsToggle != null)
                {
                    _townsToggle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.TownsTitle, value =>
            {
                if (_townsTitle != null)
                {
                    _townsTitle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.TownsEmpty, value =>
            {
                if (_townsEmpty != null)
                {
                    _townsEmpty.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.TownsSummary, value =>
            {
                _townSummaryTemplate = value;
                RefreshTowns(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.TownsNeedLine, value =>
            {
                _townNeedLineTemplate = value;
                RefreshTowns(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.TownsPassengerLine, value =>
            {
                _townPassengerLineTemplate = value;
                RefreshTowns(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.TownsMailLine, value =>
            {
                _townMailLineTemplate = value;
                RefreshTowns(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.VehiclesRow, value =>
            {
                _rowTemplate = value;
                RefreshVehicles(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.IndustriesRow, value =>
            {
                _industryRowTemplate = value;
                RefreshIndustries(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.VehiclesSell, value =>
            {
                _sellLabel = value;
                for (int index = 0; index < _vehicleRows.Count; index++)
                {
                    _vehicleRows[index].Sell.text = _sellLabel;
                }
            });

            ApplyLocalizedString(GameUiKeys.VehiclesAddWagon, value =>
            {
                _addWagonLabel = value;
                RefreshVehicles(forceRebuild: true);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesSellWagon, value =>
            {
                _sellWagonLabel = value;
                RefreshVehicles(forceRebuild: true);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesRowRail, value =>
            {
                _rowRailTemplate = value;
                RefreshVehicles(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesKindRoad, value =>
            {
                _kindRoadLabel = value;
                RefreshVehicles(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesKindRail, value =>
            {
                _kindRailLabel = value;
                RefreshVehicles(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.VehiclesStatusParked, value =>
            {
                _statusParked = value;
                RefreshVehicles(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesStatusMoving, value =>
            {
                _statusMoving = value;
                RefreshVehicles(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesStatusLoading, value =>
            {
                _statusLoading = value;
                RefreshVehicles(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.VehiclesStatusStuck, value =>
            {
                _statusStuck = value;
                RefreshVehicles(forceRebuild: false);
            });

            ApplyLocalizedString(GameUiKeys.OrdersTitle, value =>
            {
                if (_ordersTitle != null)
                {
                    _ordersTitle.text = value;
                }
            });
            ApplyLocalizedString(GameUiKeys.OrdersHint, value =>
            {
                if (_ordersHint != null)
                {
                    _ordersHint.text = value;
                }
            });
            ApplyLocalizedString(GameUiKeys.OrdersNoneSelected, value =>
            {
                _ordersNoneSelectedMessage = value;
                RefreshOrders(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.OrdersSelected, value =>
            {
                _ordersSelectedTemplate = value;
                RefreshOrders(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.OrdersEmpty, value =>
            {
                _ordersEmptyMessage = value;
                RefreshOrders(forceRebuild: false);
            });
            ApplyLocalizedString(GameUiKeys.OrdersStop, value =>
            {
                _ordersStopTemplate = value;
                RefreshOrders(forceRebuild: true);
            });
            ApplyLocalizedString(GameUiKeys.OrdersUp, value =>
            {
                _ordersUpLabel = value;
                RefreshOrders(forceRebuild: true);
            });
            ApplyLocalizedString(GameUiKeys.OrdersDown, value =>
            {
                _ordersDownLabel = value;
                RefreshOrders(forceRebuild: true);
            });
            ApplyLocalizedString(GameUiKeys.OrdersRemove, value =>
            {
                _ordersRemoveLabel = value;
                RefreshOrders(forceRebuild: true);
            });
            ApplyLocalizedString(GameUiKeys.OrdersApply, value =>
            {
                if (_ordersApply != null)
                {
                    _ordersApply.text = value;
                }
            });
            ApplyLocalizedString(GameUiKeys.OrdersApplied, value =>
            {
                _ordersAppliedMessage = value;
            });
            ApplyLocalizedString(GameUiKeys.OrdersRejected, value =>
            {
                _ordersRejectedMessage = value;
            });

            for (int index = 0; index < GameUiKeys.IndustryNameKeys.Length; index++)
            {
                string key = GameUiKeys.IndustryNameKeys[index];
                ApplyLocalizedString(key, value =>
                {
                    _industryNames[key] = value;
                    RefreshIndustries(forceRebuild: false);
                });
            }

            foreach (BuildToolId tool in ToolbarTools)
            {
                if (!_toolButtons.ContainsKey(tool))
                {
                    continue;
                }

                BuildToolId captured = tool;
                ApplyLocalizedString(GameUiKeys.ToolLabelKey(captured), value =>
                {
                    if (_toolButtons.TryGetValue(captured, out Button live))
                    {
                        live.text = value;
                    }
                });
            }
        }

        private static void ApplyLocalizedString(string key, Action<string> apply)
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(GameUiKeys.Table, key);
            op.Completed += handle =>
            {
                if (!string.IsNullOrEmpty(handle.Result))
                {
                    apply(handle.Result);
                }
            };
        }

        private void RefreshHint()
        {
            if (_hintLabel == null)
            {
                return;
            }

            string key = GameUiKeys.HintKey(_activeTool);
            ApplyLocalizedString(key, value =>
            {
                if (_hintLabel != null)
                {
                    _hintLabel.text = value;
                }
            });
        }

        private void RefreshToolHighlight()
        {
            foreach (KeyValuePair<BuildToolId, Button> pair in _toolButtons)
            {
                if (pair.Key == _activeTool)
                {
                    pair.Value.AddToClassList("build-tool--active");
                }
                else
                {
                    pair.Value.RemoveFromClassList("build-tool--active");
                }
            }
        }

        private void RefreshCash(bool force)
        {
            if (!_localizationReady || _cashLabel == null || _cashProvider == null)
            {
                return;
            }

            long cash = _cashProvider();
            if (!force && cash == _lastCash)
            {
                return;
            }

            _lastCash = cash;
            _cashLabel.text = CashFormat.FormatLabeled(_cashTemplate, cash, _culture);
        }

        private void RefreshVehicles(bool forceRebuild)
        {
            if (!_localizationReady
                || !_uiReady
                || !_vehiclesPanelOpen
                || _vehiclesList == null
                || _vehiclesEmpty == null
                || _vehiclesProvider == null)
            {
                return;
            }

            IReadOnlyList<VehicleListItem> vehicles = _vehiclesProvider();
            EnsureSelectedVehicleStillExists(vehicles);
            int idFingerprint = ComputeVehicleIdFingerprint(vehicles);
            bool structureChanged = forceRebuild || idFingerprint != _lastVehicleIdFingerprint;
            if (structureChanged)
            {
                _lastVehicleIdFingerprint = idFingerprint;
                RebuildVehicleRows(vehicles);
                RefreshOrders(forceRebuild: true);
                return;
            }

            for (int index = 0; index < vehicles.Count; index++)
            {
                _vehicleRows[index].Summary.text = FormatVehicleRow(vehicles[index]);
            }

            RefreshVehicleSelectionHighlight();
        }

        private void EnsureSelectedVehicleStillExists(IReadOnlyList<VehicleListItem> vehicles)
        {
            if (!_ordersDraft.VehicleId.HasValue)
            {
                return;
            }

            int selectedId = _ordersDraft.VehicleId.Value;
            VehicleListKind selectedKind = _ordersDraft.Kind!.Value;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                if (item.Kind == selectedKind && item.Id == selectedId)
                {
                    return;
                }
            }

            _ordersDraft.ClearSelection();
            if (_ordersStatus != null)
            {
                _ordersStatus.text = string.Empty;
            }
        }

        private void RefreshIndustries(bool forceRebuild)
        {
            if (!_localizationReady
                || !_uiReady
                || !_industriesPanelOpen
                || _industriesList == null
                || _industriesEmpty == null
                || _industriesProvider == null)
            {
                return;
            }

            IReadOnlyList<IndustryListItem> industries = _industriesProvider();
            int fingerprint = ComputeIndustryFingerprint(industries);
            bool structureChanged = forceRebuild || fingerprint != _lastIndustryFingerprint;
            if (structureChanged)
            {
                _lastIndustryFingerprint = fingerprint;
                RebuildIndustryRows(industries);
                RefreshIndustrySelectionHighlight();
                return;
            }

            for (int index = 0; index < industries.Count; index++)
            {
                _industryRows[index].Summary.text = FormatIndustryRow(industries[index]);
            }

            RefreshIndustrySelectionHighlight();
        }

        private void RefreshTowns(bool forceRebuild)
        {
            if (!_localizationReady
                || !_uiReady
                || !_townsPanelOpen
                || _townsList == null
                || _townsEmpty == null
                || _townsProvider == null)
            {
                return;
            }

            IReadOnlyList<TownListItem> towns = _townsProvider();
            int fingerprint = ComputeTownFingerprint(towns);
            bool structureChanged = forceRebuild || fingerprint != _lastTownFingerprint;
            if (structureChanged)
            {
                _lastTownFingerprint = fingerprint;
                RebuildTownRows(towns);
                return;
            }

            for (int index = 0; index < towns.Count; index++)
            {
                UpdateTownRowBindings(_townRowBindings[index], towns[index]);
            }

            RefreshTownSelectionHighlight();
        }

        private void RebuildVehicleRows(IReadOnlyList<VehicleListItem> vehicles)
        {
            if (_vehiclesList == null || _vehiclesEmpty == null)
            {
                return;
            }

            ClearVehicleRows();
            _vehiclesList.Clear();

            if (vehicles.Count == 0)
            {
                _vehiclesEmpty.style.display = DisplayStyle.Flex;
                return;
            }

            _vehiclesEmpty.style.display = DisplayStyle.None;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleListItem item = vehicles[index];
                var row = new VisualElement();
                row.AddToClassList("vehicles-row");

                var summary = new Label(FormatVehicleRow(item));
                summary.AddToClassList("vehicles-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                VehicleListKind kind = item.Kind;
                int vehicleId = item.Id;
                EventCallback<ClickEvent> selectHandler = evt =>
                {
                    if (evt.target is Button)
                    {
                        return;
                    }

                    SelectVehicleForOrders(kind, vehicleId);
                };
                row.RegisterCallback(selectHandler);

                Button? addWagon = null;
                Action? addWagonHandler = null;
                Button? sellWagon = null;
                Action? sellWagonHandler = null;
                if (kind == VehicleListKind.Rail)
                {
                    addWagon = new Button { text = _addWagonLabel };
                    addWagon.AddToClassList("vehicles-row-sell");
                    addWagon.SetEnabled(item.WagonCount < item.MaxWagons);
                    addWagonHandler = () =>
                    {
                        _onAddRailWagon?.Invoke(vehicleId);
                        _lastVehicleIdFingerprint = int.MinValue;
                        RefreshVehicles(forceRebuild: true);
                    };
                    addWagon.clicked += addWagonHandler;
                    row.Add(addWagon);

                    sellWagon = new Button { text = _sellWagonLabel };
                    sellWagon.AddToClassList("vehicles-row-sell");
                    sellWagon.SetEnabled(item.WagonCount > 0);
                    sellWagonHandler = () =>
                    {
                        _onSellRailWagon?.Invoke(vehicleId);
                        _lastVehicleIdFingerprint = int.MinValue;
                        RefreshVehicles(forceRebuild: true);
                    };
                    sellWagon.clicked += sellWagonHandler;
                    row.Add(sellWagon);
                }

                var sell = new Button { text = _sellLabel };
                sell.AddToClassList("vehicles-row-sell");
                Action sellHandler = () =>
                {
                    if (_ordersDraft.Kind == kind && _ordersDraft.VehicleId == vehicleId)
                    {
                        _ordersDraft.ClearSelection();
                    }

                    _onSellVehicle?.Invoke(kind, vehicleId);
                    _lastVehicleIdFingerprint = int.MinValue;
                    RefreshVehicles(forceRebuild: true);
                };
                sell.clicked += sellHandler;
                row.Add(sell);
                _vehiclesList.Add(row);
                _vehicleRows.Add(
                    new VehicleRowBindings(
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

            RefreshVehicleSelectionHighlight();
        }

        private void RefreshVehicleSelectionHighlight()
        {
            int? selectedId = _ordersDraft.VehicleId;
            for (int index = 0; index < _vehicleRows.Count; index++)
            {
                VehicleRowBindings bindings = _vehicleRows[index];
                bool selected = selectedId.HasValue
                    && _ordersDraft.Kind.HasValue
                    && bindings.Kind == _ordersDraft.Kind.Value
                    && bindings.VehicleId == selectedId.Value;
                if (selected)
                {
                    bindings.Row.AddToClassList("vehicles-row--selected");
                }
                else
                {
                    bindings.Row.RemoveFromClassList("vehicles-row--selected");
                }

                // Keep summary text in sync with selection prefix without rebuilding all rows.
                if (_vehiclesProvider != null)
                {
                    IReadOnlyList<VehicleListItem> vehicles = _vehiclesProvider();
                    for (int vehicleIndex = 0; vehicleIndex < vehicles.Count; vehicleIndex++)
                    {
                        VehicleListItem vehicle = vehicles[vehicleIndex];
                        if (vehicle.Kind == bindings.Kind && vehicle.Id == bindings.VehicleId)
                        {
                            bindings.Summary.text = FormatVehicleRow(vehicle);
                            break;
                        }
                    }
                }
            }
        }

        private void RefreshOrders(bool forceRebuild)
        {
            if (!_localizationReady
                || !_uiReady
                || !_vehiclesPanelOpen
                || _ordersList == null
                || _ordersEmpty == null
                || _ordersSelected == null
                || _ordersApply == null)
            {
                return;
            }

            if (!_ordersDraft.HasSelection || !_ordersDraft.VehicleId.HasValue)
            {
                ClearOrderRows();
                _ordersList.Clear();
                _ordersList.style.display = DisplayStyle.None;
                _ordersEmpty.style.display = DisplayStyle.None;
                _ordersApply.SetEnabled(false);
                _ordersSelected.text = _ordersNoneSelectedMessage;
                return;
            }

            _ordersApply.SetEnabled(true);
            _ordersSelected.text = string.Format(
                _culture,
                _ordersSelectedTemplate,
                _ordersDraft.VehicleId.Value.ToString(_culture));

            IReadOnlyList<int> stops = _ordersDraft.Stops;
            if (!forceRebuild && _orderRows.Count == stops.Count)
            {
                for (int index = 0; index < stops.Count; index++)
                {
                    _orderRows[index].Summary.text = FormatOrderStop(index, stops[index]);
                }

                _ordersEmpty.style.display = stops.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _ordersList.style.display = stops.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                return;
            }

            RebuildOrderRows(stops);
        }

        private void RebuildOrderRows(IReadOnlyList<int> stops)
        {
            if (_ordersList == null || _ordersEmpty == null)
            {
                return;
            }

            ClearOrderRows();
            _ordersList.Clear();

            if (stops.Count == 0)
            {
                _ordersEmpty.style.display = DisplayStyle.Flex;
                _ordersList.style.display = DisplayStyle.None;
                _ordersEmpty.text = _ordersEmptyMessage;
                return;
            }

            _ordersEmpty.style.display = DisplayStyle.None;
            _ordersList.style.display = DisplayStyle.Flex;
            for (int index = 0; index < stops.Count; index++)
            {
                int stopIndex = index;
                int stationId = stops[index];
                var row = new VisualElement();
                row.AddToClassList("orders-row");

                var summary = new Label(FormatOrderStop(stopIndex, stationId));
                summary.AddToClassList("orders-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                var actions = new VisualElement();
                actions.AddToClassList("orders-row-actions");

                var up = new Button { text = _ordersUpLabel };
                up.AddToClassList("orders-row-action");
                Action upHandler = () =>
                {
                    if (_ordersDraft.TryMoveUp(stopIndex))
                    {
                        RefreshOrders(forceRebuild: true);
                    }
                };
                up.clicked += upHandler;
                up.SetEnabled(stopIndex > 0);
                actions.Add(up);

                var down = new Button { text = _ordersDownLabel };
                down.AddToClassList("orders-row-action");
                Action downHandler = () =>
                {
                    if (_ordersDraft.TryMoveDown(stopIndex))
                    {
                        RefreshOrders(forceRebuild: true);
                    }
                };
                down.clicked += downHandler;
                down.SetEnabled(stopIndex < stops.Count - 1);
                actions.Add(down);

                var remove = new Button { text = _ordersRemoveLabel };
                remove.AddToClassList("orders-row-action");
                Action removeHandler = () =>
                {
                    if (_ordersDraft.TryRemoveAt(stopIndex))
                    {
                        if (_ordersStatus != null)
                        {
                            _ordersStatus.text = string.Empty;
                        }

                        RefreshOrders(forceRebuild: true);
                    }
                };
                remove.clicked += removeHandler;
                actions.Add(remove);

                row.Add(actions);
                _ordersList.Add(row);
                _orderRows.Add(new OrderRowBindings(summary, up, down, remove, upHandler, downHandler, removeHandler));
            }
        }

        private string FormatOrderStop(int index, int stationId)
        {
            return string.Format(
                _culture,
                _ordersStopTemplate,
                (index + 1).ToString(_culture),
                stationId.ToString(_culture));
        }

        private void RebuildIndustryRows(IReadOnlyList<IndustryListItem> industries)
        {
            if (_industriesList == null || _industriesEmpty == null)
            {
                return;
            }

            ClearIndustryRows();
            _industriesList.Clear();

            if (industries.Count == 0)
            {
                _industriesEmpty.style.display = DisplayStyle.Flex;
                return;
            }

            _industriesEmpty.style.display = DisplayStyle.None;
            for (int index = 0; index < industries.Count; index++)
            {
                IndustryListItem item = industries[index];
                string industryId = item.IndustryId;
                int instanceIndex = item.InstanceIndex;
                var row = new VisualElement();
                row.AddToClassList("industries-row");

                var summary = new Label(FormatIndustryRow(item));
                summary.AddToClassList("industries-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                EventCallback<ClickEvent> clickHandler = _ =>
                {
                    _selectedIndustryId = industryId;
                    _selectedIndustryInstance = instanceIndex;
                    _selectedTownId = null;
                    RefreshIndustrySelectionHighlight();
                    RefreshTownSelectionHighlight();
                    _onIndustryRowSelected?.Invoke(industryId, instanceIndex);
                };
                row.RegisterCallback(clickHandler);

                if (_selectedIndustryId != null
                    && string.Equals(_selectedIndustryId, industryId, StringComparison.Ordinal)
                    && _selectedIndustryInstance == instanceIndex)
                {
                    row.AddToClassList("industries-row--selected");
                }

                _industriesList.Add(row);
                _industryRows.Add(
                    new IndustryRowBindings(industryId, instanceIndex, row, summary, clickHandler));
            }
        }

        private void RebuildTownRows(IReadOnlyList<TownListItem> towns)
        {
            if (_townsList == null || _townsEmpty == null)
            {
                return;
            }

            ClearTownRows();
            _townsList.Clear();

            if (towns.Count == 0)
            {
                _townsEmpty.style.display = DisplayStyle.Flex;
                return;
            }

            _townsEmpty.style.display = DisplayStyle.None;
            for (int index = 0; index < towns.Count; index++)
            {
                TownListItem item = towns[index];
                int townId = item.TownId;
                var row = new VisualElement();
                row.AddToClassList("towns-row");

                var summary = new Label(FormatTownSummary(item));
                summary.AddToClassList("towns-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);

                var needLabels = new List<Label>();
                IReadOnlyList<TownNeedListLine> needs = item.Needs;
                for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                {
                    var needLabel = new Label(FormatNeedLine(needs[needIndex]));
                    needLabel.AddToClassList("towns-row-line");
                    needLabel.pickingMode = PickingMode.Ignore;
                    row.Add(needLabel);
                    needLabels.Add(needLabel);
                }

                var passengerLabel = new Label(FormatPassengerLine(item));
                passengerLabel.AddToClassList("towns-row-line");
                passengerLabel.pickingMode = PickingMode.Ignore;
                row.Add(passengerLabel);

                var mailLabel = new Label(FormatMailLine(item));
                mailLabel.AddToClassList("towns-row-line");
                mailLabel.pickingMode = PickingMode.Ignore;
                row.Add(mailLabel);

                EventCallback<ClickEvent> clickHandler = _ =>
                {
                    _selectedTownId = townId;
                    _selectedIndustryId = null;
                    _selectedIndustryInstance = 0;
                    RefreshTownSelectionHighlight();
                    RefreshIndustrySelectionHighlight();
                    _onTownRowSelected?.Invoke(townId);
                };
                row.RegisterCallback(clickHandler);

                if (_selectedTownId.HasValue && _selectedTownId.Value == townId)
                {
                    row.AddToClassList("towns-row--selected");
                }

                _townsList.Add(row);
                _townRowBindings.Add(
                    new TownRowBindings(townId, row, summary, needLabels, passengerLabel, mailLabel, clickHandler));
            }
        }

        private void UpdateTownRowBindings(TownRowBindings bindings, TownListItem item)
        {
            bindings.Summary.text = FormatTownSummary(item);
            IReadOnlyList<TownNeedListLine> needs = item.Needs;
            for (int needIndex = 0; needIndex < bindings.NeedLabels.Count; needIndex++)
            {
                bindings.NeedLabels[needIndex].text = FormatNeedLine(needs[needIndex]);
            }

            bindings.PassengerLabel.text = FormatPassengerLine(item);
            bindings.MailLabel.text = FormatMailLine(item);
        }

        private void RefreshTownSelectionHighlight()
        {
            int? selectedId = _selectedTownId;
            for (int index = 0; index < _townRowBindings.Count; index++)
            {
                TownRowBindings bindings = _townRowBindings[index];
                bool selected = selectedId.HasValue && bindings.TownId == selectedId.Value;
                if (selected)
                {
                    bindings.Row.AddToClassList("towns-row--selected");
                }
                else
                {
                    bindings.Row.RemoveFromClassList("towns-row--selected");
                }
            }
        }

        private void RefreshIndustrySelectionHighlight()
        {
            string? selectedId = _selectedIndustryId;
            for (int index = 0; index < _industryRows.Count; index++)
            {
                IndustryRowBindings bindings = _industryRows[index];
                bool selected = selectedId != null
                    && string.Equals(selectedId, bindings.IndustryId, StringComparison.Ordinal)
                    && _selectedIndustryInstance == bindings.InstanceIndex;
                if (selected)
                {
                    bindings.Row.AddToClassList("industries-row--selected");
                }
                else
                {
                    bindings.Row.RemoveFromClassList("industries-row--selected");
                }
            }
        }

        private void ClearVehicleRows()
        {
            for (int index = 0; index < _vehicleRows.Count; index++)
            {
                VehicleRowBindings bindings = _vehicleRows[index];
                bindings.Sell.clicked -= bindings.SellHandler;
                if (bindings.AddWagon != null && bindings.AddWagonHandler != null)
                {
                    bindings.AddWagon.clicked -= bindings.AddWagonHandler;
                }

                if (bindings.SellWagon != null && bindings.SellWagonHandler != null)
                {
                    bindings.SellWagon.clicked -= bindings.SellWagonHandler;
                }

                if (bindings.SelectHandler != null)
                {
                    bindings.Row.UnregisterCallback(bindings.SelectHandler);
                }
            }

            _vehicleRows.Clear();
        }

        private void ClearOrderRows()
        {
            for (int index = 0; index < _orderRows.Count; index++)
            {
                OrderRowBindings bindings = _orderRows[index];
                bindings.Up.clicked -= bindings.UpHandler;
                bindings.Down.clicked -= bindings.DownHandler;
                bindings.Remove.clicked -= bindings.RemoveHandler;
            }

            _orderRows.Clear();
        }

        private void ClearIndustryRows()
        {
            for (int index = 0; index < _industryRows.Count; index++)
            {
                IndustryRowBindings bindings = _industryRows[index];
                bindings.Row.UnregisterCallback(bindings.ClickHandler);
            }

            _industryRows.Clear();
        }

        private void ClearTownRows()
        {
            for (int index = 0; index < _townRowBindings.Count; index++)
            {
                TownRowBindings bindings = _townRowBindings[index];
                bindings.Row.UnregisterCallback(bindings.ClickHandler);
            }

            _townRowBindings.Clear();
        }

        private string FormatVehicleRow(VehicleListItem item)
        {
            string status = StatusLabel(item.Status);
            string income = CashFormat.FormatAmount(item.LifetimeIncome, _culture);
            string row;
            if (item.Kind == VehicleListKind.Rail)
            {
                row = string.Format(
                    _culture,
                    _rowRailTemplate,
                    item.Id.ToString(_culture),
                    _kindRailLabel,
                    status,
                    item.WagonCount.ToString(_culture),
                    item.MaxWagons.ToString(_culture),
                    item.CargoAmount.ToString(_culture),
                    item.CargoCapacity.ToString(_culture),
                    income);
            }
            else
            {
                row = string.Format(
                    _culture,
                    _rowTemplate,
                    item.Id.ToString(_culture),
                    _kindRoadLabel,
                    status,
                    item.CargoAmount.ToString(_culture),
                    item.CargoCapacity.ToString(_culture),
                    income);
            }

            if (_ordersDraft.Kind.HasValue
                && _ordersDraft.Kind.Value == item.Kind
                && _ordersDraft.VehicleId.HasValue
                && _ordersDraft.VehicleId.Value == item.Id)
            {
                return "► SELECTED · " + row;
            }

            return row;
        }

        private string FormatIndustryRow(IndustryListItem item)
        {
            string name = ResolveIndustryName(item.NameKey);
            return string.Format(
                _culture,
                _industryRowTemplate,
                name,
                item.InstanceIndex.ToString(_culture),
                item.Level.ToString(_culture),
                item.CargoAmount.ToString(_culture),
                item.CargoCapacity.ToString(_culture),
                item.MonthProduced.ToString(_culture),
                item.MonthTransported.ToString(_culture));
        }

        private string ResolveIndustryName(string nameKey)
        {
            if (_industryNames.TryGetValue(nameKey, out string? localized) && !string.IsNullOrEmpty(localized))
            {
                return localized;
            }

            return nameKey;
        }

        private string FormatTownSummary(TownListItem item)
        {
            return string.Format(
                _culture,
                _townSummaryTemplate,
                item.TownId.ToString(_culture),
                item.Population.ToString(_culture),
                item.SatisfiedNeeds.ToString(_culture),
                item.TotalNeeds.ToString(_culture));
        }

        private string FormatNeedLine(TownNeedListLine need)
        {
            return string.Format(
                _culture,
                _townNeedLineTemplate,
                ResolveCargoDisplayName(need.CargoId, need.CargoNameKey),
                need.MonthSupply.ToString(_culture),
                need.Demand.ToString(_culture));
        }

        private string FormatPassengerLine(TownListItem item)
        {
            return string.Format(
                _culture,
                _townPassengerLineTemplate,
                item.MonthPassengersGenerated.ToString(_culture),
                item.MonthPassengersTransported.ToString(_culture));
        }

        private string FormatMailLine(TownListItem item)
        {
            return string.Format(
                _culture,
                _townMailLineTemplate,
                item.MonthMailGenerated.ToString(_culture),
                item.MonthMailTransported.ToString(_culture));
        }

        private static string ResolveCargoDisplayName(string cargoId, string cargoNameKey)
        {
            _ = cargoNameKey;
            return cargoId.Replace('_', ' ');
        }

        private string StatusLabel(VehicleListStatusKind status)
        {
            switch (status)
            {
                case VehicleListStatusKind.Moving:
                    return _statusMoving;
                case VehicleListStatusKind.Loading:
                    return _statusLoading;
                case VehicleListStatusKind.Stuck:
                    return _statusStuck;
                case VehicleListStatusKind.Parked:
                    return _statusParked;
                default:
                    return _statusParked;
            }
        }

        private static int ComputeVehicleIdFingerprint(IReadOnlyList<VehicleListItem> vehicles)
        {
            unchecked
            {
                int hash = vehicles.Count;
                for (int index = 0; index < vehicles.Count; index++)
                {
                    VehicleListItem item = vehicles[index];
                    hash = (hash * 397) ^ (int)item.Kind;
                    hash = (hash * 397) ^ item.Id;
                    hash = (hash * 397) ^ item.WagonCount;
                    hash = (hash * 397) ^ item.CargoAmount;
                    hash = (hash * 397) ^ item.CargoCapacity;
                }

                return hash;
            }
        }

        private static int ComputeIndustryFingerprint(IReadOnlyList<IndustryListItem> industries)
        {
            unchecked
            {
                int hash = industries.Count;
                for (int index = 0; index < industries.Count; index++)
                {
                    IndustryListItem item = industries[index];
                    hash = (hash * 397) ^ item.IndustryId.GetHashCode();
                    hash = (hash * 397) ^ item.InstanceIndex;
                }

                return hash;
            }
        }

        private static int ComputeTownFingerprint(IReadOnlyList<TownListItem> towns)
        {
            unchecked
            {
                int hash = towns.Count;
                for (int index = 0; index < towns.Count; index++)
                {
                    TownListItem item = towns[index];
                    hash = (hash * 397) ^ item.TownId;
                    hash = (hash * 397) ^ item.Needs.Count;
                }

                return hash;
            }
        }

        private sealed class IndustryRowBindings
        {
            public IndustryRowBindings(
                string industryId,
                int instanceIndex,
                VisualElement row,
                Label summary,
                EventCallback<ClickEvent> clickHandler)
            {
                IndustryId = industryId;
                InstanceIndex = instanceIndex;
                Row = row;
                Summary = summary;
                ClickHandler = clickHandler;
            }

            public string IndustryId { get; }

            public int InstanceIndex { get; }

            public VisualElement Row { get; }

            public Label Summary { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }

        private sealed class TownRowBindings
        {
            public TownRowBindings(
                int townId,
                VisualElement row,
                Label summary,
                List<Label> needLabels,
                Label passengerLabel,
                Label mailLabel,
                EventCallback<ClickEvent> clickHandler)
            {
                TownId = townId;
                Row = row;
                Summary = summary;
                NeedLabels = needLabels;
                PassengerLabel = passengerLabel;
                MailLabel = mailLabel;
                ClickHandler = clickHandler;
            }

            public int TownId { get; }

            public VisualElement Row { get; }

            public Label Summary { get; }

            public IReadOnlyList<Label> NeedLabels { get; }

            public Label PassengerLabel { get; }

            public Label MailLabel { get; }

            public EventCallback<ClickEvent> ClickHandler { get; }
        }

        private sealed class VehicleRowBindings
        {
            public VehicleRowBindings(
                VehicleListKind kind,
                int vehicleId,
                VisualElement row,
                Label summary,
                Button sell,
                Action sellHandler,
                EventCallback<ClickEvent>? selectHandler,
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

            public Label Summary { get; }

            public Button Sell { get; }

            public Action SellHandler { get; }

            public EventCallback<ClickEvent>? SelectHandler { get; }

            public Button? AddWagon { get; }

            public Action? AddWagonHandler { get; }

            public Button? SellWagon { get; }

            public Action? SellWagonHandler { get; }
        }

        private sealed class OrderRowBindings
        {
            public OrderRowBindings(
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
