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

        private ChunkedMapView? _mapView;
        private MapTileInteractionController? _tileInteraction;
        private MapCameraController? _mapCamera;
        private MapVehicleView? _vehicleView;
        private MapIndustryCargoLabelView? _industryCargoLabels;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;
        private GeneratedWorld? _world;
        private ContentCatalog? _catalog;
        private SimulationLoop? _simulationLoop;
        private SimulationCalendar? _calendar;
        private readonly EventBus _events = new EventBus();
        private readonly List<IDisposable> _eventSubscriptions = new List<IDisposable>();
        private bool _syncingSelectionFromList;
        private bool _gameOver;

        private void Start()
        {
            _catalog = LoadCatalog();
            MapSize mapSize = ResolveMapSize(_mapSizeTiles);
            WorldGenerationSettings settings = CreateDemoSettings(mapSize);
            _world = WorldGenerator.Generate(settings, _worldSeed, _catalog.TownNeeds, _catalog);
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

            controller.Configure((int)mapSize, _orthographicSize, isPointerOverUi);

            Func<bool>? tryConsumeEscape = null;
            if (shell != null)
            {
                MapPlayShell boundShell = shell;
                tryConsumeEscape = () =>
                {
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

            MapPlayShell? shellForOrders = shell;
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
                (kind, stationId) =>
                    shellForOrders != null && shellForOrders.TryAppendStationToSelectedOrders(kind, stationId),
                _catalog,
                EnterGameOver,
                () => calendarForYear.GetDate(loopForYear.CurrentTick).Year);

            BindShell(shell);
            if (_vehicleView != null && shell != null)
            {
                MapPlayShell boundShell = shell;
                _vehicleView.SetSelectedVehicleProviders(
                    () => boundShell.SelectedVehicleIdForOrders,
                    () => boundShell.SelectedVehicleKindForOrders);
            }

            _vehicleView.Sync();
            _industryCargoLabels.Sync();
            if (_world.IsBankrupt)
            {
                EnterGameOver();
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
                    "MapPlayShell is missing; run Transport Tycoon/UI/Ensure Game UI Assets (or MapPlaySceneBuilder).");
                return;
            }

            GeneratedWorld world = _world;
            ContentCatalog catalog = _catalog!;
            MapTileInteractionController interaction = _tileInteraction;
            SimulationCommandProcessor<GeneratedWorld> processor = _commandProcessor!;
            SimulationLoop simulationLoop = _simulationLoop;
            SimulationCalendar calendar = _calendar;
            MapPlayShell boundShell = shell;
            shell.Bind(
                () => world.CompanyCash,
                () => CompanyScore.Compute(world, catalog),
                interaction.SetBuildTool,
                () => BuildVehicleListItems(world, catalog),
                () => BuildIndustryListItems(world, catalog),
                typeId => interaction.TryBuyVehicleAtSelection(typeId),
                () => BuildPurchaseUiState(world, catalog, interaction, calendar, simulationLoop),
                interaction.TrySellVehicle,
                interaction.TryAddRailWagon,
                interaction.TrySellRailWagon,
                (kind, vehicleId) => GetVehicleOrderStationIds(world, kind, vehicleId),
                (kind, vehicleId, stationIds) => TryApplyVehicleOrders(processor, world, kind, vehicleId, stationIds),
                () => BuildTownListItems(world, catalog),
                townId => OnTownListRowSelected(boundShell, interaction, world, townId),
                (industryId, instanceIndex) =>
                    OnIndustryListRowSelected(boundShell, interaction, world, industryId, instanceIndex),
                () => BuildUnlockListItems(
                    world,
                    catalog,
                    calendar.GetDate(simulationLoop.CurrentTick).Year),
                typeId => interaction.TryUnlockVehicleType(typeId),
                () => calendar.GetDate(simulationLoop.CurrentTick).ToString(),
                () => simulationLoop.SpeedMultiplier,
                speedMultiplier => simulationLoop.SpeedMultiplier = speedMultiplier);
            BindGoals(boundShell, interaction, world, catalog);
            interaction.ActiveBuildToolChanged += shell.SetActiveBuildTool;
            interaction.SelectedTileChanged += tile => OnMapSelectedTileChanged(boundShell, world, tile);
            shell.SetActiveBuildTool(interaction.ActiveBuildTool);
        }

        private void BindGoals(
            MapPlayShell shell,
            MapTileInteractionController interaction,
            GeneratedWorld world,
            ContentCatalog catalog)
        {
            shell.BindGoals(
                () => GoalsUiBuilder.BuildFinanceRecap(world),
                () => GoalsUiBuilder.BuildAchievementItems(world, catalog),
                () => GoalsUiBuilder.BuildContractItems(world, catalog),
                (tileX, tileY) => OnContractRowSelected(interaction, tileX, tileY));
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
                _events.Subscribe<ContractExpiredEvent>(
                    expired => ShowToastIfAny(shell, GoalsUiBuilder.BuildContractExpiredToast(world, catalog, expired))));
        }

        private static void ShowToastIfAny(MapPlayShell shell, ToastMessage? message)
        {
            if (message != null)
            {
                shell.ShowToast(message);
            }
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

        private static IReadOnlyList<int> GetVehicleOrderStationIds(
            GeneratedWorld world,
            VehicleListKind kind,
            int vehicleId)
        {
            if (kind == VehicleListKind.Rail)
            {
                if (!world.TryGetRailVehicle(vehicleId, out RailVehicle? railVehicle) || railVehicle == null)
                {
                    return Array.Empty<int>();
                }

                IReadOnlyList<RailVehicleOrder> railOrders = railVehicle.Orders;
                if (railOrders.Count == 0)
                {
                    return Array.Empty<int>();
                }

                var railStationIds = new int[railOrders.Count];
                for (int index = 0; index < railOrders.Count; index++)
                {
                    railStationIds[index] = railOrders[index].StationId;
                }

                return railStationIds;
            }

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

        private bool TryApplyVehicleOrders(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            VehicleListKind kind,
            int vehicleId,
            IReadOnlyList<int> stationIds)
        {
            if (kind == VehicleListKind.Rail)
            {
                if (!world.TryGetRailVehicle(vehicleId, out RailVehicle? railVehicle) || railVehicle == null)
                {
                    return false;
                }

                var railOrders = new RailVehicleOrder[stationIds.Count];
                for (int index = 0; index < stationIds.Count; index++)
                {
                    int stationId = stationIds[index];
                    bool waitForFull = false;
                    for (int existing = 0; existing < railVehicle.Orders.Count; existing++)
                    {
                        RailVehicleOrder prior = railVehicle.Orders[existing];
                        if (prior.StationId == stationId)
                        {
                            waitForFull = prior.WaitForFull;
                            break;
                        }
                    }

                    railOrders[index] = new RailVehicleOrder(stationId, waitForFull);
                }

                return processor.Process(Tick.Zero, new SetRailVehicleOrdersCommand(vehicleId, railOrders, _catalog)).IsAccepted;
            }

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

            return processor.Process(Tick.Zero, new SetRoadVehicleOrdersCommand(vehicleId, orders, _catalog)).IsAccepted;
        }

        private static VehiclePurchaseUiState BuildPurchaseUiState(
            GeneratedWorld world,
            ContentCatalog catalog,
            MapTileInteractionController interaction,
            SimulationCalendar calendar,
            SimulationLoop simulationLoop)
        {
            VehiclePurchaseSelection selection = VehiclePurchaseSelection.None;
            if (interaction.SelectedTile.HasValue)
            {
                TileCoordinate tile = interaction.SelectedTile.Value;
                if (world.TryGetRoadStationAt(tile, out _))
                {
                    selection = VehiclePurchaseSelection.Road;
                }
                else if (world.TryGetRailStationAt(tile, out _))
                {
                    selection = VehiclePurchaseSelection.Rail;
                }
            }

            int year = calendar.GetDate(simulationLoop.CurrentTick).Year;
            long cash = world.CompanyCash;
            IReadOnlyList<VehiclePurchaseOption> options = selection == VehiclePurchaseSelection.None
                ? Array.Empty<VehiclePurchaseOption>()
                : BuildPurchaseOptions(world, catalog, selection, year, cash);

            return new VehiclePurchaseUiState(
                cash,
                selection,
                options,
                catalog.RailWagon.Purchase);
        }

        private static IReadOnlyList<VehiclePurchaseOption> BuildPurchaseOptions(
            GeneratedWorld world,
            ContentCatalog catalog,
            VehiclePurchaseSelection selection,
            int calendarYear,
            long cash)
        {
            VehicleMode mode = selection == VehiclePurchaseSelection.Rail
                ? VehicleMode.Rail
                : VehicleMode.Road;

            var matches = new List<VehicleDefinition>();
            IReadOnlyList<VehicleDefinition> vehicles = catalog.Vehicles;
            for (int index = 0; index < vehicles.Count; index++)
            {
                VehicleDefinition definition = vehicles[index];
                if (definition.Mode != mode)
                {
                    continue;
                }

                if (!VehicleAvailability.CanPurchase(definition, world, calendarYear))
                {
                    continue;
                }

                matches.Add(definition);
            }

            matches.Sort(CompareVehicleByYearThenId);
            var options = new VehiclePurchaseOption[matches.Count];
            for (int index = 0; index < matches.Count; index++)
            {
                VehicleDefinition definition = matches[index];
                options[index] = new VehiclePurchaseOption(
                    definition.Id,
                    definition.NameKey,
                    definition.Purchase,
                    cash >= definition.Purchase);
            }

            return options;
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

        private static IReadOnlyList<VehicleListItem> BuildVehicleListItems(
            GeneratedWorld world,
            ContentCatalog catalog)
        {
            IReadOnlyList<RoadVehicle> roadVehicles = world.RoadVehicles;
            IReadOnlyList<RailVehicle> railVehicles = world.RailVehicles;
            if (roadVehicles.Count == 0 && railVehicles.Count == 0)
            {
                return Array.Empty<VehicleListItem>();
            }

            var items = new VehicleListItem[roadVehicles.Count + railVehicles.Count];
            int writeIndex = 0;
            for (int index = 0; index < roadVehicles.Count; index++)
            {
                RoadVehicle vehicle = roadVehicles[index];
                long roadUpkeep = catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? roadType) &&
                    roadType != null
                    ? roadType.UpkeepPerMonth
                    : 0L;
                items[writeIndex++] = new VehicleListItem(
                    VehicleListKind.Road,
                    vehicle.Id,
                    VehicleListStatus.Resolve(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop),
                    vehicle.Cargo.TotalAmount,
                    vehicle.CargoCapacity,
                    vehicle.LifetimeDeliveryIncome,
                    monthlyUpkeep: roadUpkeep,
                    cargoStacks: BuildCargoStacks(vehicle.Cargo, catalog));
            }

            for (int index = 0; index < railVehicles.Count; index++)
            {
                RailVehicle vehicle = railVehicles[index];
                items[writeIndex++] = new VehicleListItem(
                    VehicleListKind.Rail,
                    vehicle.Id,
                    VehicleListStatus.Resolve(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop),
                    vehicle.Cargo.TotalAmount,
                    vehicle.CargoCapacity,
                    vehicle.LifetimeDeliveryIncome,
                    vehicle.WagonCount,
                    vehicle.MaxWagons,
                    catalog.MonthlyUpkeepForRail(vehicle.TypeId, vehicle.WagonCount),
                    BuildCargoStacks(vehicle.Cargo, catalog));
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
                    town.MonthMailTransported);
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

            if (_gameOver || _world.IsBankrupt)
            {
                EnterGameOver();
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                // F9: unused by tools/panels (Y is demolish rail station). Dev-only year skip.
                if (keyboard.f9Key.wasPressedThisFrame)
                {
                    AdvanceOneGameYearForDevelopment();
                }
#endif
            }

            _simulationLoop.Advance(TimeSpan.FromSeconds(Time.deltaTime), AdvanceOneSimulationTick);
            _vehicleView.Sync();
            _industryCargoLabels.Sync();
        }

        /// <summary>
        /// Shared per-tick simulation step used by the frame loop and Development year-skip.
        /// </summary>
        private void AdvanceOneSimulationTick(Tick tick)
        {
            SimulationCalendar calendar = _calendar!;
            GameDate date = calendar.GetDate(tick);
            // Roll the finance year first so the first tick of a year books its money into the new year.
            _world!.Finance.AdvanceTo(date.Year);
            // Shared load pass: road+rail unload/travel, industry+town Distribute, then finish.
            RoadVehicleMovement.AdvanceUnloadOrTravelAll(_world!, _catalog!, date);
            RailVehicleMovement.AdvanceUnloadOrTravelAll(_world!, _catalog!, date);
            IndustryCargoLoading.Distribute(_world!, _catalog!, date);
            TownCargoLoading.Distribute(_world!, _catalog!, date);
            RoadVehicleMovement.FinishServicesAll(_world!, _catalog!);
            RailVehicleMovement.FinishServicesAll(_world!, _catalog!);
            IndustryProduction.AdvanceAll(_world!, _catalog!, tick, date);
            TownPassengerGeneration.AdvanceAll(_world!, _catalog!, tick);
            TownGrowth.AdvanceAll(_world!, _catalog!, date);
            VehicleUpkeep.AdvanceAll(_world!, _catalog!, date);
            Contracts.AdvanceAll(_world!, _catalog!, date, _events);
            Achievements.AdvanceAll(_world!, _catalog!, date, _events);
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

        private void OnDestroy()
        {
            MapPlayShell? shell = GetComponent<MapPlayShell>();
            if (shell != null && _tileInteraction != null)
            {
                _tileInteraction.ActiveBuildToolChanged -= shell.SetActiveBuildTool;
            }

            for (int index = 0; index < _eventSubscriptions.Count; index++)
            {
                _eventSubscriptions[index].Dispose();
            }

            _eventSubscriptions.Clear();
            _mapView?.Dispose();
            _mapView = null;
            _vehicleView?.Dispose();
            _vehicleView = null;
            _industryCargoLabels?.Dispose();
            _industryCargoLabels = null;
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

            _mapView.MarkTileDirty(coordinate.X, coordinate.Y);
            return _mapView.RebuildDirtyChunks();
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
                    _contractsData);
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
                        contractsAsset);
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
