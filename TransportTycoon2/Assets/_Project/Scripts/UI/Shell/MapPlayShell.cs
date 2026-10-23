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
    /// MapPlay HUD shell: localized cash, hint, build toolbar, and vehicles panel.
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
            BuildToolId.Station,
            BuildToolId.DemolishStation,
        };

        private Label? _cashLabel;
        private Label? _hintLabel;
        private Button? _vehiclesToggle;
        private VisualElement? _vehiclesPanel;
        private Label? _vehiclesTitle;
        private Button? _vehiclesBuy;
        private ScrollView? _vehiclesList;
        private Label? _vehiclesEmpty;
        private VisualElement? _root;
        private readonly Dictionary<BuildToolId, Button> _toolButtons = new Dictionary<BuildToolId, Button>();
        private readonly Dictionary<BuildToolId, Action> _toolClickHandlers = new Dictionary<BuildToolId, Action>();
        private readonly List<Button> _sellButtons = new List<Button>();
        private readonly List<Action> _sellHandlers = new List<Action>();
        private Func<long>? _cashProvider;
        private Action<BuildToolId>? _onToolChosen;
        private Func<IReadOnlyList<VehicleListItem>>? _vehiclesProvider;
        private Action? _onBuyVehicle;
        private Action<int>? _onSellVehicle;
        private string _cashTemplate = "Cash: {0}";
        private string _rowTemplate = "#{0} · {1} · Cargo {2}/{3} · Income {4}";
        private string _statusParked = "Parked";
        private string _statusMoving = "Moving";
        private string _statusLoading = "Loading";
        private string _statusStuck = "Stuck";
        private string _sellLabel = "Sell";
        private CultureInfo _culture = CultureInfo.GetCultureInfo("en");
        private long _lastCash = long.MinValue;
        private BuildToolId _activeTool = BuildToolId.Select;
        private bool _localizationReady;
        private bool _uiReady;
        private bool _vehiclesPanelOpen;
        private int _lastVehicleFingerprint = int.MinValue;

        /// <summary>
        /// Binds cash, tool-selection, and vehicle-list callbacks from the composition root (Presentation).
        /// </summary>
        public void Bind(
            Func<long> cashProvider,
            Action<BuildToolId> onToolChosen,
            Func<IReadOnlyList<VehicleListItem>> vehiclesProvider,
            Action onBuyVehicle,
            Action<int> onSellVehicle)
        {
            _cashProvider = cashProvider ?? throw new ArgumentNullException(nameof(cashProvider));
            _onToolChosen = onToolChosen ?? throw new ArgumentNullException(nameof(onToolChosen));
            _vehiclesProvider = vehiclesProvider ?? throw new ArgumentNullException(nameof(vehiclesProvider));
            _onBuyVehicle = onBuyVehicle ?? throw new ArgumentNullException(nameof(onBuyVehicle));
            _onSellVehicle = onSellVehicle ?? throw new ArgumentNullException(nameof(onSellVehicle));
            _lastCash = long.MinValue;
            _lastVehicleFingerprint = int.MinValue;
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
        /// Sets vehicles panel visibility.
        /// </summary>
        public void SetVehiclesPanelOpen(bool open)
        {
            _vehiclesPanelOpen = open;
            if (_vehiclesPanel == null)
            {
                return;
            }

            if (open)
            {
                _vehiclesPanel.RemoveFromClassList("vehicles-panel--hidden");
                _lastVehicleFingerprint = int.MinValue;
                RefreshVehicles(force: true);
            }
            else
            {
                _vehiclesPanel.AddToClassList("vehicles-panel--hidden");
            }
        }

        /// <summary>
        /// Gets whether the vehicles panel is open.
        /// </summary>
        public bool IsVehiclesPanelOpen => _vehiclesPanelOpen;

        /// <summary>
        /// Returns whether a screen-space pointer is over an interactive shell control (blocks map clicks).
        /// </summary>
        public bool IsScreenPositionOverInteractiveUi(Vector2 screenPosition)
        {
            if (!_uiReady || _root == null)
            {
                return false;
            }

            IPanel? panel = _root.panel;
            if (panel == null)
            {
                return false;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            VisualElement? picked = panel.Pick(panelPosition);
            if (picked == null || picked == _root)
            {
                return false;
            }

            return picked.pickingMode != PickingMode.Ignore;
        }

        private IEnumerator Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;
            _cashLabel = _root.Q<Label>("cash-label");
            _hintLabel = _root.Q<Label>("hint-label");
            _vehiclesToggle = _root.Q<Button>("vehicles-toggle");
            _vehiclesPanel = _root.Q<VisualElement>("vehicles-panel");
            _vehiclesTitle = _root.Q<Label>("vehicles-title");
            _vehiclesBuy = _root.Q<Button>("vehicles-buy");
            _vehiclesList = _root.Q<ScrollView>("vehicles-list");
            _vehiclesEmpty = _root.Q<Label>("vehicles-empty");
            if (_cashLabel == null
                || _hintLabel == null
                || _vehiclesToggle == null
                || _vehiclesPanel == null
                || _vehiclesTitle == null
                || _vehiclesBuy == null
                || _vehiclesList == null
                || _vehiclesEmpty == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define cash, hint, and vehicles panel elements.");
            }

            BindToolbarButtons(_root);
            _vehiclesToggle.clicked += OnVehiclesToggleClicked;
            _vehiclesBuy.clicked += OnVehiclesBuyClicked;
            SetVehiclesPanelOpen(false);
            _uiReady = true;

            yield return LocalizationSettings.InitializationOperation;
            ApplyCultureFromLocale(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            RefreshStaticTexts();
            _localizationReady = true;
            RefreshCash(force: true);
            RefreshHint();
            RefreshToolHighlight();
            RefreshVehicles(force: true);
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

            if (_vehiclesBuy != null)
            {
                _vehiclesBuy.clicked -= OnVehiclesBuyClicked;
            }

            ClearSellHandlers();
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            _cashProvider = null;
            _onToolChosen = null;
            _vehiclesProvider = null;
            _onBuyVehicle = null;
            _onSellVehicle = null;
            _cashLabel = null;
            _hintLabel = null;
            _vehiclesToggle = null;
            _vehiclesPanel = null;
            _vehiclesTitle = null;
            _vehiclesBuy = null;
            _vehiclesList = null;
            _vehiclesEmpty = null;
            _root = null;
            _uiReady = false;
        }

        private void Update()
        {
            RefreshCash(force: false);
            if (_vehiclesPanelOpen)
            {
                RefreshVehicles(force: false);
            }
        }

        private void OnVehiclesToggleClicked()
        {
            ToggleVehiclesPanel();
        }

        private void OnVehiclesBuyClicked()
        {
            _onBuyVehicle?.Invoke();
            _lastVehicleFingerprint = int.MinValue;
            RefreshVehicles(force: true);
        }

        private void BindToolbarButtons(VisualElement root)
        {
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            RegisterToolButton(root, "tool-select", BuildToolId.Select);
            RegisterToolButton(root, "tool-road", BuildToolId.Road);
            RegisterToolButton(root, "tool-demolish-road", BuildToolId.DemolishRoad);
            RegisterToolButton(root, "tool-station", BuildToolId.Station);
            RegisterToolButton(root, "tool-demolish-station", BuildToolId.DemolishStation);
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
            _lastVehicleFingerprint = int.MinValue;
            RefreshVehicles(force: true);
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

            ApplyLocalizedString(GameUiKeys.VehiclesTitle, value =>
            {
                if (_vehiclesTitle != null)
                {
                    _vehiclesTitle.text = value;
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

            ApplyLocalizedString(GameUiKeys.VehiclesRow, value =>
            {
                _rowTemplate = value;
                _lastVehicleFingerprint = int.MinValue;
                RefreshVehicles(force: true);
            });

            ApplyLocalizedString(GameUiKeys.VehiclesSell, value =>
            {
                _sellLabel = value;
                _lastVehicleFingerprint = int.MinValue;
                RefreshVehicles(force: true);
            });

            ApplyLocalizedString(GameUiKeys.VehiclesStatusParked, value => _statusParked = value);
            ApplyLocalizedString(GameUiKeys.VehiclesStatusMoving, value => _statusMoving = value);
            ApplyLocalizedString(GameUiKeys.VehiclesStatusLoading, value => _statusLoading = value);
            ApplyLocalizedString(GameUiKeys.VehiclesStatusStuck, value => _statusStuck = value);

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

        private void RefreshVehicles(bool force)
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
            int fingerprint = ComputeVehicleFingerprint(vehicles);
            if (!force && fingerprint == _lastVehicleFingerprint)
            {
                return;
            }

            _lastVehicleFingerprint = fingerprint;
            ClearSellHandlers();
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

                var sell = new Button();
                sell.text = _sellLabel;
                sell.AddToClassList("vehicles-row-sell");
                int vehicleId = item.Id;
                Action handler = () =>
                {
                    _onSellVehicle?.Invoke(vehicleId);
                    _lastVehicleFingerprint = int.MinValue;
                    RefreshVehicles(force: true);
                };
                sell.clicked += handler;
                _sellButtons.Add(sell);
                _sellHandlers.Add(handler);
                row.Add(sell);

                _vehiclesList.Add(row);
            }
        }

        private void ClearSellHandlers()
        {
            for (int index = 0; index < _sellButtons.Count; index++)
            {
                _sellButtons[index].clicked -= _sellHandlers[index];
            }

            _sellButtons.Clear();
            _sellHandlers.Clear();
        }

        private string FormatVehicleRow(VehicleListItem item)
        {
            string status = StatusLabel(item.Status);
            string income = CashFormat.FormatAmount(item.LifetimeIncome, _culture);
            return string.Format(
                _culture,
                _rowTemplate,
                item.Id.ToString(_culture),
                status,
                item.CargoAmount.ToString(_culture),
                item.CargoCapacity.ToString(_culture),
                income);
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

        private static int ComputeVehicleFingerprint(IReadOnlyList<VehicleListItem> vehicles)
        {
            unchecked
            {
                int hash = vehicles.Count;
                for (int index = 0; index < vehicles.Count; index++)
                {
                    hash = (hash * 397) ^ vehicles[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
