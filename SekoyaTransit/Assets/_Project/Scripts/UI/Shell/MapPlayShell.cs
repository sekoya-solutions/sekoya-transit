#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// MapPlay HUD shell: localized cash, score, date, speed controls, hint, and build toolbar, and the wiring of the
    /// panel hosts (<see cref="ListShellPanels"/>, <see cref="GoalsShellPanels"/>, <see cref="NewGameShellPanels"/>).
    /// Does not mutate simulation state; Presentation supplies data and command callbacks.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapPlayShell : MonoBehaviour
    {
        private static readonly BuildToolId[] ToolbarTools =
        {
            BuildToolId.Select,
            BuildToolId.Road,
            BuildToolId.Rail,
            BuildToolId.Station,
            BuildToolId.RailStation,
            BuildToolId.Demolish,
        };

        [SerializeField]
        private CargoIconSet? _cargoIcons;

        private Label? _cashLabel;
        private VisualElement? _cashIcon;
        private ShellTooltip? _tooltip;
        private Label? _scoreLabel;
        private Label? _calendarDateLabel;
        private Button? _speedPause;
        private Button? _speedX1;
        private Button? _speedX2;
        private Button? _speedX3;
        private Label? _gameOverTitle;
        private Label? _gameOverMessage;
        private VisualElement? _gameOverPanel;
        private VisualElement? _buildToolbar;
        private VisualElement? _hudPanel;
        private VisualElement? _root;
        private GoalsShellPanels? _goals;
        private ListShellPanels? _lists;
        private SaveLoadShellPanels? _saveLoad;
        private SoundShellPanels? _sound;
        private SoundSettings _pendingSoundSettings = SoundSettings.Default;
        private IReadOnlyList<SaveSlotEntry>? _pendingSaveSlots;
        private bool _pendingSaveGameRunning;
        private ListPanelBindings? _listBindings;
        private readonly Queue<(ToastMessage Message, float Time)> _pendingToasts = new Queue<(ToastMessage, float)>();
        private long _gameOverSummaryScore;
        private string? _gameOverSummaryDate;
        private Func<FinanceRecap>? _financeProvider;
        private Func<IReadOnlyList<AchievementTrackItem>>? _achievementsProvider;
        private Func<IReadOnlyList<ContractListItem>>? _contractsProvider;
        private Action<int, int>? _onContractRowSelected;
        private readonly Dictionary<BuildToolId, Button> _toolButtons = new Dictionary<BuildToolId, Button>();
        private readonly Dictionary<BuildToolId, Action> _toolClickHandlers = new Dictionary<BuildToolId, Action>();
        private bool _pointerOverChrome;
        private int _blockMapInputUntilFrame = -1;
        private Func<long>? _cashProvider;
        private Func<long>? _scoreProvider;
        private Func<string>? _calendarDateProvider;
        private SimulationSpeedSelection? _simulationSpeedSelection;
        private Action<BuildToolId>? _onToolChosen;
        private string _cashTemplate = "{0}";
        private string _scoreTemplate = "Score: {0}";
        private string _calendarDateTemplate = "Date: {0}";
        private CultureInfo _culture = CultureInfo.GetCultureInfo("en");
        private long _lastCash = long.MinValue;
        private long _lastScore = long.MinValue;
        private string? _lastCalendarDate;
        private int _lastSimulationSpeed = int.MinValue;
        private bool _lastSimulationPaused;
        private BuildToolId _activeTool = BuildToolId.Select;
        private bool _localizationReady;
        private bool _uiReady;
        private bool _gameOverShown;
        private NewGameShellPanels? _newGame;
        private NewGameSelection? _pendingGameInfo;

        /// <summary>
        /// Binds cash, score, tool-selection, town/industry/vehicle list, and vehicle-order callbacks from Presentation.
        /// </summary>
        public void Bind(
            Func<long> cashProvider,
            Func<long> scoreProvider,
            Action<BuildToolId> onToolChosen,
            Func<IReadOnlyList<VehicleListItem>> vehiclesProvider,
            Func<IReadOnlyList<IndustryListItem>> industriesProvider,
            Action<string> onBuyVehicleType,
            Func<VehiclePurchaseUiState> purchaseUiProvider,
            Action<VehicleListKind, int> onSellVehicle,
            Action<int> onAddRailWagon,
            Action<int> onSellRailWagon,
            Func<VehicleListKind, int, IReadOnlyList<int>> getVehicleOrderStationIds,
            Func<VehicleListKind, int, IReadOnlyList<int>, bool> tryApplyVehicleOrders,
            Func<IReadOnlyList<TownListItem>> townsProvider,
            Action<int> onTownRowSelected,
            Action<string, int> onIndustryRowSelected,
            Func<IReadOnlyList<VehicleUnlockListItem>> unlocksProvider,
            Action<string> onUnlockVehicleType,
            Func<string> calendarDateProvider,
            Func<int> simulationSpeedProvider,
            Action<int> onSimulationSpeedChanged,
            Func<bool> simulationPausedProvider,
            Action<bool> onSimulationPausedChanged)
        {
            _cashProvider = cashProvider ?? throw new ArgumentNullException(nameof(cashProvider));
            _scoreProvider = scoreProvider ?? throw new ArgumentNullException(nameof(scoreProvider));
            _calendarDateProvider = calendarDateProvider ?? throw new ArgumentNullException(nameof(calendarDateProvider));
            _simulationSpeedSelection = new SimulationSpeedSelection(
                simulationSpeedProvider,
                onSimulationSpeedChanged,
                simulationPausedProvider,
                onSimulationPausedChanged);
            _onToolChosen = onToolChosen ?? throw new ArgumentNullException(nameof(onToolChosen));
            _listBindings = new ListPanelBindings(
                vehiclesProvider,
                industriesProvider,
                townsProvider,
                unlocksProvider,
                purchaseUiProvider,
                onBuyVehicleType,
                onUnlockVehicleType,
                onSellVehicle,
                onAddRailWagon,
                onSellRailWagon,
                getVehicleOrderStationIds,
                tryApplyVehicleOrders,
                onTownRowSelected,
                onIndustryRowSelected);
            _lastCash = long.MinValue;
            _lastScore = long.MinValue;
            _lists?.Bind(_listBindings);
        }

        /// <summary>
        /// Updates toolbar highlight and hint for the active tool (keyboard or UI).
        /// </summary>
        public void SetActiveBuildTool(BuildToolId tool)
        {
            _activeTool = tool;
            RefreshToolHighlight();
        }

        /// <summary>
        /// Shows the terminal game-over screen with the final score and bankruptcy date, and blocks map interaction.
        /// </summary>
        public void ShowGameOver(long finalScore, string bankruptcyDate)
        {
            _gameOverSummaryScore = finalScore;
            _gameOverSummaryDate = bankruptcyDate ?? throw new ArgumentNullException(nameof(bankruptcyDate));
            _goals?.SetGameOverSummary(finalScore, bankruptcyDate);
            _gameOverShown = true;
            ApplyGameOverVisibility();
        }

        /// <summary>
        /// Binds the finance recap, achievements, and contracts data and the contract-row callback from Presentation
        /// (Issue #66). May be called before or after the shell UI is ready.
        /// </summary>
        public void BindGoals(
            Func<FinanceRecap> financeProvider,
            Func<IReadOnlyList<AchievementTrackItem>> achievementsProvider,
            Func<IReadOnlyList<ContractListItem>> contractsProvider,
            Action<int, int> onContractRowSelected)
        {
            _financeProvider = financeProvider ?? throw new ArgumentNullException(nameof(financeProvider));
            _achievementsProvider = achievementsProvider ?? throw new ArgumentNullException(nameof(achievementsProvider));
            _contractsProvider = contractsProvider ?? throw new ArgumentNullException(nameof(contractsProvider));
            _onContractRowSelected = onContractRowSelected
                ?? throw new ArgumentNullException(nameof(onContractRowSelected));
            _goals?.Bind(financeProvider, achievementsProvider, contractsProvider, onContractRowSelected);
        }

        /// <summary>
        /// Shows a transient toast. It never pauses the game and hides on its own.
        /// </summary>
        public void ShowToast(ToastMessage message)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            _pendingToasts.Enqueue((message, Time.unscaledTime));
            FlushPendingToasts();
        }

        /// <summary>
        /// Opens or closes the finance panel (shell button or keyboard F).
        /// </summary>
        public void ToggleFinancePanel()
        {
            _goals?.ToggleFinance();
        }

        /// <summary>
        /// Opens or closes the achievements panel (shell button or keyboard H).
        /// </summary>
        public void ToggleAchievementsPanel()
        {
            _goals?.ToggleAchievements();
        }

        /// <summary>
        /// Opens or closes the achievements window (close cross or Escape).
        /// </summary>
        public void SetAchievementsPanelOpen(bool open)
        {
            _goals?.SetAchievementsOpen(open);
        }

        /// <summary>
        /// Opens or closes the contracts panel (shell button or keyboard K).
        /// </summary>
        public void ToggleContractsPanel()
        {
            _goals?.ToggleContracts();
        }

        /// <summary>
        /// Gets whether the finance panel is open.
        /// </summary>
        public bool IsFinancePanelOpen => _goals != null && _goals.IsFinanceOpen;

        /// <summary>
        /// Gets whether the achievements panel is open.
        /// </summary>
        public bool IsAchievementsPanelOpen => _goals != null && _goals.IsAchievementsOpen;

        /// <summary>
        /// Raised with the new state when the achievements window opens or closes; the session pauses the game while it
        /// is open (Issue #223).
        /// </summary>
        public event Action<bool>? AchievementsWindowOpenChanged;

        /// <summary>
        /// Gets whether the contracts panel is open.
        /// </summary>
        public bool IsContractsPanelOpen => _goals != null && _goals.IsContractsOpen;

        /// <summary>
        /// Gets a counter that changes whenever the list panels load text (map labels, Issue #193).
        /// </summary>
        public int TextVersion => _lists?.TextVersion ?? 0;

        /// <summary>
        /// Gets loaded text for a key, such as an industry name, or the key while it is not loaded.
        /// </summary>
        public string GetText(string key)
        {
            return _lists != null ? _lists.GetText(key) : key;
        }

        /// <summary>
        /// Opens or closes the vehicles panel (shell button or keyboard L).
        /// </summary>
        public void ToggleVehiclesPanel()
        {
            _lists?.ToggleVehicles();
        }

        /// <summary>
        /// Opens or closes the unlocks panel (shell button or keyboard U).
        /// </summary>
        public void ToggleUnlocksPanel()
        {
            _lists?.ToggleUnlocks();
        }

        /// <summary>
        /// Opens or closes the industries panel (shell button or keyboard I).
        /// </summary>
        public void ToggleIndustriesPanel()
        {
            _lists?.ToggleIndustries();
        }

        /// <summary>
        /// Opens or closes the towns panel (shell button or keyboard T).
        /// </summary>
        public void ToggleTownsPanel()
        {
            _lists?.ToggleTowns();
        }

        /// <summary>
        /// Sets vehicles panel visibility. Opening closes unlocks, industries, towns, and the goals panels.
        /// </summary>
        public void SetVehiclesPanelOpen(bool open)
        {
            _lists?.SetVehiclesOpen(open);
        }

        /// <summary>
        /// Sets unlocks panel visibility. Opening closes vehicles, industries, towns, and the goals panels.
        /// </summary>
        public void SetUnlocksPanelOpen(bool open)
        {
            _lists?.SetUnlocksOpen(open);
        }

        /// <summary>
        /// Sets industries panel visibility. Opening closes vehicles, unlocks, towns, and the goals panels.
        /// </summary>
        public void SetIndustriesPanelOpen(bool open)
        {
            _lists?.SetIndustriesOpen(open);
        }

        /// <summary>
        /// Sets towns panel visibility. Opening closes vehicles, unlocks, industries, and the goals panels.
        /// </summary>
        public void SetTownsPanelOpen(bool open)
        {
            _lists?.SetTownsOpen(open);
        }

        /// <summary>
        /// Gets whether the vehicles panel is open.
        /// </summary>
        public bool IsVehiclesPanelOpen => _lists != null && _lists.IsVehiclesOpen;

        /// <summary>
        /// Gets whether the unlocks panel is open.
        /// </summary>
        public bool IsUnlocksPanelOpen => _lists != null && _lists.IsUnlocksOpen;

        /// <summary>
        /// Gets whether the industries panel is open.
        /// </summary>
        public bool IsIndustriesPanelOpen => _lists != null && _lists.IsIndustriesOpen;

        /// <summary>
        /// Gets whether the towns panel is open.
        /// </summary>
        public bool IsTownsPanelOpen => _lists != null && _lists.IsTownsOpen;

        /// <summary>
        /// Gets the town id selected in the towns list, if any.
        /// </summary>
        public int? SelectedTownId => _lists?.SelectedTownId;

        /// <summary>
        /// Gets whether an industry row is selected in the industries list.
        /// </summary>
        public bool HasSelectedIndustry => _lists != null && _lists.HasSelectedIndustry;

        /// <summary>
        /// Opens the towns panel and selects the given town row.
        /// </summary>
        public void SelectTownInList(int townId)
        {
            _lists?.SelectTownInList(townId);
        }

        /// <summary>
        /// Opens the industries panel and selects the given industry instance row.
        /// </summary>
        public void SelectIndustryInList(string industryId, int instanceIndex)
        {
            if (industryId is null)
            {
                throw new ArgumentNullException(nameof(industryId));
            }

            _lists?.SelectIndustryInList(industryId, instanceIndex);
        }

        /// <summary>
        /// Clears town and industry list selection and refreshes row highlights when panels are open.
        /// </summary>
        public void ClearListSelection()
        {
            _lists?.ClearListSelection();
        }

        /// <summary>
        /// Gets whether a vehicle is selected for route editing while the vehicles panel is open.
        /// </summary>
        public bool IsEditingVehicleOrders => _lists != null && _lists.IsEditingVehicleOrders;

        /// <summary>
        /// Gets the vehicle id selected for route editing, or null when none / panel closed.
        /// </summary>
        public int? SelectedVehicleIdForOrders => _lists?.SelectedVehicleIdForOrders;

        /// <summary>
        /// Gets the vehicle kind selected for route editing, or null when none / panel closed.
        /// </summary>
        public VehicleListKind? SelectedVehicleKindForOrders => _lists?.SelectedVehicleKindForOrders;

        /// <summary>
        /// Appends a station to the selected vehicle's draft route (map click while editing).
        /// Station kind must match the selected vehicle kind (road↔road station, rail↔rail station).
        /// </summary>
        /// <returns>True when the station was appended.</returns>
        public bool TryAppendStationToSelectedOrders(VehicleListKind stationKind, int stationId)
        {
            return _lists != null && _lists.TryAppendStationToSelectedOrders(stationKind, stationId);
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

            return IsNewGameScreenOpen || _pointerOverChrome || Time.frameCount <= _blockMapInputUntilFrame;
        }

        /// <summary>
        /// Raised when the player presses Start on the new game screen with a valid seed (Issue #67).
        /// </summary>
        public event Action<NewGameSelection>? NewGameRequested;

        /// <summary>
        /// Raised when the player presses New game on the game-over screen (Issue #67).
        /// </summary>
        public event Action? GameOverNewGameRequested;

        /// <summary>
        /// Raised when the player presses Save on a manual slot of the save and load panel (Issue #183); the argument is
        /// the slot number.
        /// </summary>
        public event Action<string>? SaveRequested;

        /// <summary>
        /// Raised when a load is chosen in the save and load panel (and confirmed while a game runs); the argument is
        /// the slot id.
        /// </summary>
        public event Action<string>? LoadRequested;

        /// <summary>
        /// Raised when the player presses Continue on the new game screen.
        /// </summary>
        public event Action? ContinueRequested;

        /// <summary>
        /// Raised when the save and load panel opens, so Presentation refreshes the slot list.
        /// </summary>
        public event Action? SaveSlotsRefreshRequested;

        /// <summary>
        /// Gets whether the save and load panel is open.
        /// </summary>
        public bool IsSaveLoadPanelOpen => _saveLoad != null && _saveLoad.IsOpen;

        /// <summary>
        /// Opens or closes the save and load panel.
        /// </summary>
        public void SetSaveLoadPanelOpen(bool open)
        {
            _saveLoad?.SetOpen(open);
        }

        /// <summary>
        /// Replaces the slot list of the save and load panel; Continue and Load on the new game screen need a save.
        /// </summary>
        public void SetSaveSlots(IReadOnlyList<SaveSlotEntry> slots)
        {
            _pendingSaveSlots = slots ?? throw new ArgumentNullException(nameof(slots));
            _saveLoad?.SetSlots(slots);
        }

        /// <summary>
        /// Records whether a game is running: saving needs one and loading asks for confirmation.
        /// </summary>
        public void SetSaveGameRunning(bool running)
        {
            _pendingSaveGameRunning = running;
            _saveLoad?.SetGameRunning(running);
        }

        /// <summary>
        /// Shows the outcome of the last save or load; a failure opens the panel so it is not missed.
        /// </summary>
        public void SetSaveLoadStatus(SaveLoadStatus status, bool openPanel)
        {
            _saveLoad?.SetStatus(status, openPanel);
        }

        /// <summary>
        /// Raised after the player clicks any enabled shell button (Issue #68), so Presentation plays the click sound.
        /// </summary>
        public event Action? ButtonClicked;

        /// <summary>
        /// Raised when the player changes a volume or the mute toggle in the sound panel (Issue #68).
        /// </summary>
        public event Action<SoundSettings>? SoundSettingsChanged;

        /// <summary>
        /// Gets whether the sound panel is open.
        /// </summary>
        public bool IsSoundPanelOpen => _sound != null && _sound.IsOpen;

        /// <summary>
        /// Opens or closes the sound panel.
        /// </summary>
        public void SetSoundPanelOpen(bool open)
        {
            _sound?.SetOpen(open);
        }

        /// <summary>
        /// Shows the stored sound settings in the sound panel without raising <see cref="SoundSettingsChanged"/>.
        /// </summary>
        public void SetSoundSettings(SoundSettings settings)
        {
            _pendingSoundSettings = settings;
            _sound?.SetSettings(settings);
        }

        /// <summary>
        /// Gets whether the new game screen is open. The map, camera, and shortcuts ignore input meanwhile.
        /// </summary>
        public bool IsNewGameScreenOpen => _newGame == null || _newGame.IsNewGameScreenOpen;

        /// <summary>
        /// Gets whether the shell finished building its UI, so screens can be opened and closed.
        /// </summary>
        public bool IsUiReady => _uiReady;

        /// <summary>
        /// Hides the new game screen once the chosen world exists.
        /// </summary>
        public void CloseNewGameScreen()
        {
            _newGame?.CloseNewGameScreen();
        }

        /// <summary>
        /// Shows the seed and map size of the running game in the side panel and on the game-over screen.
        /// </summary>
        public void SetGameInfo(NewGameSelection selection)
        {
            _pendingGameInfo = selection;
            _newGame?.SetGameInfo(selection);
        }

        private IEnumerator Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;
            _cashLabel = _root.Q<Label>("cash-label");
            _cashIcon = _root.Q<VisualElement>("cash-icon");
            _scoreLabel = _root.Q<Label>("score-label");
            _calendarDateLabel = _root.Q<Label>("calendar-date-label");
            _speedPause = _root.Q<Button>("speed-pause");
            _speedX1 = _root.Q<Button>("speed-x1");
            _speedX2 = _root.Q<Button>("speed-x2");
            _speedX3 = _root.Q<Button>("speed-x3");
            _gameOverPanel = _root.Q<VisualElement>("game-over-panel");
            _gameOverTitle = _root.Q<Label>("game-over-title");
            _gameOverMessage = _root.Q<Label>("game-over-message");
            _buildToolbar = _root.Q("build-toolbar");
            _hudPanel = _root.Q("hud-panel");
            if (_cashLabel == null
                || _cashIcon == null
                || _scoreLabel == null
                || _calendarDateLabel == null
                || _speedPause == null
                || _speedX1 == null
                || _speedX2 == null
                || _speedX3 == null
                || _gameOverPanel == null
                || _gameOverTitle == null
                || _gameOverMessage == null
                || _buildToolbar == null
                || _hudPanel == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define cash, score, calendar date, speed controls, hint, game-over, HUD, and toolbar elements.");
            }

            BindToolbarButtons(_root);
            _tooltip = new ShellTooltip(_root);
            _hudPanel.pickingMode = PickingMode.Position;
            _buildToolbar.pickingMode = PickingMode.Position;
            _gameOverPanel.pickingMode = PickingMode.Position;
            // TrickleDown on root so we see pointer targets under Ignore shell-root (map pass-through).
            _root.RegisterCallback<PointerMoveEvent>(OnShellPointerMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerDownEvent>(OnShellPointerDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerLeaveEvent>(OnShellPointerLeave);
            RegisterChromePointerTracking(_hudPanel);
            RegisterChromePointerTracking(_buildToolbar);
            RegisterChromePointerTracking(_gameOverPanel);
            _newGame = new NewGameShellPanels(_root, new NewGameRandomSeed().Next);
            _newGame.NewGameRequested += OnNewGameScreenRequested;
            _newGame.GameOverNewGameRequested += OnGameOverNewGameRequested;
            RegisterChromePointerTracking(_newGame.Panel);
            if (_pendingGameInfo.HasValue)
            {
                _newGame.SetGameInfo(_pendingGameInfo.Value);
            }

            _lists = new ListShellPanels(_root, () => _culture, CloseGoalsPanelsForLists, _cargoIcons);
            _lists.ForEachPanel(RegisterChromePointerTracking);
            if (_listBindings != null)
            {
                _lists.Bind(_listBindings);
            }

            _goals = new GoalsShellPanels(_root, () => _culture, CloseListPanelsForGoals, _cargoIcons);
            _goals.AchievementsOpenChanged += OnAchievementsOpenChanged;
            _goals.ForEachPanel(RegisterChromePointerTracking);
            if (_financeProvider != null
                && _achievementsProvider != null
                && _contractsProvider != null
                && _onContractRowSelected != null)
            {
                _goals.Bind(_financeProvider, _achievementsProvider, _contractsProvider, _onContractRowSelected);
            }

            if (_gameOverSummaryDate != null)
            {
                _goals.SetGameOverSummary(_gameOverSummaryScore, _gameOverSummaryDate);
            }

            _saveLoad = new SaveLoadShellPanels(_root, () => _culture, CloseOtherPanelsForSaveLoad);
            RegisterChromePointerTracking(_saveLoad.Panel.Panel);
            _saveLoad.SaveRequested += OnSaveRequested;
            _saveLoad.LoadRequested += OnLoadRequested;
            _saveLoad.ContinueRequested += OnContinueRequested;
            _saveLoad.SlotsRefreshRequested += OnSaveSlotsRefreshRequested;
            _saveLoad.SetGameRunning(_pendingSaveGameRunning);
            if (_pendingSaveSlots != null)
            {
                _saveLoad.SetSlots(_pendingSaveSlots);
            }

            _sound = new SoundShellPanels(_root, () => _culture, CloseOtherPanelsForSound);
            RegisterChromePointerTracking(_sound.Panel.Panel);
            _sound.SettingsChanged += OnSoundSettingsChanged;
            _sound.SetSettings(_pendingSoundSettings);
            // Bubble phase: a button's own click handler has run, so the sound follows the action.
            _root.RegisterCallback<ClickEvent>(OnShellClick);

            FlushPendingToasts();
            _speedPause.clicked += OnSpeedPauseClicked;
            _speedX1.clicked += OnSpeedX1Clicked;
            _speedX2.clicked += OnSpeedX2Clicked;
            _speedX3.clicked += OnSpeedX3Clicked;
            ApplyGameOverVisibility();
            _uiReady = true;
            RefreshCalendarDate(force: true);
            RefreshSimulationSpeed(force: true);

            yield return LocalizationSettings.InitializationOperation;
            ApplyCultureFromLocale(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            RefreshStaticTexts();
            _localizationReady = true;
            _goals?.LoadTexts();
            _lists?.LoadTexts();
            _newGame?.LoadTexts();
            _saveLoad?.LoadTexts();
            _sound?.LoadTexts();
            RefreshCash(force: true);
            RefreshScore(force: true);
            RefreshToolHighlight();
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

            if (_speedPause != null)
            {
                _speedPause.clicked -= OnSpeedPauseClicked;
            }

            if (_speedX1 != null)
            {
                _speedX1.clicked -= OnSpeedX1Clicked;
            }

            if (_speedX2 != null)
            {
                _speedX2.clicked -= OnSpeedX2Clicked;
            }

            if (_speedX3 != null)
            {
                _speedX3.clicked -= OnSpeedX3Clicked;
            }

            if (_root != null)
            {
                _root.UnregisterCallback<PointerMoveEvent>(OnShellPointerMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerDownEvent>(OnShellPointerDown, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerLeaveEvent>(OnShellPointerLeave);
                _root.UnregisterCallback<ClickEvent>(OnShellClick);
            }

            _tooltip?.Dispose();
            _tooltip = null;

            UnregisterChromePointerTracking(_hudPanel);
            UnregisterChromePointerTracking(_buildToolbar);
            UnregisterChromePointerTracking(_gameOverPanel);
            if (_newGame != null)
            {
                UnregisterChromePointerTracking(_newGame.Panel);
                _newGame.Dispose();
                _newGame = null;
            }

            if (_saveLoad != null)
            {
                UnregisterChromePointerTracking(_saveLoad.Panel.Panel);
                _saveLoad.Dispose();
                _saveLoad = null;
            }

            if (_sound != null)
            {
                UnregisterChromePointerTracking(_sound.Panel.Panel);
                _sound.Dispose();
                _sound = null;
            }

            if (_lists != null)
            {
                _lists.ForEachPanel(UnregisterChromePointerTracking);
                _lists.Dispose();
                _lists = null;
            }

            if (_goals != null)
            {
                _goals.ForEachPanel(UnregisterChromePointerTracking);
                _goals.AchievementsOpenChanged -= OnAchievementsOpenChanged;
                _goals.Dispose();
                _goals = null;
            }

            _listBindings = null;
            _financeProvider = null;
            _achievementsProvider = null;
            _contractsProvider = null;
            _onContractRowSelected = null;
            _pendingToasts.Clear();
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            _cashProvider = null;
            _scoreProvider = null;
            _calendarDateProvider = null;
            _simulationSpeedSelection = null;
            _onToolChosen = null;
            _cashLabel = null;
            _cashIcon = null;
            _scoreLabel = null;
            _calendarDateLabel = null;
            _speedPause = null;
            _speedX1 = null;
            _speedX2 = null;
            _speedX3 = null;
            _gameOverTitle = null;
            _gameOverMessage = null;
            _gameOverPanel = null;
            _buildToolbar = null;
            _hudPanel = null;
            _root = null;
            _pointerOverChrome = false;
            _blockMapInputUntilFrame = -1;
            _uiReady = false;
            _lastCalendarDate = null;
            _lastSimulationSpeed = int.MinValue;
            _lastSimulationPaused = false;
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
                || IsListOrGoalsPanel(evt.target as VisualElement))
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

        private bool IsListOrGoalsPanel(VisualElement? element)
        {
            return (_lists != null && _lists.IsPanel(element))
                || (_goals != null && _goals.IsPanel(element))
                || (_saveLoad != null && _saveLoad.IsPanel(element))
                || (_sound != null && _sound.IsPanel(element));
        }

        private bool IsUnderChrome(VisualElement? element)
        {
            VisualElement? current = element;
            while (current != null && current != _root)
            {
                if (current == _buildToolbar
                    || current == _hudPanel
                    || IsListOrGoalsPanel(current)
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
            RefreshScore(force: false);
            RefreshCalendarDate(force: false);
            RefreshSimulationSpeed(force: false);
            _goals?.Update(Time.unscaledTime);
            _lists?.Update();
            _newGame?.Update();
            _saveLoad?.Update();
            _sound?.Update();
        }

        private void RefreshCalendarDate(bool force)
        {
            if (!_uiReady || _calendarDateLabel == null || _calendarDateProvider == null)
            {
                return;
            }

            string date = _calendarDateProvider();
            if (!force && string.Equals(date, _lastCalendarDate, StringComparison.Ordinal))
            {
                return;
            }

            _calendarDateLabel.text = CalendarDateFormat.FormatLabeled(
                _calendarDateTemplate,
                date,
                _culture);
            _lastCalendarDate = date;
        }

        private void RefreshSimulationSpeed(bool force)
        {
            if (!_uiReady
                || _speedPause == null
                || _speedX1 == null
                || _speedX2 == null
                || _speedX3 == null
                || _simulationSpeedSelection == null)
            {
                return;
            }

            int multiplier = _simulationSpeedSelection.CurrentMultiplier;
            bool paused = _simulationSpeedSelection.IsPaused;
            if (!force && multiplier == _lastSimulationSpeed && paused == _lastSimulationPaused)
            {
                return;
            }

            _speedPause.EnableInClassList("simulation-speed--selected", paused);

            _speedX1.EnableInClassList(
                "simulation-speed--selected",
                _simulationSpeedSelection.IsSelected(1));
            _speedX2.EnableInClassList(
                "simulation-speed--selected",
                _simulationSpeedSelection.IsSelected(2));
            _speedX3.EnableInClassList(
                "simulation-speed--selected",
                _simulationSpeedSelection.IsSelected(3));
            _lastSimulationSpeed = multiplier;
            _lastSimulationPaused = paused;
        }

        private void SelectSimulationSpeed(int multiplier)
        {
            if (_simulationSpeedSelection == null)
            {
                return;
            }

            _simulationSpeedSelection.Select(multiplier);
            RefreshSimulationSpeed(force: true);
        }

        private void OnSpeedPauseClicked()
        {
            if (_simulationSpeedSelection == null)
            {
                return;
            }

            _simulationSpeedSelection.TogglePause();
            RefreshSimulationSpeed(force: true);
        }

        private void OnSpeedX1Clicked()
        {
            SelectSimulationSpeed(1);
        }

        private void OnSpeedX2Clicked()
        {
            SelectSimulationSpeed(2);
        }

        private void OnSpeedX3Clicked()
        {
            SelectSimulationSpeed(3);
        }

        private void FlushPendingToasts()
        {
            if (_goals == null)
            {
                return;
            }

            while (_pendingToasts.Count > 0)
            {
                (ToastMessage message, float time) = _pendingToasts.Dequeue();
                _goals.ShowToast(message, time);
            }
        }

        private void CloseOtherPanelsForSaveLoad()
        {
            _lists?.CloseAll();
            _goals?.CloseAll();
            _sound?.SetOpen(false);
        }

        private void CloseOtherPanelsForSound()
        {
            _lists?.CloseAll();
            _goals?.CloseAll();
            _saveLoad?.SetOpen(false);
        }

        private void OnSoundSettingsChanged(SoundSettings settings)
        {
            _pendingSoundSettings = settings;
            SoundSettingsChanged?.Invoke(settings);
        }

        private void OnShellClick(ClickEvent evt)
        {
            if (IsButtonOrInsideButton(evt.target as VisualElement, _root))
            {
                ButtonClicked?.Invoke();
            }
        }

        /// <summary>
        /// Returns whether <paramref name="element"/> is an enabled button or sits inside one (an icon or label child),
        /// below <paramref name="root"/>.
        /// </summary>
        public static bool IsButtonOrInsideButton(VisualElement? element, VisualElement? root)
        {
            for (VisualElement? current = element; current != null && current != root; current = current.parent)
            {
                if (current is Button)
                {
                    return current.enabledInHierarchy;
                }
            }

            return false;
        }

        private void OnSaveRequested(string slotId)
        {
            SaveRequested?.Invoke(slotId);
        }

        private void OnLoadRequested(string slotId)
        {
            // Swallow clicks that arrive while the session is already handling the load.
            _blockMapInputUntilFrame = Time.frameCount + 1;
            LoadRequested?.Invoke(slotId);
        }

        private void OnContinueRequested()
        {
            _blockMapInputUntilFrame = Time.frameCount + 1;
            ContinueRequested?.Invoke();
        }

        private void OnSaveSlotsRefreshRequested()
        {
            SaveSlotsRefreshRequested?.Invoke();
        }

        private void OnAchievementsOpenChanged(bool open)
        {
            AchievementsWindowOpenChanged?.Invoke(open);
        }

        private void CloseListPanelsForGoals()
        {
            _lists?.CloseAll();
            _saveLoad?.SetOpen(false);
            _sound?.SetOpen(false);
        }

        private void CloseGoalsPanelsForLists()
        {
            _goals?.CloseAll();
            _saveLoad?.SetOpen(false);
            _sound?.SetOpen(false);
        }

        private void BindToolbarButtons(VisualElement root)
        {
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            RegisterToolButton(root, "tool-select", BuildToolId.Select);
            RegisterToolButton(root, "tool-road", BuildToolId.Road);
            RegisterToolButton(root, "tool-rail", BuildToolId.Rail);
            RegisterToolButton(root, "tool-station", BuildToolId.Station);
            RegisterToolButton(root, "tool-rail-station", BuildToolId.RailStation);
            RegisterToolButton(root, "tool-demolish", BuildToolId.Demolish);
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

        private void OnNewGameScreenRequested(NewGameSelection selection)
        {
            // Swallow clicks that arrive while Start is already being handled by the session.
            _blockMapInputUntilFrame = Time.frameCount + 1;
            NewGameRequested?.Invoke(selection);
        }

        private void OnGameOverNewGameRequested()
        {
            GameOverNewGameRequested?.Invoke();
        }

        private void OnLocaleChanged(Locale locale)
        {
            ApplyCultureFromLocale(locale);
            _goals?.LoadTexts();
            _lists?.LoadTexts();
            _newGame?.LoadTexts();
            _saveLoad?.LoadTexts();
            _sound?.LoadTexts();
            RefreshStaticTexts();
            RefreshCash(force: true);
            RefreshScore(force: true);
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

            ApplyLocalizedString(GameUiKeys.CashTooltip, value =>
            {
                if (_cashIcon != null)
                {
                    _cashIcon.tooltip = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.Score, value =>
            {
                _scoreTemplate = value;
                RefreshScore(force: true);
            });

            ApplyLocalizedString(GameUiKeys.CalendarDate, value =>
            {
                _calendarDateTemplate = value;
                RefreshCalendarDate(force: true);
            });

            ApplyLocalizedString(GameUiKeys.SpeedPause, value =>
            {
                if (_speedPause != null)
                {
                    ShellTooltip.SetIconButtonName(_speedPause, value);
                }
            });

            ApplyLocalizedString(GameUiKeys.SpeedX1, value =>
            {
                if (_speedX1 != null)
                {
                    ShellTooltip.SetIconButtonName(_speedX1, value);
                }
            });

            ApplyLocalizedString(GameUiKeys.SpeedX2, value =>
            {
                if (_speedX2 != null)
                {
                    ShellTooltip.SetIconButtonName(_speedX2, value);
                }
            });

            ApplyLocalizedString(GameUiKeys.SpeedX3, value =>
            {
                if (_speedX3 != null)
                {
                    ShellTooltip.SetIconButtonName(_speedX3, value);
                }
            });

            ApplyLocalizedString(GameUiKeys.GameOverTitle, value =>
            {
                if (_gameOverTitle != null)
                {
                    _gameOverTitle.text = value;
                }
            });

            ApplyLocalizedString(GameUiKeys.GameOverMessage, value =>
            {
                if (_gameOverMessage != null)
                {
                    _gameOverMessage.text = value;
                }
            });

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
                        ShellTooltip.SetIconButtonName(live, value);
                    }
                });
            }
        }

        private void ApplyGameOverVisibility()
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.style.display = _gameOverShown ? DisplayStyle.Flex : DisplayStyle.None;
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

        private void RefreshScore(bool force)
        {
            if (!_localizationReady || _scoreLabel == null || _scoreProvider == null)
            {
                return;
            }

            long score = _scoreProvider();
            if (!force && score == _lastScore)
            {
                return;
            }

            _lastScore = score;
            _scoreLabel.text = CashFormat.FormatLabeled(_scoreTemplate, score, _culture);
        }
    }
}
