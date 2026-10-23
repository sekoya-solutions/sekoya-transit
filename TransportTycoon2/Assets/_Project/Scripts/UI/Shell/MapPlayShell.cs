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
    /// MapPlay HUD shell: localized cash, hint, build toolbar, vehicles and industries panels.
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
        private VisualElement? _vehiclesPanel;
        private VisualElement? _industriesPanel;
        private Label? _vehiclesTitle;
        private Label? _industriesTitle;
        private Button? _vehiclesBuy;
        private ScrollView? _vehiclesList;
        private ScrollView? _industriesList;
        private Label? _vehiclesEmpty;
        private Label? _industriesEmpty;
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
        private readonly List<Label> _industryRowLabels = new List<Label>();
        private readonly Dictionary<string, string> _industryNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly VehicleOrdersDraft _ordersDraft = new VehicleOrdersDraft();
        private bool _pointerOverChrome;
        private int _blockMapInputUntilFrame = -1;
        private Func<long>? _cashProvider;
        private Action<BuildToolId>? _onToolChosen;
        private Func<IReadOnlyList<VehicleListItem>>? _vehiclesProvider;
        private Func<IReadOnlyList<IndustryListItem>>? _industriesProvider;
        private Action? _onBuyVehicle;
        private Action<int>? _onSellVehicle;
        private Func<int, IReadOnlyList<int>>? _getVehicleOrderStationIds;
        private Func<int, IReadOnlyList<int>, bool>? _tryApplyVehicleOrders;
        private string _cashTemplate = "Cash: {0}";
        private string _rowTemplate = "#{0} · {1} · Cargo {2}/{3} · Income {4}";
        private string _industryRowTemplate = "{0} #{1} · L{2} · Stock {3}/{4} · Produced {5} · Transported {6}";
        private string _ordersSelectedTemplate = "Editing vehicle #{0}";
        private string _ordersStopTemplate = "#{0} Station {1}";
        private string _statusParked = "Parked";
        private string _statusMoving = "Moving";
        private string _statusLoading = "Loading";
        private string _statusStuck = "Stuck";
        private string _sellLabel = "Sell";
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
        private int _lastVehicleIdFingerprint = int.MinValue;
        private int _lastIndustryFingerprint = int.MinValue;

        /// <summary>
        /// Binds cash, tool-selection, list, and vehicle-order callbacks from the composition root (Presentation).
        /// </summary>
        public void Bind(
            Func<long> cashProvider,
            Action<BuildToolId> onToolChosen,
            Func<IReadOnlyList<VehicleListItem>> vehiclesProvider,
            Func<IReadOnlyList<IndustryListItem>> industriesProvider,
            Action onBuyVehicle,
            Action<int> onSellVehicle,
            Func<int, IReadOnlyList<int>> getVehicleOrderStationIds,
            Func<int, IReadOnlyList<int>, bool> tryApplyVehicleOrders)
        {
            _cashProvider = cashProvider ?? throw new ArgumentNullException(nameof(cashProvider));
            _onToolChosen = onToolChosen ?? throw new ArgumentNullException(nameof(onToolChosen));
            _vehiclesProvider = vehiclesProvider ?? throw new ArgumentNullException(nameof(vehiclesProvider));
            _industriesProvider = industriesProvider ?? throw new ArgumentNullException(nameof(industriesProvider));
            _onBuyVehicle = onBuyVehicle ?? throw new ArgumentNullException(nameof(onBuyVehicle));
            _onSellVehicle = onSellVehicle ?? throw new ArgumentNullException(nameof(onSellVehicle));
            _getVehicleOrderStationIds = getVehicleOrderStationIds
                ?? throw new ArgumentNullException(nameof(getVehicleOrderStationIds));
            _tryApplyVehicleOrders = tryApplyVehicleOrders
                ?? throw new ArgumentNullException(nameof(tryApplyVehicleOrders));
            _lastCash = long.MinValue;
            _lastVehicleIdFingerprint = int.MinValue;
            _lastIndustryFingerprint = int.MinValue;
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
        /// Sets vehicles panel visibility. Opening closes the industries panel.
        /// </summary>
        public void SetVehiclesPanelOpen(bool open)
        {
            if (open)
            {
                HideIndustriesPanelOnly();
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
        /// Sets industries panel visibility. Opening closes the vehicles panel.
        /// </summary>
        public void SetIndustriesPanelOpen(bool open)
        {
            if (open)
            {
                HideVehiclesPanelOnly();
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
        /// Gets whether the vehicles panel is open.
        /// </summary>
        public bool IsVehiclesPanelOpen => _vehiclesPanelOpen;

        /// <summary>
        /// Gets whether the industries panel is open.
        /// </summary>
        public bool IsIndustriesPanelOpen => _industriesPanelOpen;

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
        /// Appends a station to the selected vehicle's draft route (map click while editing).
        /// </summary>
        /// <returns>True when the station was appended.</returns>
        public bool TryAppendStationToSelectedOrders(int stationId)
        {
            if (!IsEditingVehicleOrders)
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
        public bool IsScreenPositionOverInteractiveUi(Vector2 screenPosition)
        {
            if (!_uiReady || _root == null)
            {
                return false;
            }

            // UITK pointer tracking is authoritative (avoids ScreenToPanel mismatches on WebGL).
            if (_pointerOverChrome || Time.frameCount <= _blockMapInputUntilFrame)
            {
                return true;
            }

            IPanel? panel = _root.panel;
            if (panel == null)
            {
                return false;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);

            // Geometry fallback: Labels use PickingMode.Ignore, so Pick alone misses scroll-over-text.
            if (_vehiclesPanelOpen && _vehiclesPanel != null && _vehiclesPanel.worldBound.Contains(panelPosition))
            {
                return true;
            }

            if (_industriesPanelOpen && _industriesPanel != null && _industriesPanel.worldBound.Contains(panelPosition))
            {
                return true;
            }

            if (_buildToolbar != null && _buildToolbar.worldBound.Contains(panelPosition))
            {
                return true;
            }

            if (_hudPanel != null && _hudPanel.worldBound.Contains(panelPosition))
            {
                return true;
            }

            VisualElement? picked = panel.Pick(panelPosition);
            return IsUnderChrome(picked);
        }

        private IEnumerator Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;
            _cashLabel = _root.Q<Label>("cash-label");
            _hintLabel = _root.Q<Label>("hint-label");
            _vehiclesToggle = _root.Q<Button>("vehicles-toggle");
            _industriesToggle = _root.Q<Button>("industries-toggle");
            _vehiclesPanel = _root.Q<VisualElement>("vehicles-panel");
            _industriesPanel = _root.Q<VisualElement>("industries-panel");
            _vehiclesTitle = _root.Q<Label>("vehicles-title");
            _industriesTitle = _root.Q<Label>("industries-title");
            _vehiclesBuy = _root.Q<Button>("vehicles-buy");
            _vehiclesList = _root.Q<ScrollView>("vehicles-list");
            _industriesList = _root.Q<ScrollView>("industries-list");
            _vehiclesEmpty = _root.Q<Label>("vehicles-empty");
            _industriesEmpty = _root.Q<Label>("industries-empty");
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
                || _vehiclesPanel == null
                || _industriesPanel == null
                || _vehiclesTitle == null
                || _industriesTitle == null
                || _vehiclesBuy == null
                || _vehiclesList == null
                || _industriesList == null
                || _vehiclesEmpty == null
                || _industriesEmpty == null
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
                    "MapPlayShell UXML must define cash, hint, vehicles (including orders), industries, HUD, and toolbar elements.");
            }

            BindToolbarButtons(_root);
            _hudPanel.pickingMode = PickingMode.Position;
            _buildToolbar.pickingMode = PickingMode.Position;
            _vehiclesPanel.pickingMode = PickingMode.Position;
            _industriesPanel.pickingMode = PickingMode.Position;
            // TrickleDown on root so we see pointer targets under Ignore shell-root (map pass-through).
            _root.RegisterCallback<PointerMoveEvent>(OnShellPointerMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerDownEvent>(OnShellPointerDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerLeaveEvent>(OnShellPointerLeave);
            _vehiclesToggle.clicked += OnVehiclesToggleClicked;
            _industriesToggle.clicked += OnIndustriesToggleClicked;
            _vehiclesBuy.clicked += OnVehiclesBuyClicked;
            _ordersApply.clicked += OnOrdersApplyClicked;
            SetVehiclesPanelOpen(false);
            SetIndustriesPanelOpen(false);
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

            ClearVehicleRows();
            ClearOrderRows();
            ClearIndustryRows();
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            _cashProvider = null;
            _onToolChosen = null;
            _vehiclesProvider = null;
            _industriesProvider = null;
            _onBuyVehicle = null;
            _onSellVehicle = null;
            _getVehicleOrderStationIds = null;
            _tryApplyVehicleOrders = null;
            _ordersDraft.ClearSelection();
            _cashLabel = null;
            _hintLabel = null;
            _vehiclesToggle = null;
            _industriesToggle = null;
            _vehiclesPanel = null;
            _industriesPanel = null;
            _vehiclesTitle = null;
            _industriesTitle = null;
            _vehiclesBuy = null;
            _vehiclesList = null;
            _industriesList = null;
            _vehiclesEmpty = null;
            _industriesEmpty = null;
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

        private bool IsUnderChrome(VisualElement? element)
        {
            VisualElement? current = element;
            while (current != null && current != _root)
            {
                if (current == _buildToolbar
                    || current == _hudPanel
                    || current == _vehiclesPanel
                    || current == _industriesPanel
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
        }

        private void OnVehiclesToggleClicked()
        {
            ToggleVehiclesPanel();
        }

        private void OnIndustriesToggleClicked()
        {
            ToggleIndustriesPanel();
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
            bool accepted = _tryApplyVehicleOrders(vehicleId, _ordersDraft.ToStationIdArray());
            if (_ordersStatus != null)
            {
                _ordersStatus.text = accepted ? _ordersAppliedMessage : _ordersRejectedMessage;
            }

            if (accepted && _getVehicleOrderStationIds != null)
            {
                _ordersDraft.SelectVehicle(vehicleId, _getVehicleOrderStationIds(vehicleId));
                RefreshOrders(forceRebuild: true);
            }
        }

        private void SelectVehicleForOrders(int vehicleId)
        {
            if (_getVehicleOrderStationIds == null)
            {
                return;
            }

            _ordersDraft.SelectVehicle(vehicleId, _getVehicleOrderStationIds(vehicleId));
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
            RefreshVehicles(forceRebuild: true);
            RefreshIndustries(forceRebuild: true);
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
            for (int index = 0; index < vehicles.Count; index++)
            {
                if (vehicles[index].Id == selectedId)
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
                return;
            }

            for (int index = 0; index < industries.Count; index++)
            {
                _industryRowLabels[index].text = FormatIndustryRow(industries[index]);
            }
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

                var sell = new Button { text = _sellLabel };
                sell.AddToClassList("vehicles-row-sell");
                int vehicleId = item.Id;
                EventCallback<ClickEvent> selectHandler = evt =>
                {
                    if (evt.target is Button)
                    {
                        return;
                    }

                    SelectVehicleForOrders(vehicleId);
                };
                row.RegisterCallback(selectHandler);

                Action sellHandler = () =>
                {
                    if (_ordersDraft.VehicleId == vehicleId)
                    {
                        _ordersDraft.ClearSelection();
                    }

                    _onSellVehicle?.Invoke(vehicleId);
                    _lastVehicleIdFingerprint = int.MinValue;
                    RefreshVehicles(forceRebuild: true);
                };
                sell.clicked += sellHandler;
                row.Add(sell);
                _vehiclesList.Add(row);
                _vehicleRows.Add(new VehicleRowBindings(vehicleId, row, summary, sell, sellHandler, selectHandler));
            }

            RefreshVehicleSelectionHighlight();
        }

        private void RefreshVehicleSelectionHighlight()
        {
            int? selectedId = _ordersDraft.VehicleId;
            for (int index = 0; index < _vehicleRows.Count; index++)
            {
                VehicleRowBindings bindings = _vehicleRows[index];
                if (selectedId.HasValue && bindings.VehicleId == selectedId.Value)
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
                        if (vehicles[vehicleIndex].Id == bindings.VehicleId)
                        {
                            bindings.Summary.text = FormatVehicleRow(vehicles[vehicleIndex]);
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
                var row = new VisualElement();
                row.AddToClassList("industries-row");

                var summary = new Label(FormatIndustryRow(item));
                summary.AddToClassList("industries-row-summary");
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);
                _industriesList.Add(row);
                _industryRowLabels.Add(summary);
            }
        }

        private void ClearVehicleRows()
        {
            for (int index = 0; index < _vehicleRows.Count; index++)
            {
                VehicleRowBindings bindings = _vehicleRows[index];
                bindings.Sell.clicked -= bindings.SellHandler;
                bindings.Row.UnregisterCallback(bindings.SelectHandler);
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
            _industryRowLabels.Clear();
        }

        private string FormatVehicleRow(VehicleListItem item)
        {
            string status = StatusLabel(item.Status);
            string income = CashFormat.FormatAmount(item.LifetimeIncome, _culture);
            string row = string.Format(
                _culture,
                _rowTemplate,
                item.Id.ToString(_culture),
                status,
                item.CargoAmount.ToString(_culture),
                item.CargoCapacity.ToString(_culture),
                income);
            if (_ordersDraft.VehicleId.HasValue && _ordersDraft.VehicleId.Value == item.Id)
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
                    hash = (hash * 397) ^ vehicles[index].Id;
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

        private sealed class VehicleRowBindings
        {
            public VehicleRowBindings(
                int vehicleId,
                VisualElement row,
                Label summary,
                Button sell,
                Action sellHandler,
                EventCallback<ClickEvent> selectHandler)
            {
                VehicleId = vehicleId;
                Row = row;
                Summary = summary;
                Sell = sell;
                SellHandler = sellHandler;
                SelectHandler = selectHandler;
            }

            public int VehicleId { get; }

            public VisualElement Row { get; }

            public Label Summary { get; }

            public Button Sell { get; }

            public Action SellHandler { get; }

            public EventCallback<ClickEvent> SelectHandler { get; }
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
