#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
using SekoyaTransit.Save;
using SekoyaTransit.UI.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
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
        private MapStationLabelView? _stationLabels;
        private MapCashPopupView? _cashPopups;
        private Camera? _viewCamera;
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
        private bool _downloadInFlight;
        private ISaveFileTransfer? _fileTransfer;

        /// <summary>
        /// Whether this page already looked at its <c>?save=</c> parameter (Development WebGL builds, Issue #320). Static
        /// so the scene reload of a later load does not load the URL's file again.
        /// </summary>
        private static bool _debugUrlSaveChecked;
        private MapPlayAudio? _audio;
        private SoundSettingsStore? _soundSettings;

        /// <summary>
        /// Real-time duration of one simulation tick at normal speed; the calendar and the save slot list use it too.
        /// </summary>
        /// <summary>
        /// Real time of one simulation tick at speed 1; the save file report (Issue #320) runs saves with the same value.
        /// </summary>
        internal static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(50);

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
            _fileTransfer = SaveGameController.CreateDefaultFileTransfer();
            shell.SetFileTransferAvailable(_fileTransfer.IsAvailable);
            shell.LoadFromFileRequested += OnLoadFromFileRequested;
            shell.DebugDownloadRequested += OnDebugDownloadRequested;
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
        private int[] GetTownTierMinPopulations()
        {
            if (_catalog == null)
            {
                return Array.Empty<int>();
            }

            IReadOnlyList<TownNeedTier> tiers = _catalog.TownNeeds.Tiers;
            var minPopulations = new int[tiers.Count];
            for (int index = 0; index < tiers.Count; index++)
            {
                minPopulations[index] = tiers[index].MinPopulation;
            }

            return minPopulations;
        }

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
            int[] townTierMinPopulations = GetTownTierMinPopulations();
            _objectView = _mapArt != null ? new MapObjectView(_world, transform, _mapArt, townTierMinPopulations) : null;
            _vehicleView = new MapVehicleView(_world, transform, _mapArt);
            _industryCargoLabels = new MapIndustryCargoLabelView(_world, transform, _catalog, _mapArt, CreateLabelText());
            _townLabels = new MapTownLabelView(_world, transform, _mapArt, townTierMinPopulations);
            _stationLabels = new MapStationLabelView(_world, transform, CreateLabelText());
            MapPlayShell? cultureShell = GetComponent<MapPlayShell>();
            _cashPopups = new MapCashPopupView(
                transform,
                _mapArt?.CashIcon,
                () => cultureShell != null ? cultureShell.Culture : CultureInfo.GetCultureInfo("en"));
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
            _viewCamera = camera;

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

                    if (boundShell.IsFinancePanelOpen)
                    {
                        boundShell.SetFinancePanelOpen(false);
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
            _eventSubscriptions.Add(_events.Subscribe<DeliveryIncomeEvent>(OnDeliveryIncome));

            BindShell(shell);

            _vehicleView.Sync();
            _industryCargoLabels.Sync();
            _townLabels?.Sync();
            _stationLabels?.Sync();
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

        private Task LoadSlotAsync(string slotId)
        {
            return LoadSaveAsync("Save slot " + slotId, saveGame => saveGame.LoadAsync(slotId), slotId);
        }

        /// <summary>
        /// Load from file was chosen (Issue #320): opens the browser file picker at once, inside the player's click, which
        /// the browser requires.
        /// </summary>
        private void OnLoadFromFileRequested()
        {
            if (_fileTransfer == null || !_fileTransfer.IsAvailable || _saveGame == null)
            {
                return;
            }

            Task<byte[]?> pick;
            try
            {
                pick = _fileTransfer.PickFileAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("The file picker could not be opened: " + exception);
                GetComponent<MapPlayShell>()?.SetSaveLoadStatus(SaveLoadStatus.FileFailed, openPanel: true);
                return;
            }

            _ = LoadPickedFileAsync(pick);
        }

        /// <summary>
        /// Waits for the picked file and loads it like a slot. A cancelled pick changes nothing and leaves the panel
        /// open; a file that cannot be opened or read shows its own status.
        /// </summary>
        private async Task LoadPickedFileAsync(Task<byte[]?> pick)
        {
            byte[]? data;
            try
            {
                data = await pick;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("The chosen save file could not be opened: " + exception.Message);
                if (this != null)
                {
                    GetComponent<MapPlayShell>()?.SetSaveLoadStatus(SaveLoadStatus.FileFailed, openPanel: true);
                }

                return;
            }

            if (this == null || data == null)
            {
                return;
            }

            GetComponent<MapPlayShell>()?.CloseSaveLoadPanelForFileLoad();
            byte[] bytes = data;
            await LoadSaveAsync("Save file", saveGame => saveGame.ImportAsync(bytes), null);
        }

        /// <summary>
        /// The Debug button was pressed (Issue #320): captures the running game at the current tick and hands it to the
        /// browser as a download named after the game date. Ignored without a running game or while a download runs.
        /// </summary>
        private void OnDebugDownloadRequested()
        {
            if (_world == null
                || _simulationLoop == null
                || _calendar == null
                || _saveGame == null
                || _fileTransfer == null
                || !_fileTransfer.IsAvailable
                || _downloadInFlight)
            {
                return;
            }

            SaveSnapshot snapshot = SaveSnapshot.Capture(_world.MapSeed, _simulationLoop.CurrentTick, _world);
            string fileName = SaveGameController.DebugFileName(_calendar.GetDate(_simulationLoop.CurrentTick));
            _ = DownloadSnapshotAsync(_saveGame, _fileTransfer, snapshot, fileName);
        }

        private async Task DownloadSnapshotAsync(
            SaveGameController saveGame,
            ISaveFileTransfer fileTransfer,
            SaveSnapshot snapshot,
            string fileName)
        {
            _downloadInFlight = true;
            bool failed = false;
            try
            {
                byte[] data = await saveGame.ExportAsync(snapshot);
                fileTransfer.Download(fileName, data);
                Debug.Log("Debug download: " + fileName + " (" + data.Length + " bytes, tick " + snapshot.CurrentTick.Value + ").");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("The debug download " + fileName + " failed: " + exception);
                failed = true;
            }

            if (this == null)
            {
                return;
            }

            _downloadInFlight = false;
            if (failed)
            {
                GetComponent<MapPlayShell>()?.SetSaveLoadStatus(SaveLoadStatus.DownloadFailed, openPanel: true);
            }
        }

        /// <summary>
        /// Development WebGL builds only (Issue #320): once the shell is ready, loads the save file named by the page's
        /// <c>?save=</c> parameter from the page's own folder, so an agent can open a bug save in a browser that cannot
        /// pick files. Checked once per page: a scene reload never loads it again. Other builds ignore the parameter.
        /// </summary>
        private void LoadDebugUrlSaveWhenReady()
        {
            if (_debugUrlSaveChecked)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (_catalog == null || _saveGame == null || shell == null || !shell.IsUiReady)
            {
                return;
            }

            _debugUrlSaveChecked = true;
            string pageUrl = Application.absoluteURL;
            if (!Debug.isDebugBuild || _world != null || !DebugSaveUrl.TryGetFileName(pageUrl, out string fileName))
            {
                return;
            }

            _ = LoadDebugUrlSaveAsync(DebugSaveUrl.ResolveFileUrl(pageUrl, fileName));
#else
            _debugUrlSaveChecked = true;
#endif
        }

        private async Task LoadDebugUrlSaveAsync(string url)
        {
            byte[]? data = null;
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                var completion = new TaskCompletionSource<bool>();
                request.SendWebRequest().completed += _ => completion.TrySetResult(true);
                await completion.Task;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    data = request.downloadHandler.data;
                }
                else
                {
                    Debug.LogWarning("The debug save " + url + " could not be fetched: " + request.error);
                }
            }

            if (this == null)
            {
                return;
            }

            if (data == null || data.Length == 0)
            {
                GetComponent<MapPlayShell>()?.SetSaveLoadStatus(SaveLoadStatus.LoadFailed, openPanel: true);
                return;
            }

            Debug.Log("Loading the debug save " + url + " (" + data.Length + " bytes).");
            byte[] bytes = data;
            await LoadSaveAsync("Debug save " + url, saveGame => saveGame.ImportAsync(bytes), null);
        }

        /// <summary>
        /// Reads a save (a slot, a file or the debug URL; <paramref name="source"/> names it in the log) and restores its
        /// world. Nothing of the running game is touched until the restore succeeded, so a damaged or unsupported save
        /// leaves the game as it was and only reports the failure. A slot load remembers <paramref name="slotId"/> for
        /// Continue.
        /// </summary>
        private async Task LoadSaveAsync(
            string source,
            Func<SaveGameController, Task<SaveSnapshot>> read,
            string? slotId)
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
                snapshot = await read(saveGame);
                SaveSnapshot loaded = snapshot;
                world = await SaveGameController.RunInBackground(
                    () => WorldSave.Restore(loaded, catalog, new SimulationCalendar(TickDuration)));
            }
            catch (SaveFormatTooOldException exception)
            {
                Debug.LogWarning(source + " is too old: " + exception.Message);
                failure = SaveLoadStatus.TooOld;
            }
            catch (SaveFormatException exception)
            {
                Debug.LogWarning(source + " is not supported: " + exception.Message);
                failure = SaveLoadStatus.NotSupported;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(source + " could not be loaded: " + exception);
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

            if (slotId != null)
            {
                saveGame.RememberLastSlot(slotId);
            }

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
                interaction.SetBuildTool,
                () => BuildVehicleListItems(world, catalog, interaction.SelectedTile),
                () => BuildIndustryListItems(world, catalog),
                () => new VehiclesPanelState(
                    IsStationTile(world, interaction.SelectedTile),
                    world.CompanyCash,
                    catalog.RailWagon.Purchase,
                    EconomySettings.HalfRefund(catalog.RailWagon.Purchase)),
                routeId => boundShell.OpenRoutesHighlighting(new[] { routeId }),
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
                () => dateText.Get(calendar.GetDate(simulationLoop.CurrentTick), boundShell.Culture),
                () => simulationLoop.SpeedMultiplier,
                speedMultiplier => simulationLoop.SpeedMultiplier = speedMultiplier,
                () => simulationLoop.IsPaused,
                paused => SetPlayerPaused(simulationLoop, paused));
            shell.BindTownGrowthRules(CreateTownGrowthRules(catalog.TownNeeds));
            shell.BindIndustryGrowthRules(CreateIndustryGrowthRules(catalog.Economy.IndustryGrowth));
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
            shell.RouteStationsEditingChanged += interaction.SetBuildLocked;
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
                () => GoalsUiBuilder.BuildFinanceTable(world),
                () => GoalsUiBuilder.BuildAchievementItems(world, catalog, shell.Culture),
                () => GoalsUiBuilder.BuildContractsPanel(world, catalog, shell.Culture),
                () => GoalsUiBuilder.BuildActiveContracts(world, currentDate()),
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
                if (SiteFootprint.Contains(town.Coordinate, coordinate, SiteFootprint.TownSize))
                {
                    shell.SelectTownInList(town.Id);
                    return;
                }
            }

            for (int index = 0; index < world.Industries.Count; index++)
            {
                PlacedIndustry industry = world.Industries[index];
                if (SiteFootprint.Contains(industry.Coordinate, coordinate, SiteFootprint.IndustrySize))
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
                else if (VehicleAvailability.IsAvailable(definition, world, calendarYear))
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
                    cash >= definition.UnlockCost,
                    new VehicleUnlockStats(
                        definition.Mode == VehicleMode.Rail ? definition.CapacityPerWagon : definition.CargoCapacity,
                        definition.Mode == VehicleMode.Rail ? definition.MaxWagons : 0,
                        UnlocksViewModel.SpeedTenthsPerSecond(definition.SpeedThousandths, TickDuration.TotalSeconds),
                        definition.Purchase,
                        definition.Mode == VehicleMode.Rail ? definition.GetMonthlyUpkeep(1) : definition.GetMonthlyUpkeep(0),
                        definition.Mode == VehicleMode.Rail
                            ? definition.GetMonthlyUpkeep(definition.MaxWagons)
                            : definition.GetMonthlyUpkeep(0)));
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

        // Every owned vehicle with its route name: the vehicles on routes first, then the unassigned ones, which the
        // panel lists under their own heading. While a station is selected, only the vehicles of the routes that stop
        // there and the unassigned vehicles of the station's kind are listed (Issue #229).
        private static IReadOnlyList<VehicleListItem> BuildVehicleListItems(
            GeneratedWorld world,
            ContentCatalog catalog,
            TileCoordinate? selectedTile)
        {
            bool filtered = TryGetSelectedStation(world, selectedTile, out VehicleListKind stationKind, out int stationId);
            var items = new List<VehicleListItem>();
            AddRoadItems(world, catalog, world.RoadVehicles, filtered, stationKind, stationId, items);
            AddRailItems(world, catalog, world.RailVehicles, filtered, stationKind, stationId, items);
            AddRoadItems(world, catalog, world.UnassignedRoadVehicles, filtered, stationKind, stationId, items);
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
                    routeName: RoutesUiBuilder.FindRouteName(world, vehicle.RouteId),
                    routeId: vehicle.RouteId,
                    hasLastMonth: vehicle.HasLastMonth,
                    lastMonthIncome: vehicle.LastMonthDeliveryIncome,
                    sellValue: GetRoadSellValue(catalog, vehicle)));
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
                    RoutesUiBuilder.FindRouteName(world, vehicle.RouteId),
                    vehicle.RouteId,
                    vehicle.HasLastMonth,
                    vehicle.LastMonthDeliveryIncome,
                    catalog.SellRefundForRail(vehicle.TypeId, vehicle.WagonCount)));
            }
        }

        // The cash a sale pays, as SellRoadVehicleCommand credits it.
        private static long GetRoadSellValue(ContentCatalog catalog, RoadVehicle vehicle)
        {
            return catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? definition) && definition != null
                ? EconomySettings.HalfRefund(definition.Purchase)
                : 0L;
        }

        private static bool RunsThroughStation(
            GeneratedWorld world,
            int routeId,
            VehicleListKind vehicleKind,
            VehicleListKind stationKind,
            int stationId)
        {
            if (vehicleKind != stationKind)
            {
                return false;
            }

            // Unassigned vehicles run through no station, so they are hidden while a station is selected.
            return routeId != RoadVehicle.NoRouteId
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

            IndustryGrowthSettings growth = catalog.Economy.IndustryGrowth;
            var items = new IndustryListItem[industries.Count];
            for (int index = 0; index < industries.Count; index++)
            {
                PlacedIndustry industry = industries[index];
                string nameKey = industry.IndustryId;
                int level = 1;
                bool canGrow = false;
                var outputCargoIds = new List<string>();
                if (catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                    definition != null)
                {
                    nameKey = definition.NameKey;
                    level = definition.Level;
                    canGrow = definition.Inputs.Count == 0 && growth.MaxLevel > 0;
                    for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
                    {
                        outputCargoIds.Add(definition.Outputs[outputIndex].CargoId);
                    }
                }

                items[index] = new IndustryListItem(
                    industry.IndustryId,
                    nameKey,
                    industry.InstanceIndex,
                    level,
                    industry.Cargo.TotalAmount,
                    industry.CargoCapacity,
                    industry.MonthProduced,
                    industry.MonthTransported,
                    industry.Coordinate.X,
                    industry.Coordinate.Y,
                    BuildCargoStacks(industry.Cargo, catalog),
                    industry.LastMonthProduced,
                    industry.LastMonthTransported,
                    canGrow,
                    canGrow ? industry.ProductionLevel : 0,
                    canGrow ? growth.MaxLevel : 0,
                    industry.ServedMonths,
                    growth.MonthsToGrow,
                    outputCargoIds);
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

        private static IndustryGrowthRulesInfo? CreateIndustryGrowthRules(IndustryGrowthSettings growth)
        {
            if (growth.MaxLevel <= 0)
            {
                return null;
            }

            return new IndustryGrowthRulesInfo(
                growth.ServedPercent,
                growth.MonthsToGrow,
                growth.LevelMultiplier,
                growth.MaxLevel + 1);
        }

        private static TownGrowthRulesInfo CreateTownGrowthRules(TownNeedsSettings settings)
        {
            TownNeedLevelAmounts partlyMet = settings.PopulationPerPartlyMetNeed;
            TownNeedLevelAmounts fullyMet = settings.PopulationPerFullyMetNeed;
            return new TownGrowthRulesInfo(
                partlyMet.Level1,
                partlyMet.Level2,
                partlyMet.Level3,
                fullyMet.Level1,
                fullyMet.Level2,
                fullyMet.Level3,
                settings.AllNeedsMetGrowthMultiplier,
                settings.PartlyMetMinDemandPercent);
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

            TownNeedsSettings settings = catalog.TownNeeds;
            int maxGrowthLevel = settings.Tiers.Count;
            var items = new TownListItem[towns.Count];
            for (int index = 0; index < towns.Count; index++)
            {
                Town town = towns[index];
                var needLines = new TownNeedListLine[town.Needs.Count];
                for (int needIndex = 0; needIndex < town.Needs.Count; needIndex++)
                {
                    needLines[needIndex] = TownNeedLines.Create(town.Needs[needIndex], catalog);
                }

                int growthLevel = settings.GetTierIndexForPopulation(town.Population) + 1;
                items[index] = new TownListItem(
                    town.Id,
                    town.Coordinate.X,
                    town.Coordinate.Y,
                    town.Population,
                    growthLevel,
                    maxGrowthLevel,
                    growthLevel < maxGrowthLevel ? settings.Tiers[growthLevel].MinPopulation : 0,
                    town.LastMonthGrowth,
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
                _stationLabels?.Sync();
            }
        }

        private void Update()
        {
            _soundSettings?.WriteIfDue(Time.unscaledTime);
            StartPendingLoadWhenReady();
            LoadDebugUrlSaveWhenReady();
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

                // Playtest cheats (Issue #316) in every build, on the top-row digit keys read by physical position (the
                // keys above A, Z, E and R on AZERTY): browsers do not use them, unlike F9 to F12.
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    AdvanceGameCalendarForCheat(1);
                }

                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    AdvanceGameCalendarForCheat(12);
                }

                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    ProcessCheatCommand(new CheatAddCashCommand());
                }

                if (keyboard.digit4Key.wasPressedThisFrame)
                {
                    ProcessCheatCommand(new CheatUnlockAllVehicleTypesCommand(_catalog));
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
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
            _objectView?.RefreshTownTiers();
            _vehicleView.Sync();
            _industryCargoLabels.Sync();
            _townLabels?.Sync();
            _stationLabels?.Sync();
            _cashPopups?.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Shows a cash popup over each paid station in the camera view and plays the cash-in sound once when at
        /// least one of them is in view (Issue #284).
        /// </summary>
        private void OnDeliveryIncome(DeliveryIncomeEvent incomeEvent)
        {
            if (_viewCamera == null)
            {
                return;
            }

            Vector2 center = _viewCamera.transform.position;
            float size = _viewCamera.orthographicSize;
            float aspect = _viewCamera.aspect;
            bool anyVisible = false;
            for (int index = 0; index < incomeEvent.Payments.Count; index++)
            {
                DeliveryPayment payment = incomeEvent.Payments[index];
                Vector3 position = IsometricProjection.TileToWorld(payment.Station.X, payment.Station.Y);
                if (!MapCameraMath.IsPointInView(position, center, size, aspect))
                {
                    continue;
                }

                anyVisible = true;
                _cashPopups?.Show(payment.Station, payment.Income);
            }

            if (anyVisible)
            {
                _audio?.Play(SoundEffect.CashIn);
            }
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

        /// <summary>
        /// Playtest cheat (Issue #316): runs the simulation tick by tick until the calendar reaches the same day
        /// <paramref name="months"/> months later (clamped to the target month's length), whatever the pause state and
        /// game speed, which are restored afterwards. A bankruptcy on the way stops the skip.
        /// </summary>
        private void AdvanceGameCalendarForCheat(int months)
        {
            if (_simulationLoop == null || _calendar == null || _world == null || _gameOver)
            {
                return;
            }

            SimulationLoop loop = _simulationLoop;
            SimulationCalendar calendar = _calendar;
            GeneratedWorld world = _world;
            GameDate target = calendar.GetDate(loop.CurrentTick).AddMonths(months);
            bool wasPaused = loop.IsPaused;
            int previousSpeed = loop.SpeedMultiplier;
            loop.IsPaused = false;
            loop.SpeedMultiplier = 1;
            _advanceOneSimulationTick ??= AdvanceOneSimulationTick;
            try
            {
                while (calendar.GetDate(loop.CurrentTick) < target && !world.IsBankrupt)
                {
                    // One tick duration at speed 1 always runs at least one tick; stop if nothing ran (paused meanwhile).
                    long before = loop.CurrentTick.Value;
                    loop.Advance(loop.TickDuration, _advanceOneSimulationTick);
                    if (loop.CurrentTick.Value == before)
                    {
                        break;
                    }
                }
            }
            finally
            {
                loop.SpeedMultiplier = previousSpeed;
                loop.IsPaused = wasPaused;
            }

            Debug.Log("Cheat: calendar now " + calendar.GetDate(loop.CurrentTick) + " (tick " + loop.CurrentTick.Value + ").");
        }

        /// <summary>
        /// Playtest cheat (Issue #316): processes a cheat command at the current tick and logs the outcome.
        /// </summary>
        private void ProcessCheatCommand(ISimulationCommand<GeneratedWorld> command)
        {
            if (_commandProcessor == null || _simulationLoop == null || _gameOver)
            {
                return;
            }

            CommandProcessingResult result = _commandProcessor.Process(_simulationLoop.CurrentTick, command);
            Debug.Log("Cheat " + command.GetType().Name + ": "
                + (result.IsAccepted ? "applied." : "rejected (" + result.RejectionReason + ")."));
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
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
                MapPlayShell? shell = GetComponent<MapPlayShell>();
                // Formatted once: the language cannot change while the game-over screen shows, because the chooser is
                // only on the start menu and this screen leaves only by reloading the scene (Issue #338).
                shell?.ShowGameOver(
                    CompanyScore.Compute(_world, _catalog),
                    GameDateText.Format(_calendar.GetDate(_simulationLoop.CurrentTick), shell.Culture));
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
                shell.RouteStationsEditingChanged -= _tileInteraction.SetBuildLocked;
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
            _stationLabels?.Dispose();
            _stationLabels = null;
            _cashPopups?.Dispose();
            _cashPopups = null;
            _tileInteraction = null;
            _mapCamera = null;
            _viewCamera = null;
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
