#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TransportTycoon.Content;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;
using TransportTycoon.UI.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Play Mode bootstrap: generates a Core world and renders it with <see cref="ChunkedMapView"/>.
    /// Default map size is S (128). Initial zoom favors inspection over fitting the whole map.
    /// Advances the simulation so road and rail vehicles move each tick.
    /// </summary>
    public sealed class MapPlaySession : MonoBehaviour
    {
        [SerializeField]
        private int _mapSizeTiles = 128;

        [SerializeField]
        private ulong _worldSeed = 37001UL;

        [SerializeField]
        private float _orthographicSize = 20f;

        [SerializeField]
        private TextAsset? _cargoData;

        [SerializeField]
        private TextAsset? _industriesData;

        private ChunkedMapView? _mapView;
        private MapTileInteractionController? _tileInteraction;
        private MapVehicleView? _vehicleView;
        private MapIndustryCargoLabelView? _industryCargoLabels;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;
        private GeneratedWorld? _world;
        private ContentCatalog? _catalog;
        private SimulationLoop? _simulationLoop;
        private SimulationCalendar? _calendar;

        private void Start()
        {
            _catalog = LoadCatalog();
            MapSize mapSize = ResolveMapSize(_mapSizeTiles);
            WorldGenerationSettings settings = CreateDemoSettings(mapSize);
            _world = WorldGenerator.Generate(settings, _worldSeed);
            _commandProcessor = new SimulationCommandProcessor<GeneratedWorld>(_world);
            _mapView = new ChunkedMapView(_world, transform);
            _vehicleView = new MapVehicleView(_world, transform);
            _industryCargoLabels = new MapIndustryCargoLabelView(_world, transform);
            _simulationLoop = new SimulationLoop(TimeSpan.FromMilliseconds(50), Tick.Zero);
            _calendar = new SimulationCalendar(_simulationLoop);

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

            controller.Configure((int)mapSize, _orthographicSize, isPointerOverUi);

            Func<bool>? tryConsumeEscape = null;
            if (shell != null)
            {
                MapPlayShell boundShell = shell;
                tryConsumeEscape = () =>
                {
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

            MapPlayShell? shellForOrders = shell;
            _tileInteraction.Configure(
                _world,
                camera,
                transform,
                _commandProcessor,
                OnTileMutated,
                isPointerOverUi,
                tryConsumeEscape,
                stationId => shellForOrders != null && shellForOrders.TryAppendStationToSelectedOrders(stationId));

            BindShell(shell);
            if (_vehicleView != null && shell != null)
            {
                MapPlayShell boundShell = shell;
                _vehicleView.SetSelectedVehicleIdProvider(() => boundShell.SelectedVehicleIdForOrders);
            }

            _vehicleView.Sync();
            _industryCargoLabels.Sync();
        }

        private void BindShell(MapPlayShell? shell)
        {
            if (_world == null || _tileInteraction == null)
            {
                return;
            }

            if (shell == null)
            {
                Debug.LogWarning(
                    "MapPlayShell is missing; run Transport Tycoon/UI/Ensure Game UI Assets (or MapPlaySceneBuilder).");
                return;
            }

            GeneratedWorld world = _world;
            ContentCatalog catalog = _catalog!;
            MapTileInteractionController interaction = _tileInteraction;
            SimulationCommandProcessor<GeneratedWorld> processor = _commandProcessor!;
            shell.Bind(
                () => world.CompanyCash,
                interaction.SetBuildTool,
                () => BuildVehicleListItems(world),
                () => BuildIndustryListItems(world, catalog),
                interaction.TryBuyVehicleAtSelection,
                interaction.TrySellVehicle,
                vehicleId => GetVehicleOrderStationIds(world, vehicleId),
                (vehicleId, stationIds) => TryApplyVehicleOrders(processor, world, vehicleId, stationIds));
            interaction.ActiveBuildToolChanged += shell.SetActiveBuildTool;
            shell.SetActiveBuildTool(interaction.ActiveBuildTool);
        }

        private static IReadOnlyList<int> GetVehicleOrderStationIds(GeneratedWorld world, int vehicleId)
        {
            if (!world.TryGetRoadVehicle(vehicleId, out RoadVehicle? vehicle) || vehicle == null)
            {
                return Array.Empty<int>();
            }

            IReadOnlyList<RoadVehicleOrder> orders = vehicle.Orders;
            if (orders.Count == 0)
            {
                return Array.Empty<int>();
            }

            var stationIds = new int[orders.Count];
            for (int index = 0; index < orders.Count; index++)
            {
                stationIds[index] = orders[index].StationId;
            }

            return stationIds;
        }

        private static bool TryApplyVehicleOrders(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            int vehicleId,
            IReadOnlyList<int> stationIds)
        {
            if (!world.TryGetRoadVehicle(vehicleId, out RoadVehicle? vehicle) || vehicle == null)
            {
                return false;
            }

            var orders = new RoadVehicleOrder[stationIds.Count];
            for (int index = 0; index < stationIds.Count; index++)
            {
                int stationId = stationIds[index];
                bool waitForFull = false;
                for (int existing = 0; existing < vehicle.Orders.Count; existing++)
                {
                    RoadVehicleOrder prior = vehicle.Orders[existing];
                    if (prior.StationId == stationId)
                    {
                        waitForFull = prior.WaitForFull;
                        break;
                    }
                }

                orders[index] = new RoadVehicleOrder(stationId, waitForFull);
            }

            return processor.Process(Tick.Zero, new SetRoadVehicleOrdersCommand(vehicleId, orders)).IsAccepted;
        }

        private static IReadOnlyList<VehicleListItem> BuildVehicleListItems(GeneratedWorld world)
        {
            IReadOnlyList<RoadVehicle> vehicles = world.RoadVehicles;
            if (vehicles.Count == 0)
            {
                return Array.Empty<VehicleListItem>();
            }

            var items = new VehicleListItem[vehicles.Count];
            for (int index = 0; index < vehicles.Count; index++)
            {
                RoadVehicle vehicle = vehicles[index];
                items[index] = new VehicleListItem(
                    vehicle.Id,
                    VehicleListStatus.Resolve(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop),
                    vehicle.Cargo.TotalAmount,
                    vehicle.CargoCapacity,
                    vehicle.LifetimeDeliveryIncome);
            }

            return items;
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
                    industry.MonthTransported);
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
            }
        }

        private void Update()
        {
            if (_simulationLoop == null
                || _world == null
                || _catalog == null
                || _calendar == null
                || _vehicleView == null
                || _industryCargoLabels == null)
            {
                return;
            }

            Keyboard? keyboard = Keyboard.current;
            if (keyboard != null)
            {
                MapPlayShell? shellForList = GetComponent<MapPlayShell>();
                if (keyboard.lKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleVehiclesPanel();
                }

                if (keyboard.iKey.wasPressedThisFrame)
                {
                    shellForList?.ToggleIndustriesPanel();
                }
            }

            SimulationCalendar calendar = _calendar;
            _simulationLoop.Advance(
                TimeSpan.FromSeconds(Time.deltaTime),
                tick =>
                {
                    GameDate date = calendar.GetDate(tick);
                    // Shared industry load pass: road+rail unload/travel, one Distribute, then finish.
                    RoadVehicleMovement.AdvanceUnloadOrTravelAll(_world, _catalog);
                    RailVehicleMovement.AdvanceUnloadOrTravelAll(_world, _catalog);
                    IndustryCargoLoading.Distribute(_world, date);
                    RoadVehicleMovement.FinishServicesAll(_world);
                    RailVehicleMovement.FinishServicesAll(_world);
                    IndustryProduction.AdvanceAll(_world, _catalog, tick, date);
                });
            _vehicleView.Sync();
            _industryCargoLabels.Sync();
        }

        private void OnDestroy()
        {
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (shell != null && _tileInteraction != null)
            {
                _tileInteraction.ActiveBuildToolChanged -= shell.SetActiveBuildTool;
            }

            _mapView?.Dispose();
            _mapView = null;
            _vehicleView?.Dispose();
            _vehicleView = null;
            _industryCargoLabels?.Dispose();
            _industryCargoLabels = null;
            _tileInteraction = null;
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

            _mapView.MarkTileDirty(coordinate.X, coordinate.Y);
            return _mapView.RebuildDirtyChunks();
        }

        private ContentCatalog LoadCatalog()
        {
            if (_cargoData != null && _industriesData != null)
            {
                return ContentLoader.Load(_cargoData, _industriesData);
            }

            // Editor-only fallback: player builds (especially WebGL) must use serialized TextAssets.
            if (Application.isEditor)
            {
                string contentDir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
                string cargoPath = Path.Combine(contentDir, "cargo.json");
                string industriesPath = Path.Combine(contentDir, "industries.json");
                if (File.Exists(cargoPath) && File.Exists(industriesPath))
                {
                    return ContentLoader.Load(
                        new TextAsset(File.ReadAllText(cargoPath)),
                        new TextAsset(File.ReadAllText(industriesPath)));
                }
            }

            throw new InvalidOperationException(
                "MapPlay content TextAssets are not assigned. Re-run MapPlaySceneBuilder so cargo.json and industries.json are wired on the scene (required for WebGL).");
        }

        private static MapSize ResolveMapSize(int sideLength)
        {
            switch (sideLength)
            {
                case 128:
                    return MapSize.Small;
                case 256:
                    return MapSize.Medium;
                case 512:
                    return MapSize.Large;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sideLength),
                        sideLength,
                        "Map size must be 128, 256, or 512.");
            }
        }

        private static WorldGenerationSettings CreateDemoSettings(MapSize mapSize)
        {
            return new WorldGenerationSettings(
                new TownPlacementSettings(mapSize, 4, 24),
                4,
                new[]
                {
                    // Bias toward primary producers so MapPlay stations fill without a prior delivery leg.
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 4, 0),
                    new IndustryPlacementRule("iron_mine", 1, Array.Empty<string>(), new[] { "iron_ore" }, 3, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 60),
                    new IndustryPlacementRule(
                        "furniture_factory",
                        3,
                        new[] { "planks" },
                        new[] { "furniture" },
                        1,
                        60),
                });
        }
    }
}
