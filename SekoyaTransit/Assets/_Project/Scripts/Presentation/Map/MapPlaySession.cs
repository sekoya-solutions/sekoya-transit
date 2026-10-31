#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SekoyaTransit.Content;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Audio;
using SekoyaTransit.Presentation.Diagnostics;
using SekoyaTransit.UI.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Play Mode bootstrap: waits on the new game screen, then generates the chosen Core world (seed and map size,
    /// Issue #67) and renders it with <see cref="ChunkedMapView"/>. No world, map view, or simulation exists
    /// until the player presses Start. Initial zoom favors inspection over fitting the whole map.
    /// Advances the simulation so road and rail vehicles move each tick.
    /// </summary>
    public sealed class MapPlaySession : MonoBehaviour
    {
        [SerializeField]
        private float _orthographicSize = 20f;

        [SerializeField]
        private TextAsset? _cargoData;

        [SerializeField]
        private TextAsset? _industriesData;

        [SerializeField]
        private TextAsset? _townsData;

        [SerializeField]
        private TextAsset? _economyData;

        [SerializeField]
        private TextAsset? _vehiclesData;

        [SerializeField]
        private TextAsset? _achievementsData;

        [SerializeField]
        private TextAsset? _contractsData;

        [SerializeField]
        private TextAsset? _worldGenData;

        [SerializeField]
        private MapArtSet? _mapArt;

        private ChunkedMapView? _mapView;
        private MapObjectView? _objectView;
        private MapTileInteractionController? _tileInteraction;
        private MapCameraController? _mapCamera;
        private MapVehicleView? _vehicleView;
        private MapIndustryCargoLabelView? _industryCargoLabels;
        private MapTownLabelView? _townLabels;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;
        private GeneratedWorld? _world;
        private ContentCatalog? _catalog;
        private SimulationLoop? _simulationLoop;
        private SimulationCalendar? _calendar;
        private readonly EventBus _events = new EventBus();
        private readonly List<IDisposable> _eventSubscriptions = new List<IDisposable>();

        // Created once: passing the method group every frame allocated a new delegate per frame (Issue #70).
        private Action<Tick>? _advanceOneSimulationTick;
        private bool _syncingSelectionFromList;
        private bool _gameOver;
        private bool _pausedByWindow;
        private SaveGameController? _saveGame;
        private IReadOnlyList<SaveSlotEntry> _saveEntries = Array.Empty<SaveSlotEntry>();
        private int _lastAutosaveMonth;
        private bool _saveInFlight;
        private bool _loadInFlight;
        private MapPlayAudio? _audio;
        private SoundSettingsStore? _soundSettings;

        /// <summary>
        /// Real-time duration of one simulation tick at normal speed; the calendar and the save slot list use it too.
        /// </summary>
        private static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// A world restored by a load while another game was running. Reloading the scene is the one way to drop every
        /// view, subscription and panel state of the old game, so the restored world waits here for the new scene.
        /// </summary>
        private static PendingLoad? _pendingLoad;

        private sealed class PendingLoad
        {
            public PendingLoad(GeneratedWorld world, Tick tick)
            {
                World = world;
                Tick = tick;
            }

            public GeneratedWorld World { get; }

            public Tick Tick { get; }
        }

        /// <summary>
        /// Gets whether a world exists, which is only the case after Start was pressed on the new game screen.
        /// </summary>
        public bool HasWorld => _world != null;

        private void Start()
        {
            _catalog = LoadCatalog();
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (shell == null)
            {
                Debug.LogWarning(
                    "MapPlayShell is missing; run Sekoya Transit/UI/Ensure Game UI Assets (or MapPlaySceneBuilder).");
                return;
            }

            shell.NewGameRequested += StartNewGame;
            shell.GameOverNewGameRequested += ReloadScene;
            _saveGame = SaveGameController.CreateDefault(new SimulationCalendar(TickDuration));
            shell.SaveRequested += OnSaveRequested;
            shell.LoadRequested += OnLoadRequested;
            shell.ContinueRequested += OnContinueRequested;
            shell.SaveSlotsRefreshRequested += RefreshSaveSlots;
            RefreshSaveSlots();
            BindSound(shell);
        }

        /// <summary>
        /// Loads the player's sound settings and connects the shell to the audio (Issue #68). Music starts with the
        /// scene, so it already plays on the new game screen.
        /// </summary>
        private void BindSound(MapPlayShell shell)
        {
            _audio = GetComponent<MapPlayAudio>();
            if (_audio == null)
            {
                Debug.LogWarning("MapPlayAudio is missing; run MapPlaySceneBuilder.EnsureMapPlaySceneFromBatch.");
            }

            _soundSettings = new SoundSettingsStore(new UnityPlayerPreferences());
            SoundSettings settings = _soundSettings.Load();
            _audio?.ApplySettings(settings);
            shell.SetSoundSettings(settings);
            shell.SoundSettingsChanged += OnSoundSettingsChanged;
            shell.ButtonClicked += OnShellButtonClicked;
            shell.AchievementsWindowOpenChanged += OnFullScreenWindowOpenChanged;
            shell.RoutesWindowOpenChanged += OnFullScreenWindowOpenChanged;
        }

        private void OnSoundSettingsChanged(SoundSettings settings)
        {
            _audio?.ApplySettings(settings);
            _soundSettings?.Change(settings, Time.unscaledTime);
        }

        private void OnShellButtonClicked()
        {
            _audio?.Play(SoundEffect.Click);
        }

        private void OnMapActionCompleted(MapPlayerAction action)
        {
            _audio?.Play(MapPlayAudio.EffectFor(action));
        }

        /// <summary>
        /// Generates the chosen world and starts the game; ignored once a world exists.
        /// </summary>
        public void StartNewGame(NewGameSelection selection)
        {
            if (_world != null || _catalog == null)
            {
                return;
            }

            MapSize mapSize = ToCoreMapSize(selection.MapSize);
            GeneratedWorld world;
            try
            {
                world = NewGame.Create(new NewGameRequest(selection.Seed, mapSize), _catalog);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("New game could not be generated: " + exception);
                return;
            }

            StartWorld(world, Tick.Zero, selection);
        }

        /// <summary>
        /// Starts the game on a world that was just generated or restored from a save, at the given tick.
        /// </summary>
        private void StartWorld(GeneratedWorld world, Tick startTick, NewGameSelection selection)
        {
            MapSize mapSize = world.Map.Size;
            _world = world;
            _commandProcessor = new SimulationCommandProcessor<GeneratedWorld>(_world);
            if (_mapArt == null)
            {
                Debug.LogError("MapPlaySession has no map art; run Sekoya Transit / Art / Bake Map Art and rebuild the scene.");
            }

            _mapView = new ChunkedMapView(_world, transform, _mapArt);
            _objectView = _mapArt != null ? new MapObjectView(_world, transform, _mapArt) : null;
            _vehicleView = new MapVehicleView(_world, transform, _mapArt);
            _industryCargoLabels = new MapIndustryCargoLabelView(_world, transform, _catalog, _mapArt, CreateLabelText());
            _townLabels = new MapTownLabelView(_world, transform, _mapArt);
            _simulationLoop = new SimulationLoop(TickDuration, startTick);
            _calendar = new SimulationCalendar(_simulationLoop);
            _lastAutosaveMonth = MonthKey(_calendar.GetDate(startTick));

            Camera? camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("MapCamera");
                camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
            camera.transform.rotation = Quaternion.identity;
            if (camera.transform.position.z == 0f)
            {
                camera.transform.position = new Vector3(0f, 0f, -10f);
            }

            MapCameraController controller = camera.GetComponent<MapCameraController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<MapCameraController>();
            }

            _mapCamera = controller;

            _tileInteraction = GetComponent<MapTileInteractionController>();
            if (_tileInteraction == null)
            {
                _tileInteraction = gameObject.AddComponent<MapTileInteractionController>();
            }

            MapPlayShell? shell = GetComponent<MapPlayShell>();
            Func<Vector2, bool>? isPointerOverUi = null;
            if (shell != null)
            {
                isPointerOverUi = shell.IsScreenPositionOverInteractiveUi;
            }

            Func<bool>? isKeyboardCapturedByUi = null;
            if (shell != null)
            {
                MapPlayShell focusShell = shell;
                isKeyboardCapturedByUi = () => focusShell.IsTextInputFocused;
            }

            controller.Configure((int)mapSize, _orthographicSize, isPointerOverUi, isKeyboardCapturedByUi);

            Func<bool>? tryConsumeEscape = null;
            if (shell != null)
            {
                MapPlayShell boundShell = shell;
                tryConsumeEscape = () =>
                {
                    if (boundShell.IsAnyRoutesPanelOpen)
                    {
                        boundShell.CloseRoutesPanels();
                        return true;
                    }

                    if (boundShell.IsAchievementsPanelOpen)
                    {
                        boundShell.SetAchievementsPanelOpen(false);
                        return true;
                    }

                    if (boundShell.IsSoundPanelOpen)
                    {
                        boundShell.SetSoundPanelOpen(false);
                        return true;
                    }

                    if (boundShell.IsSaveLoadPanelOpen)
                    {
                        boundShell.SetSaveLoadPanelOpen(false);
                        return true;
                    }

                    if (boundShell.IsUnlocksPanelOpen)
                    {
                        boundShell.SetUnlocksPanelOpen(false);
                        return true;
                    }

                    if (boundShell.IsTownsPanelOpen)
                    {
                        boundShell.SetTownsPanelOpen(false);
                        return true;
                    }

                    if (boundShell.IsIndustriesPanelOpen)
                    {
                        boundShell.SetIndustriesPanelOpen(false);
                        return true;
                    }

                    if (boundShell.IsVehiclesPanelOpen)
                    {
                        boundShell.SetVehiclesPanelOpen(false);
                        return true;
                    }

                    return false;
                };
            }

            MapPlayShell? shellForRoutes = shell;
            GeneratedWorld worldForRoutes = _world;
            SimulationCommandProcessor<GeneratedWorld> processorForRoutes = _commandProcessor;
            SimulationCalendar calendarForYear = _calendar!;
            SimulationLoop loopForYear = _simulationLoop!;
            _tileInteraction.Configure(
                _world,
                camera,
                transform,
                _commandProcessor,
                OnTileMutated,
                isPointerOverUi,
                tryConsumeEscape,
                (kind, stationId) => TryAppendStationToEditedRoute(shellForRoutes, worldForRoutes, processorForRoutes, kind, stationId),
                _catalog,
                EnterGameOver,
                () => calendarForYear.GetDate(loopForYear.CurrentTick).Year,
                isKeyboardCapturedByUi);
            _tileInteraction.ActionCompleted += OnMapActionCompleted;
            _eventSubscriptions.Add(_events.Subscribe<DeliveryIncomeEvent>(_ => _audio?.Play(SoundEffect.CashIn)));

            BindShell(shell);

            _vehicleView.Sync();
            _industryCargoLabels.Sync();
            _townLabels?.Sync();
            if (shell != null)
            {
                shell.SetGameInfo(selection);
                shell.CloseNewGameScreen();
                shell.SetSaveGameRunning(true);
            }

            if (_world.IsBankrupt)
            {
                EnterGameOver();
            }
        }

        private void OnSaveRequested(string slotId)
        {
            if (_world == null || _simulationLoop == null || _saveGame == null || _saveInFlight)
            {
                return;
            }

            SaveSnapshot snapshot = SaveSnapshot.Capture(_world.MapSeed, _simulationLoop.CurrentTick, _world);
            _ = SaveSnapshotAsync(slotId, snapshot, isAutosave: false);
        }

        /// <summary>
        /// Saves into the autosave slot on the first frame after the in-game month changes, never while another save
        /// runs and never after the game is over. A save that is still running simply postpones the autosave.
        /// </summary>
        private void AutosaveWhenMonthChanged()
        {
            if (_gameOver
                || _saveGame == null
                || _world == null
                || _world.IsBankrupt
                || _simulationLoop == null
                || _calendar == null
                || _saveInFlight)
            {
                return;
            }

            int month = MonthKey(_calendar.GetDate(_simulationLoop.CurrentTick));
            if (month == _lastAutosaveMonth)
            {
                return;
            }

            _lastAutosaveMonth = month;
            SaveSnapshot snapshot = SaveSnapshot.Capture(_world.MapSeed, _simulationLoop.CurrentTick, _world);
            _ = SaveSnapshotAsync(SaveSlotEntry.AutosaveId, snapshot, isAutosave: true);
        }

        private static int MonthKey(GameDate date)
        {
            return (date.Year * 12) + (date.Month - 1);
        }

        private async Task SaveSnapshotAsync(string slotId, SaveSnapshot snapshot, bool isAutosave)
        {
            SaveGameController saveGame = _saveGame!;
            _saveInFlight = true;
            SaveLoadStatus status = isAutosave ? SaveLoadStatus.None : SaveLoadStatus.Saved;
            try
            {
                await saveGame.SaveAsync(slotId, snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Saving slot " + slotId + " failed: " + exception);
                status = isAutosave ? SaveLoadStatus.AutosaveFailed : SaveLoadStatus.SaveFailed;
            }

            if (this == null)
            {
                return;
            }

            _saveInFlight = false;
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (shell != null && (status != SaveLoadStatus.None || !isAutosave))
            {
                shell.SetSaveLoadStatus(status, openPanel: false);
            }

            RefreshSaveSlots();
        }

        private void OnLoadRequested(string slotId)
        {
            _ = LoadSlotAsync(slotId);
        }

        private void OnContinueRequested()
        {
            if (_saveGame == null)
            {
                return;
            }

            string? slotId = SaveGameController.FindContinueSlotId(_saveEntries, _saveGame.GetLastSlotId());
            if (slotId != null)
            {
                _ = LoadSlotAsync(slotId);
            }
        }

        /// <summary>
        /// Reads a slot and restores its world. Nothing of the running game is touched until the restore succeeded, so a
        /// damaged or unsupported save leaves the game as it was and only reports the failure.
        /// </summary>
        private async Task LoadSlotAsync(string slotId)
        {
            if (_saveGame == null || _catalog == null || _loadInFlight)
            {
                return;
            }

            _loadInFlight = true;
            SaveGameController saveGame = _saveGame;
            ContentCatalog catalog = _catalog;
            SaveLoadStatus failure = SaveLoadStatus.None;
            SaveSnapshot? snapshot = null;
            GeneratedWorld? world = null;
            try
            {
                snapshot = await saveGame.LoadAsync(slotId);
                SaveSnapshot loaded = snapshot;
                world = await SaveGameController.RunInBackground(
                    () => WorldSave.Restore(loaded, catalog, new SimulationCalendar(TickDuration)));
            }
            catch (SaveFormatTooOldException exception)
            {
                Debug.LogWarning("Save slot " + slotId + " is too old: " + exception.Message);
                failure = SaveLoadStatus.TooOld;
            }
            catch (SaveFormatException exception)
            {
                Debug.LogWarning("Save slot " + slotId + " is not supported: " + exception.Message);
                failure = SaveLoadStatus.NotSupported;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Save slot " + slotId + " could not be loaded: " + exception);
                failure = SaveLoadStatus.LoadFailed;
            }

            if (this == null)
            {
                return;
            }

            _loadInFlight = false;
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (failure != SaveLoadStatus.None || snapshot == null || world == null)
            {
                shell?.SetSaveLoadStatus(failure == SaveLoadStatus.None ? SaveLoadStatus.LoadFailed : failure, openPanel: true);
                return;
            }

            saveGame.RememberLastSlot(slotId);
            if (_world == null)
            {
                StartWorld(world, snapshot.CurrentTick, new NewGameSelection(snapshot.MapSeed, ToUiMapSize(snapshot.MapSize)));
                return;
            }

            _pendingLoad = new PendingLoad(world, snapshot.CurrentTick);
            ReloadScene();
        }

        /// <summary>
        /// After a scene reload, starts the world a load left in <see cref="_pendingLoad"/> as soon as the shell exists.
        /// </summary>
        private void StartPendingLoadWhenReady()
        {
            if (_pendingLoad == null || _world != null || _catalog == null)
            {
                return;
            }

            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (shell == null || !shell.IsUiReady)
            {
                return;
            }

            PendingLoad pending = _pendingLoad;
            _pendingLoad = null;
            StartWorld(
                pending.World,
                pending.Tick,
                new NewGameSelection(pending.World.MapSeed, ToUiMapSize(pending.World.Map.Size)));
        }

        private void RefreshSaveSlots()
        {
            _ = RefreshSaveSlotsAsync();
        }

        private async Task RefreshSaveSlotsAsync()
        {
            SaveGameController? saveGame = _saveGame;
            if (saveGame == null)
            {
                return;
            }

            IReadOnlyList<SaveSlotEntry> entries;
            try
            {
                entries = await saveGame.ListEntriesAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("The save slots could not be listed: " + exception);
                return;
            }

            if (this == null)
            {
                return;
            }

            _saveEntries = SaveLoadViewModel.CompleteSlots(entries);
            GetComponent<MapPlayShell>()?.SetSaveSlots(_saveEntries);
        }

        private void ReloadScene()
        {
            // A fresh scene instance leaves no world, view, subscription, or shell state of this game behind.
            Scene scene = gameObject.scene;
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(scene.path);
            }
        }

        /// <summary>
        /// Maps a UI map size to the Core map size.
        /// </summary>
        public static MapSize ToCoreMapSize(NewGameMapSize size)
        {
            switch (size)
            {
                case NewGameMapSize.Small:
                    return MapSize.Small;
                case NewGameMapSize.Medium:
                    return MapSize.Medium;
                case NewGameMapSize.Large:
                    return MapSize.Large;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, "Map size is not supported.");
            }
        }

        /// <summary>
        /// Maps a Core map size to the UI map size (the reverse of <see cref="ToCoreMapSize"/>).
        /// </summary>
        public static NewGameMapSize ToUiMapSize(MapSize size)
        {
            switch (size)
            {
                case MapSize.Small:
                    return NewGameMapSize.Small;
                case MapSize.Medium:
                    return NewGameMapSize.Medium;
                case MapSize.Large:
                    return NewGameMapSize.Large;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, "Map size is not supported.");
            }
        }

        private void BindShell(MapPlayShell? shell)
        {
            if (_world == null
                || _tileInteraction == null
                || _simulationLoop == null
                || _calendar == null)
            {
                return;
            }

            if (shell == null)
            {
                Debug.LogWarning(
                    "MapPlayShell is missing; run Sekoya Transit/UI/Ensure Game UI Assets (or MapPlaySceneBuilder).");
                return;
            }

            GeneratedWorld world = _world;
            ContentCatalog catalog = _catalog!;
            MapTileInteractionController interaction = _tileInteraction;
            SimulationCommandProcessor<GeneratedWorld> processor = _commandProcessor!;
            SimulationLoop simulationLoop = _simulationLoop;
            SimulationCalendar calendar = _calendar;
            var dateText = new GameDateTextCache();
            MapPlayShell boundShell = shell;
            shell.Bind(
                () => world.CompanyCash,
                () => CompanyScore.Compute(world, catalog),
                interaction.SetBuildTool,
                () => BuildVehicleListItems(world, catalog, interaction.SelectedTile),
                () => BuildIndustryListItems(world, catalog),
                () => new VehiclesPanelState(
                    IsStationTile(world, interaction.SelectedTile),
                    world.CompanyCash,
                    catalog.RailWagon.Purchase),
                () => OnManageRoutes(boundShell, world, interaction.SelectedTile),
                interaction.TrySellVehicle,
                interaction.TryAddRailWagon,
                interaction.TrySellRailWagon,
                () => BuildTownListItems(world, catalog),
                townId => OnTownListRowSelected(boundShell, interaction, world, townId),
                (industryId, instanceIndex) =>
                    OnIndustryListRowSelected(boundShell, interaction, world, industryId, instanceIndex),
                () => BuildUnlockListItems(
                    world,
                    catalog,
                    calendar.GetDate(simulationLoop.CurrentTick).Year),
                typeId => interaction.TryUnlockVehicleType(typeId),
                () => dateText.Get(calendar.GetDate(simulationLoop.CurrentTick)),
                () => simulationLoop.SpeedMultiplier,
                speedMultiplier => simulationLoop.SpeedMultiplier = speedMultiplier,
                () => simulationLoop.IsPaused,
                paused => SetPlayerPaused(simulationLoop, paused));
            shell.BindRoutes(RoutesUiBuilder.CreateBindings(
                world,
                catalog,
                processor,
                () => calendar.GetDate(simulationLoop.CurrentTick).Year));

            BindGoals(
                boundShell,
                interaction,
                world,
                catalog,
                processor,
                () => calendar.GetDate(simulationLoop.CurrentTick));
            interaction.ActiveBuildToolChanged += shell.SetActiveBuildTool;
            interaction.SelectedTileChanged += tile => OnMapSelectedTileChanged(boundShell, world, tile);
            shell.SetActiveBuildTool(interaction.ActiveBuildTool);
        }

        private void BindGoals(
            MapPlayShell shell,
            MapTileInteractionController interaction,
            GeneratedWorld world,
            ContentCatalog catalog,
            SimulationCommandProcessor<GeneratedWorld> processor,
            Func<GameDate> currentDate)
        {
            shell.BindGoals(
                () => GoalsUiBuilder.BuildFinanceRecap(world),
                () => GoalsUiBuilder.BuildAchievementItems(world, catalog),
                () => GoalsUiBuilder.BuildContractsPanel(world, catalog),
                (tileX, tileY) => OnContractRowSelected(interaction, tileX, tileY),
                (action, contractId) => OnContractAction(processor, catalog, currentDate, action, contractId));
            _eventSubscriptions.Add(
                _events.Subscribe<AchievementCompletedEvent>(
                    completed => shell.ShowToast(GoalsUiBuilder.BuildAchievementToast(completed))));
            _eventSubscriptions.Add(
                _events.Subscribe<ContractOfferedEvent>(
                    offered => ShowToastIfAny(shell, GoalsUiBuilder.BuildContractOfferedToast(world, catalog, offered))));
            _eventSubscriptions.Add(
                _events.Subscribe<ContractCompletedEvent>(
                    completed => ShowToastIfAny(
                        shell,
                        GoalsUiBuilder.BuildContractCompletedToast(world, catalog, completed))));
            _eventSubscriptions.Add(
                _events.Subscribe<ContractFailedEvent>(
                    failed => ShowToastIfAny(shell, GoalsUiBuilder.BuildContractFailedToast(world, catalog, failed))));
        }

        private static void ShowToastIfAny(MapPlayShell shell, ToastMessage? message)
        {
            if (message != null)
            {
                shell.ShowToast(message);
            }
        }

        // The panel buttons run as Core commands; a rejected command (a stale click) changes nothing.
        private static void OnContractAction(
            SimulationCommandProcessor<GeneratedWorld> processor,
            ContentCatalog catalog,
            Func<GameDate> currentDate,
            ContractUiAction action,
            int contractId)
        {
            ISimulationCommand<GeneratedWorld> command;
            switch (action)
            {
                case ContractUiAction.Accept:
                    command = new AcceptContractCommand(contractId, currentDate(), catalog);
                    break;
                case ContractUiAction.Decline:
                    command = new DismissContractCommand(contractId, ContractStatus.Proposal);
                    break;
                case ContractUiAction.Cancel:
                    command = new DismissContractCommand(contractId, ContractStatus.Accepted);
                    break;
                case ContractUiAction.ClaimReward:
                    command = new ClaimContractRewardCommand(contractId, catalog);
                    break;
                case ContractUiAction.Remove:
                    command = new DismissContractCommand(contractId, ContractStatus.Failed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown contract action.");
            }

            processor.Process(Tick.Zero, command);
        }

        private void OnContractRowSelected(MapTileInteractionController interaction, int tileX, int tileY)
        {
            // Select the target tile and center the camera without switching to the towns or industries list.
            _syncingSelectionFromList = true;
            try
            {
                interaction.SetSelectedTile(new TileCoordinate(tileX, tileY));
                _mapCamera?.CenterOnTile(tileX, tileY);
            }
            finally
            {
                _syncingSelectionFromList = false;
            }
        }

        private void OnTownListRowSelected(
            MapPlayShell shell,
            MapTileInteractionController interaction,
            GeneratedWorld world,
            int townId)
        {
            if (townId < 0 || townId >= world.Towns.Count)
            {
                return;
            }

            Town town = world.Towns[townId];
            _syncingSelectionFromList = true;
            try
            {
                interaction.SetSelectedTile(town.Coordinate);
                _mapCamera?.CenterOnTile(town.Coordinate.X, town.Coordinate.Y);
                shell.SelectTownInList(townId);
            }
            finally
            {
                _syncingSelectionFromList = false;
            }
        }

        private void OnIndustryListRowSelected(
            MapPlayShell shell,
            MapTileInteractionController interaction,
            GeneratedWorld world,
            string industryId,
            int instanceIndex)
        {
            PlacedIndustry? industry = FindIndustry(world, industryId, instanceIndex);
            if (industry == null)
            {
                return;
            }

            _syncingSelectionFromList = true;
            try
            {
                interaction.SetSelectedTile(industry.Coordinate);
                _mapCamera?.CenterOnTile(industry.Coordinate.X, industry.Coordinate.Y);
                shell.SelectIndustryInList(industry.IndustryId, industry.InstanceIndex);
            }
            finally
            {
                _syncingSelectionFromList = false;
            }
        }

        private void OnMapSelectedTileChanged(MapPlayShell shell, GeneratedWorld world, TileCoordinate? tile)
        {
            if (_syncingSelectionFromList)
            {
                return;
            }

            if (!tile.HasValue)
            {
                shell.ClearListSelection();
                return;
            }

            TileCoordinate coordinate = tile.Value;
            for (int index = 0; index < world.Towns.Count; index++)
            {
                Town town = world.Towns[index];
                if (town.Coordinate.Equals(coordinate))
                {
                    shell.SelectTownInList(town.Id);
                    return;
                }
            }

            for (int index = 0; index < world.Industries.Count; index++)
            {
                PlacedIndustry industry = world.Industries[index];
                if (industry.Coordinate.Equals(coordinate))
                {
                    shell.SelectIndustryInList(industry.IndustryId, industry.InstanceIndex);
                    return;
                }
            }

            if (world.TryGetRoadStationAt(coordinate, out _) || world.TryGetRailStationAt(coordinate, out _))
            {
                shell.ClearListSelection();
                shell.SetVehiclesPanelOpen(true);
                return;
            }

            shell.ClearListSelection();
        }

        private static PlacedIndustry? FindIndustry(GeneratedWorld world, string industryId, int instanceIndex)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                PlacedIndustry industry = world.Industries[index];
                if (industry.InstanceIndex == instanceIndex
                    && string.Equals(industry.IndustryId, industryId, StringComparison.Ordinal))
                {
                    return industry;
                }
            }

            return null;
        }

        // While a route's station window is open, a station click on the map adds that station to the route; the
        // window shows why a click was refused (Issue #229). Other clicks select tiles as usual.
        private static bool TryAppendStationToEditedRoute(
            MapPlayShell? shell,
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            VehicleListKind stationKind,
            int stationId)
        {
            int? routeId = shell?.EditingRouteStationsId;
            if (shell == null || !routeId.HasValue)
            {
                return false;
            }

            shell.ShowRouteStopResult(RoutesUiBuilder.AppendStation(world, processor, routeId.Value, stationKind, stationId));
            return true;
        }

        // "Manage routes" from a selected station opens the routes window with the routes that stop there highlighted.
        private static void OnManageRoutes(MapPlayShell shell, GeneratedWorld world, TileCoordinate? selectedTile)
        {
            IReadOnlyCollection<int> routeIds = Array.Empty<int>();
            if (TryGetSelectedStation(world, selectedTile, out VehicleListKind kind, out int stationId))
            {
                routeIds = RoutesUiBuilder.FindRoutesStoppingAt(world, kind, stationId);
            }

            shell.OpenRoutesHighlighting(routeIds);
        }

        private static bool IsStationTile(GeneratedWorld world, TileCoordinate? tile)
        {
            return TryGetSelectedStation(world, tile, out _, out _);
        }

        private static bool TryGetSelectedStation(
            GeneratedWorld world,
            TileCoordinate? tile,
            out VehicleListKind kind,
            out int stationId)
        {
            kind = VehicleListKind.Road;
            stationId = -1;
            if (!tile.HasValue)
            {
                return false;
            }

            if (world.TryGetRoadStationAt(tile.Value, out RoadStation? roadStation) && roadStation != null)
            {
                stationId = roadStation.Id;
                return true;
            }

            if (world.TryGetRailStationAt(tile.Value, out RailStation? railStation) && railStation != null)
            {
                kind = VehicleListKind.Rail;
                stationId = railStation.Id;
                return true;
            }

            return false;
        }

        private static IReadOnlyList<VehicleUnlockListItem> BuildUnlockListItems(

            GeneratedWorld world,
            ContentCatalog catalog,
            int calendarYear)
        {
            long cash = world.CompanyCash;
            var matches = new List<VehicleDefinition>();
            IReadOnlyList<VehicleDefinition> vehicles = catalog.Vehicles;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleDefinition definition = vehicles[index];
                if (definition.Mode != VehicleMode.Road && definition.Mode != VehicleMode.Rail)
                {
                    continue;
                }

                matches.Add(definition);
            }

            matches.Sort(CompareVehicleByYearThenId);
            var items = new VehicleUnlockListItem[matches.Count];
            for (int index = 0; index < matches.Count; index++)
            {
                VehicleDefinition definition = matches[index];
                VehicleUnlockRowStatus status;
                if (definition.Starting || world.IsVehicleTypeUnlocked(definition.Id))
                {
                    status = VehicleUnlockRowStatus.Unlocked;
                }
                else if (calendarYear >= definition.AvailableYear)
                {
                    status = VehicleUnlockRowStatus.Unlockable;
                }
                else
                {
                    status = VehicleUnlockRowStatus.Future;
                }

                VehicleListKind kind = definition.Mode == VehicleMode.Rail
                    ? VehicleListKind.Rail
                    : VehicleListKind.Road;
                items[index] = new VehicleUnlockListItem(
                    definition.Id,
                    definition.NameKey,
                    kind,
                    definition.AvailableYear,
                    definition.UnlockCost,
                    status,
                    cash >= definition.UnlockCost);
            }

            return items;
        }

        private static int CompareVehicleByYearThenId(VehicleDefinition left, VehicleDefinition right)
        {
            int yearCompare = left.AvailableYear.CompareTo(right.AvailableYear);
            if (yearCompare != 0)
            {
                return yearCompare;
            }

            return string.CompareOrdinal(left.Id, right.Id);
        }

        // Every owned vehicle, on the map or unassigned, with its route name. While a station is selected, only the
        // vehicles of the routes that stop there are listed (Issue #229).
        private static IReadOnlyList<VehicleListItem> BuildVehicleListItems(
            GeneratedWorld world,
            ContentCatalog catalog,
            TileCoordinate? selectedTile)
        {
            bool filtered = TryGetSelectedStation(world, selectedTile, out VehicleListKind stationKind, out int stationId);
            var items = new List<VehicleListItem>();
            AddRoadItems(world, catalog, world.RoadVehicles, filtered, stationKind, stationId, items);
            AddRoadItems(world, catalog, world.UnassignedRoadVehicles, filtered, stationKind, stationId, items);
            AddRailItems(world, catalog, world.RailVehicles, filtered, stationKind, stationId, items);
            AddRailItems(world, catalog, world.UnassignedRailVehicles, filtered, stationKind, stationId, items);
            return items;
        }

        private static void AddRoadItems(
            GeneratedWorld world,
            ContentCatalog catalog,
            IReadOnlyList<RoadVehicle> vehicles,
            bool filtered,
            VehicleListKind stationKind,
            int stationId,
            List<VehicleListItem> items)
        {
            for (int index = 0; index < vehicles.Count; index++)
            {
                RoadVehicle vehicle = vehicles[index];
                if (filtered && !RunsThroughStation(world, vehicle.RouteId, VehicleListKind.Road, stationKind, stationId))
                {
                    continue;
                }

                bool assigned = vehicle.RouteId != RoadVehicle.NoRouteId;
                items.Add(new VehicleListItem(
                    VehicleListKind.Road,
                    vehicle.Id,
                    assigned
                        ? VehicleListStatus.Resolve(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop)
                        : VehicleListStatusKind.Unassigned,
                    vehicle.Cargo.TotalAmount,
                    vehicle.CargoCapacity,
                    vehicle.LifetimeDeliveryIncome,
                    monthlyUpkeep: assigned ? VehicleUpkeep.GetMonthlyUpkeep(catalog, vehicle) : 0L,
                    cargoStacks: BuildCargoStacks(vehicle.Cargo, catalog),
                    routeName: RoutesUiBuilder.FindRouteName(world, vehicle.RouteId)));
            }
        }

        private static void AddRailItems(
            GeneratedWorld world,
            ContentCatalog catalog,
            IReadOnlyList<RailVehicle> vehicles,
            bool filtered,
            VehicleListKind stationKind,
            int stationId,
            List<VehicleListItem> items)
        {
            for (int index = 0; index < vehicles.Count; index++)
            {
                RailVehicle vehicle = vehicles[index];
                if (filtered && !RunsThroughStation(world, vehicle.RouteId, VehicleListKind.Rail, stationKind, stationId))
                {
                    continue;
                }

                bool assigned = vehicle.RouteId != RoadVehicle.NoRouteId;
                items.Add(new VehicleListItem(
                    VehicleListKind.Rail,
                    vehicle.Id,
                    assigned
                        ? VehicleListStatus.Resolve(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop)
                        : VehicleListStatusKind.Unassigned,
                    vehicle.Cargo.TotalAmount,
                    vehicle.CargoCapacity,
                    vehicle.LifetimeDeliveryIncome,
                    vehicle.WagonCount,
                    vehicle.MaxWagons,
                    assigned ? VehicleUpkeep.GetMonthlyUpkeep(catalog, vehicle) : 0L,
                    BuildCargoStacks(vehicle.Cargo, catalog),
                    RoutesUiBuilder.FindRouteName(world, vehicle.RouteId)));
            }
        }

        private static bool RunsThroughStation(
            GeneratedWorld world,
            int routeId,
            VehicleListKind vehicleKind,
            VehicleListKind stationKind,
            int stationId)
        {
            return vehicleKind == stationKind
                && world.TryGetRoute(routeId, out Route? route)
                && route != null
                && RoutesUiBuilder.StopsAt(route, stationId);
        }

        private static IReadOnlyList<IndustryListItem> BuildIndustryListItems(
            GeneratedWorld world,
            ContentCatalog catalog)
        {
            IReadOnlyList<PlacedIndustry> industries = world.Industries;
            if (industries.Count == 0)
            {
                return Array.Empty<IndustryListItem>();
            }

            var items = new IndustryListItem[industries.Count];
            for (int index = 0; index < industries.Count; index++)
            {
                PlacedIndustry industry = industries[index];
                string nameKey = industry.IndustryId;
                int level = 1;
                if (catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                    definition != null)
                {
                    nameKey = definition.NameKey;
                    level = definition.Level;
                }

                items[index] = new IndustryListItem(
                    industry.IndustryId,
                    nameKey,
                    industry.InstanceIndex,
                    level,
                    industry.Cargo.TotalAmount,
                    PlacedIndustry.PlaceholderCargoCapacity,
                    industry.MonthProduced,
                    industry.MonthTransported,
                    industry.Coordinate.X,
                    industry.Coordinate.Y,
                    BuildCargoStacks(industry.Cargo, catalog));
            }

            return items;
        }

        private static CargoStackListLine[] BuildCargoStacks(CargoInventory inventory, ContentCatalog catalog)
        {
            IReadOnlyList<CargoAmount> stacks = inventory.Stacks;
            if (stacks.Count == 0)
            {
                return Array.Empty<CargoStackListLine>();
            }

            var lines = new CargoStackListLine[stacks.Count];
            for (int index = 0; index < stacks.Count; index++)
            {
                CargoAmount stack = stacks[index];
                string nameKey = stack.CargoId;
                if (catalog.TryGetCargo(stack.CargoId, out CargoDefinition? cargo) && cargo != null)
                {
                    nameKey = cargo.NameKey;
                }

                lines[index] = new CargoStackListLine(stack.CargoId, nameKey, stack.Amount);
            }

            return lines;
        }

        private static IReadOnlyList<TownListItem> BuildTownListItems(
            GeneratedWorld world,
            ContentCatalog catalog)
        {
            IReadOnlyList<Town> towns = world.Towns;
            if (towns.Count == 0)
            {
                return Array.Empty<TownListItem>();
            }

            int demand = catalog.TownNeeds.DemandPerNeedPerMonth;
            var items = new TownListItem[towns.Count];
            for (int index = 0; index < towns.Count; index++)
            {
                Town town = towns[index];
                var needLines = new TownNeedListLine[town.Needs.Count];
                for (int needIndex = 0; needIndex < town.Needs.Count; needIndex++)
                {
                    TownNeed need = town.Needs[needIndex];
                    string cargoNameKey = need.CargoId;
                    if (catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo) && cargo != null)
                    {
                        cargoNameKey = cargo.NameKey;
                    }

                    needLines[needIndex] = new TownNeedListLine(
                        need.CargoId,
                        cargoNameKey,
                        need.MonthSupply,
                        demand);
                }

                items[index] = new TownListItem(
                    town.Id,
                    town.Coordinate.X,
                    town.Coordinate.Y,
                    town.Population,
                    town.CountSatisfiedNeeds(demand),
                    town.Needs.Count,
                    needLines,
                    town.MonthPassengersGenerated,
                    town.MonthPassengersTransported,
                    town.MonthMailGenerated,
                    town.MonthMailTransported,
                    town.Name);
            }

            return items;
        }

        private void OnTileMutated(TileCoordinate coordinate)
        {
            NotifyTileChanged(coordinate);
            if (_world == null || _catalog == null)
            {
                return;
            }

            // Primary industries dump into a newly built adjacent station immediately.
            TileCoordinate? stationCoordinate = null;
            if (_world.TryGetRoadStationAt(coordinate, out RoadStation? roadStation) && roadStation != null)
            {
                stationCoordinate = roadStation.Coordinate;
            }
            else if (_world.TryGetRailStationAt(coordinate, out RailStation? railStation) && railStation != null)
            {
                stationCoordinate = railStation.Coordinate;
            }

            if (stationCoordinate.HasValue)
            {
                GameDate date = _calendar != null && _simulationLoop != null
                    ? _calendar.GetDate(_simulationLoop.CurrentTick)
                    : new GameDate(GameDate.FirstYear, 1, 1);
                IndustryProduction.AdvanceIndustriesAdjacentTo(_world, _catalog, stationCoordinate.Value, date);
                _industryCargoLabels?.Sync();
                _townLabels?.Sync();
            }
        }

        private void Update()
        {
            _soundSettings?.WriteIfDue(Time.unscaledTime);
            StartPendingLoadWhenReady();
            if (_simulationLoop == null
                || _world == null
                || _catalog == null
                || _calendar == null
                || _vehicleView == null
                || _industryCargoLabels == null)
            {
                return;
            }

            if (_gameOver || _world.IsBankrupt)
            {
                EnterGameOver();
                return;
            }

            MapPlayShell? shellForList = GetComponent<MapPlayShell>();
            Keyboard? keyboard = shellForList != null && shellForList.IsTextInputFocused ? null : Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.lKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleVehiclesPanel();
                }

                if (keyboard.iKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleIndustriesPanel();
                }

                if (keyboard.tKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleTownsPanel();
                }

                if (keyboard.fKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleFinancePanel();
                }

                if (keyboard.hKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleAchievementsPanel();
                }

                if (keyboard.kKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleContractsPanel();
                }

                if (keyboard.uKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleUnlocksPanel();
                }

                if (keyboard.oKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleRoutesPanel();
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                // F9: unused by tools/panels (Y is demolish rail station). Dev-only year skip.
                if (keyboard.f9Key.wasPressedThisFrame)
                {
                    AdvanceOneGameYearForDevelopment();
                }

                // F10: Development-only performance load of about 200 vehicles (Issue #70).
                if (keyboard.f10Key.wasPressedThisFrame)
                {
                    BuildLoadTestScenarioForDevelopment();
                }
#endif
            }

            _advanceOneSimulationTick ??= AdvanceOneSimulationTick;
            _simulationLoop.Advance(TimeSpan.FromSeconds(Time.deltaTime), _advanceOneSimulationTick);
            AutosaveWhenMonthChanged();
            _vehicleView.Sync();
            _industryCargoLabels.Sync();
            _townLabels?.Sync();
        }

        /// <summary>
        /// Shared per-tick simulation step used by the frame loop and Development year-skip.
        /// </summary>
        private void AdvanceOneSimulationTick(Tick tick)
        {
            WorldSimulationStep.Advance(_world!, _catalog!, _calendar!, tick, _events);
            ReportEventFailures(_events.DispatchPending());
            if (_world!.IsBankrupt)
            {
                EnterGameOver();
            }
        }

        private static void ReportEventFailures(EventDispatchResult result)
        {
            for (int index = 0; index < result.Failures.Count; index++)
            {
                EventDispatchFailure failure = result.Failures[index];
                Debug.LogError(
                    "Simulation event observer failed for " + failure.EventType.Name + ": " + failure.Exception);
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// Development-only: advances exactly one game year (1800s at normal speed) for unlock testing.
        /// Stripped from non-Development players. Temporarily forces speed 1 and clears pause.
        /// </summary>
        private void AdvanceOneGameYearForDevelopment()
        {
            if (_simulationLoop == null || _calendar == null || _gameOver)
            {
                return;
            }

            SimulationLoop loop = _simulationLoop;
            bool wasPaused = loop.IsPaused;
            int previousSpeed = loop.SpeedMultiplier;
            loop.IsPaused = false;
            loop.SpeedMultiplier = 1;
            try
            {
                // SimulationCalendar: one game year == 1800 real seconds at normal tick duration.
                loop.Advance(TimeSpan.FromSeconds(1800), AdvanceOneSimulationTick);
            }
            finally
            {
                loop.SpeedMultiplier = previousSpeed;
                loop.IsPaused = wasPaused;
            }

            GameDate date = _calendar.GetDate(loop.CurrentTick);
            Debug.Log("Development year-skip: calendar now " + date + " (tick " + loop.CurrentTick.Value + ").");
        }

        /// <summary>
        /// Development-only: builds the Issue #70 performance load (road and rail lines with about 200 vehicles)
        /// through the normal commands, then redraws the changed tiles. Stripped from non-Development players.
        /// </summary>
        private void BuildLoadTestScenarioForDevelopment()
        {
            if (_world == null || _catalog == null || _commandProcessor == null || _calendar == null
                || _simulationLoop == null || _mapView == null || _gameOver)
            {
                return;
            }

            LoadTestScenarioResult result = LoadTestScenario.Build(
                _world,
                _catalog,
                _commandProcessor,
                _calendar.GetDate(_simulationLoop.CurrentTick).Year);
            for (int index = 0; index < result.ChangedTiles.Count; index++)
            {
                TileCoordinate tile = result.ChangedTiles[index];
                _objectView?.RefreshTile(tile);
                _mapView.MarkTileDirty(tile.X, tile.Y);
            }

            _mapView.RebuildDirtyChunks();
            Debug.Log(
                "Development load test: " + result.RoadVehicles + " trucks on " + result.RoadLines + " road lines, " +
                result.RailVehicles + " trains on " + result.RailLines + " rail lines.");
        }
#endif

        private void EnterGameOver()
        {
            if (_gameOver)
            {
                return;
            }

            _gameOver = true;
            if (_simulationLoop != null)
            {
                _simulationLoop.IsPaused = true;
            }

            if (_tileInteraction != null)
            {
                _tileInteraction.enabled = false;
            }

            if (_world != null && _catalog != null && _simulationLoop != null && _calendar != null)
            {
                GetComponent<MapPlayShell>()?.ShowGameOver(
                    CompanyScore.Compute(_world, _catalog),
                    _calendar.GetDate(_simulationLoop.CurrentTick).ToString());
            }
        }

        // The pause button (Issue #225). Commands keep working while paused because they never go through the loop;
        // game over stays paused for good.
        private void SetPlayerPaused(SimulationLoop loop, bool paused)
        {
            if (_gameOver)
            {
                return;
            }

            loop.IsPaused = paused;
        }

        // The achievements and routes windows cover the map, so the game pauses while one is open and resumes on close
        // (Issues #223 and #229; only one can be open at a time). A pause the window did not set (game over) is left alone.
        private void OnFullScreenWindowOpenChanged(bool open)
        {
            if (_simulationLoop == null)
            {
                _pausedByWindow = false;
                return;
            }

            if (open)
            {
                _pausedByWindow = !_simulationLoop.IsPaused;
                _simulationLoop.IsPaused = true;
                return;
            }

            if (_pausedByWindow && !_gameOver)
            {
                _simulationLoop.IsPaused = false;
            }

            _pausedByWindow = false;
        }

        private void OnDestroy()
        {
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (shell != null)
            {
                shell.NewGameRequested -= StartNewGame;
                shell.GameOverNewGameRequested -= ReloadScene;
                shell.SaveRequested -= OnSaveRequested;
                shell.LoadRequested -= OnLoadRequested;
                shell.ContinueRequested -= OnContinueRequested;
                shell.SaveSlotsRefreshRequested -= RefreshSaveSlots;
                shell.SoundSettingsChanged -= OnSoundSettingsChanged;
                shell.ButtonClicked -= OnShellButtonClicked;
                shell.AchievementsWindowOpenChanged -= OnFullScreenWindowOpenChanged;
                shell.RoutesWindowOpenChanged -= OnFullScreenWindowOpenChanged;
            }

            if (shell != null && _tileInteraction != null)
            {
                _tileInteraction.ActiveBuildToolChanged -= shell.SetActiveBuildTool;
            }

            if (_tileInteraction != null)
            {
                _tileInteraction.ActionCompleted -= OnMapActionCompleted;
            }

            _soundSettings?.Flush();
            _soundSettings = null;
            _audio = null;

            for (int index = 0; index < _eventSubscriptions.Count; index++)
            {
                _eventSubscriptions[index].Dispose();
            }

            _eventSubscriptions.Clear();
            _mapView?.Dispose();
            _mapView = null;
            _objectView?.Dispose();
            _objectView = null;
            _vehicleView?.Dispose();
            _vehicleView = null;
            _industryCargoLabels?.Dispose();
            _industryCargoLabels = null;
            _townLabels?.Dispose();
            _townLabels = null;
            _tileInteraction = null;
            _mapCamera = null;
            _world = null;
            _catalog = null;
            _simulationLoop = null;
        }

        /// <summary>
        /// Marks a tile dirty and rebuilds its chunk. Intended for EditMode/smoke tests of dirty rebuild.
        /// </summary>
        public int NotifyTileChangedForTests(int tileX, int tileY)
        {
            return NotifyTileChanged(new TileCoordinate(tileX, tileY));
        }

        private int NotifyTileChanged(TileCoordinate coordinate)
        {
            if (_mapView == null)
            {
                throw new InvalidOperationException("Map view is not initialized.");
            }

            _objectView?.RefreshTile(coordinate);
            _mapView.MarkTileDirty(coordinate.X, coordinate.Y);
            return _mapView.RebuildDirtyChunks();
        }

        private IMapLabelText? CreateLabelText()
        {
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            return shell != null ? new ShellLabelText(shell) : null;
        }

        private ContentCatalog LoadCatalog()
        {
            if (_cargoData != null && _industriesData != null)
            {
                return ContentLoader.Load(
                    _cargoData,
                    _industriesData,
                    _townsData,
                    _economyData,
                    _vehiclesData,
                    _achievementsData,
                    _contractsData,
                    _worldGenData);
            }

            // Editor-only fallback: player builds (especially WebGL) must use serialized TextAssets.
            if (Application.isEditor)
            {
                string contentDir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
                string cargoPath = Path.Combine(contentDir, "cargo.json");
                string industriesPath = Path.Combine(contentDir, "industries.json");
                string townsPath = Path.Combine(contentDir, "towns.json");
                string economyPath = Path.Combine(contentDir, "economy.json");
                string vehiclesPath = Path.Combine(contentDir, "vehicles.json");
                string achievementsPath = Path.Combine(contentDir, "achievements.json");
                string contractsPath = Path.Combine(contentDir, "contracts.json");
                string worldGenPath = Path.Combine(contentDir, "worldgen.json");
                if (File.Exists(cargoPath) && File.Exists(industriesPath))
                {
                    TextAsset? townsAsset = File.Exists(townsPath)
                        ? new TextAsset(File.ReadAllText(townsPath))
                        : null;
                    TextAsset? economyAsset = File.Exists(economyPath)
                        ? new TextAsset(File.ReadAllText(economyPath))
                        : null;
                    TextAsset? achievementsAsset = File.Exists(achievementsPath)
                        ? new TextAsset(File.ReadAllText(achievementsPath))
                        : null;
                    TextAsset? contractsAsset = File.Exists(contractsPath)
                        ? new TextAsset(File.ReadAllText(contractsPath))
                        : null;
                    TextAsset? worldGenAsset = File.Exists(worldGenPath)
                        ? new TextAsset(File.ReadAllText(worldGenPath))
                        : null;
                    TextAsset? vehiclesAsset = File.Exists(vehiclesPath)
                        ? new TextAsset(File.ReadAllText(vehiclesPath))
                        : null;
                    return ContentLoader.Load(
                        new TextAsset(File.ReadAllText(cargoPath)),
                        new TextAsset(File.ReadAllText(industriesPath)),
                        townsAsset,
                        economyAsset,
                        vehiclesAsset,
                        achievementsAsset,
                        contractsAsset,
                        worldGenAsset);
                }
            }

            throw new InvalidOperationException(
                "MapPlay content TextAssets are not assigned. Re-run MapPlaySceneBuilder so cargo.json and industries.json are wired on the scene (required for WebGL).");
        }

        // Map labels read localized names from the shell string tables (Issue #193).
        private sealed class ShellLabelText : IMapLabelText
        {
            private readonly MapPlayShell _shell;

            public ShellLabelText(MapPlayShell shell)
            {
                _shell = shell;
            }

            public int Version => _shell.TextVersion;

            public string Get(string key)
            {
                return _shell.GetText(key);
            }
        }
    }
}
