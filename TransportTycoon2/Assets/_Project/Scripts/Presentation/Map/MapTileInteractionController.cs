#nullable enable
using System;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;
using TransportTycoon.UI.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// MapPlay tile hover, sticky selection, B/X/N/M road and station placement, and V vehicle spawn.
    /// </summary>
    public sealed class MapTileInteractionController : MonoBehaviour
    {
        private GeneratedWorld? _world;
        private Camera? _camera;
        private MapTileOverlayView? _overlays;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;
        private Action<TileCoordinate>? _onTileChanged;
        private Func<Vector2, bool>? _isPointerOverUi;
        private Func<bool>? _tryConsumeEscape;
        private int _sideLengthTiles;
        private bool _configured;
        private bool _buildModeEnabled;
        private bool _demolishModeEnabled;
        private bool _stationBuildModeEnabled;
        private bool _stationDemolishModeEnabled;
        private TileCoordinate? _hoverTile;
        private TileCoordinate? _selectedTile;

        /// <summary>
        /// Raised when the active build tool changes (toolbar, keyboard, or Escape).
        /// </summary>
        public event Action<BuildToolId>? ActiveBuildToolChanged;

        /// <summary>
        /// Gets whether road-build preview mode is on.
        /// </summary>
        public bool IsBuildModeEnabled => _buildModeEnabled;

        /// <summary>
        /// Gets whether road-demolish preview mode is on.
        /// </summary>
        public bool IsDemolishModeEnabled => _demolishModeEnabled;

        /// <summary>
        /// Gets whether station-build preview mode is on.
        /// </summary>
        public bool IsStationBuildModeEnabled => _stationBuildModeEnabled;

        /// <summary>
        /// Gets whether station-demolish preview mode is on.
        /// </summary>
        public bool IsStationDemolishModeEnabled => _stationDemolishModeEnabled;

        /// <summary>
        /// Gets whether any placement-preview mode is on.
        /// </summary>
        public bool IsPlacementPreviewEnabled =>
            _buildModeEnabled
            || _demolishModeEnabled
            || _stationBuildModeEnabled
            || _stationDemolishModeEnabled;

        /// <summary>
        /// Gets the sticky-selected tile, if any.
        /// </summary>
        public TileCoordinate? SelectedTile => _selectedTile;

        /// <summary>
        /// Gets the active build tool derived from placement mode flags.
        /// </summary>
        public BuildToolId ActiveBuildTool => BuildToolSelection.FromModes(
            _buildModeEnabled,
            _demolishModeEnabled,
            _stationBuildModeEnabled,
            _stationDemolishModeEnabled);

        /// <summary>
        /// Binds the controller to a Core world, camera, command processor, and dirty-tile callback.
        /// </summary>
        public void Configure(
            GeneratedWorld world,
            Camera camera,
            Transform? overlayParent,
            SimulationCommandProcessor<GeneratedWorld> commandProcessor,
            Action<TileCoordinate> onTileChanged,
            Func<Vector2, bool>? isPointerOverUi = null,
            Func<bool>? tryConsumeEscape = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (camera is null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (commandProcessor is null)
            {
                throw new ArgumentNullException(nameof(commandProcessor));
            }

            if (onTileChanged is null)
            {
                throw new ArgumentNullException(nameof(onTileChanged));
            }
#pragma warning restore CA1510

            _overlays?.Dispose();
            _world = world;
            _camera = camera;
            _commandProcessor = commandProcessor;
            _onTileChanged = onTileChanged;
            _isPointerOverUi = isPointerOverUi;
            _tryConsumeEscape = tryConsumeEscape;
            _sideLengthTiles = world.Map.Width;
            _overlays = new MapTileOverlayView(overlayParent);
            _hoverTile = null;
            _selectedTile = null;
            ApplyTool(BuildToolId.Select, notify: false);
            _configured = true;
            RefreshOverlays();
        }

        /// <summary>
        /// Sets the active build tool (toolbar absolute selection).
        /// </summary>
        public void SetBuildTool(BuildToolId tool)
        {
            ApplyTool(tool, notify: true);
        }

        private void OnDestroy()
        {
            _overlays?.Dispose();
            _overlays = null;
            _isPointerOverUi = null;
            _tryConsumeEscape = null;
            _configured = false;
            ActiveBuildToolChanged = null;
        }

        private void Update()
        {
            if (!_configured || _world == null || _camera == null || _overlays == null)
            {
                return;
            }

            Keyboard? keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (_tryConsumeEscape == null || !_tryConsumeEscape())
                    {
                        ApplyTool(BuildToolId.Select, notify: true);
                    }
                }

                if (keyboard.bKey.wasPressedThisFrame)
                {
                    ToggleTool(BuildToolId.Road);
                }

                if (keyboard.xKey.wasPressedThisFrame)
                {
                    ToggleTool(BuildToolId.DemolishRoad);
                }

                if (keyboard.nKey.wasPressedThisFrame)
                {
                    ToggleTool(BuildToolId.Station);
                }

                if (keyboard.mKey.wasPressedThisFrame)
                {
                    ToggleTool(BuildToolId.DemolishStation);
                }

                if (keyboard.vKey.wasPressedThisFrame)
                {
                    TrySpawnVehicleAtSelection();
                }
            }

            Mouse? mouse = Mouse.current;
            bool pointerOverUi = false;
            if (mouse != null && _isPointerOverUi != null)
            {
                pointerOverUi = _isPointerOverUi(mouse.position.ReadValue());
            }

            TileCoordinate? pointed = pointerOverUi ? null : TryPickTileUnderPointer();
            _hoverTile = pointed;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !pointerOverUi)
            {
                HandleLeftClick(pointed);
            }

            RefreshOverlays();
        }

        private void ToggleTool(BuildToolId tool)
        {
            ApplyTool(BuildToolSelection.Toggle(ActiveBuildTool, tool), notify: true);
        }

        private void ApplyTool(BuildToolId tool, bool notify)
        {
            BuildToolId previous = ActiveBuildTool;
            BuildToolSelection.ToModes(
                tool,
                out _buildModeEnabled,
                out _demolishModeEnabled,
                out _stationBuildModeEnabled,
                out _stationDemolishModeEnabled);

            if (notify && previous != tool)
            {
                ActiveBuildToolChanged?.Invoke(tool);
            }
        }

        private void HandleLeftClick(TileCoordinate? pointed)
        {
            if (_buildModeEnabled)
            {
                TryBuildRoad(pointed);
                return;
            }

            if (_demolishModeEnabled)
            {
                TryDemolishRoad(pointed);
                return;
            }

            if (_stationBuildModeEnabled)
            {
                TryBuildStation(pointed);
                return;
            }

            if (_stationDemolishModeEnabled)
            {
                TryDemolishStation(pointed);
                return;
            }

            if (!pointed.HasValue)
            {
                _selectedTile = null;
                return;
            }

            if (_selectedTile.HasValue && _selectedTile.Value.Equals(pointed.Value))
            {
                _selectedTile = null;
                return;
            }

            _selectedTile = pointed;
        }

        private void TryBuildRoad(TileCoordinate? pointed)
        {
            if (!pointed.HasValue || _commandProcessor == null || _onTileChanged == null)
            {
                return;
            }

            CommandProcessingResult result = _commandProcessor.Process(
                Tick.Zero,
                new BuildRoadCommand(pointed.Value));
            if (result.IsAccepted)
            {
                _onTileChanged(pointed.Value);
            }
        }

        private void TryDemolishRoad(TileCoordinate? pointed)
        {
            if (!pointed.HasValue || _commandProcessor == null || _onTileChanged == null)
            {
                return;
            }

            CommandProcessingResult result = _commandProcessor.Process(
                Tick.Zero,
                new DemolishRoadCommand(pointed.Value));
            if (result.IsAccepted)
            {
                _onTileChanged(pointed.Value);
            }
        }

        private void TryBuildStation(TileCoordinate? pointed)
        {
            if (!pointed.HasValue || _commandProcessor == null || _onTileChanged == null)
            {
                return;
            }

            CommandProcessingResult result = _commandProcessor.Process(
                Tick.Zero,
                new BuildRoadStationCommand(pointed.Value));
            if (result.IsAccepted)
            {
                _onTileChanged(pointed.Value);
            }
        }

        private void TryDemolishStation(TileCoordinate? pointed)
        {
            if (!pointed.HasValue || _commandProcessor == null || _onTileChanged == null)
            {
                return;
            }

            CommandProcessingResult result = _commandProcessor.Process(
                Tick.Zero,
                new DemolishRoadStationCommand(pointed.Value));
            if (result.IsAccepted)
            {
                _onTileChanged(pointed.Value);
            }
        }

        /// <summary>
        /// Buys a road vehicle at the sticky-selected station (keyboard V or vehicles panel).
        /// </summary>
        public void TryBuyVehicleAtSelection()
        {
            TrySpawnVehicleAtSelection();
        }

        /// <summary>
        /// Sells an owned road vehicle by id (vehicles panel).
        /// </summary>
        public void TrySellVehicle(int vehicleId)
        {
            if (_commandProcessor == null)
            {
                return;
            }

            _commandProcessor.Process(Tick.Zero, new SellRoadVehicleCommand(vehicleId));
        }

        private void TrySpawnVehicleAtSelection()
        {
            if (!_selectedTile.HasValue || _world == null || _commandProcessor == null)
            {
                return;
            }

            if (!_world.TryGetRoadStationAt(_selectedTile.Value, out RoadStation? station) || station == null)
            {
                return;
            }

            CommandProcessingResult buy = _commandProcessor.Process(
                Tick.Zero,
                new BuyRoadVehicleCommand(station.Id));
            if (!buy.IsAccepted)
            {
                return;
            }

            RoadVehicle? spawned = null;
            for (int index = 0; index < _world.RoadVehicles.Count; index++)
            {
                RoadVehicle candidate = _world.RoadVehicles[index];
                if (spawned == null || candidate.Id > spawned.Id)
                {
                    spawned = candidate;
                }
            }

            if (spawned == null)
            {
                return;
            }

            int[] orders = RoadVehicleMovement.BuildAutoOrders(_world, station.Id);
            if (orders.Length >= 2)
            {
                _commandProcessor.Process(Tick.Zero, new SetRoadVehicleOrdersCommand(spawned.Id, orders));
            }
        }

        private TileCoordinate? TryPickTileUnderPointer()
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null || _camera == null)
            {
                return null;
            }

            Vector2 screen = mouse.position.ReadValue();
            float planeDistance = Mathf.Abs(_camera.transform.position.z);
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, planeDistance));
            if (!IsometricProjection.TryWorldToTile(world.x, world.y, _sideLengthTiles, out int tileX, out int tileY))
            {
                return null;
            }

            return new TileCoordinate(tileX, tileY);
        }

        private void RefreshOverlays()
        {
            if (_overlays == null || _world == null)
            {
                return;
            }

            _overlays.SetSelection(_selectedTile);

            if (IsPlacementPreviewEnabled)
            {
                // Ghost replaces hover highlight while preview mode is on.
                _overlays.SetHover(null);
                if (_hoverTile.HasValue)
                {
                    TileCoordinate tile = _hoverTile.Value;
                    bool valid = ResolveGhostValidity(tile);
                    _overlays.SetGhost(tile, valid);
                }
                else
                {
                    _overlays.SetGhost(null, false);
                }
            }
            else
            {
                _overlays.SetGhost(null, false);
                _overlays.SetHover(_hoverTile);
            }
        }

        private bool ResolveGhostValidity(TileCoordinate tile)
        {
            if (_world == null)
            {
                return false;
            }

            if (_buildModeEnabled)
            {
                return TilePlacementPreviewRules.IsBuildGhostValid(_world.Map, tile);
            }

            if (_demolishModeEnabled)
            {
                return TilePlacementPreviewRules.IsDemolishGhostValid(_world.Map, tile);
            }

            if (_stationBuildModeEnabled)
            {
                return TilePlacementPreviewRules.IsStationBuildGhostValid(_world.Map, tile);
            }

            if (_stationDemolishModeEnabled)
            {
                return TilePlacementPreviewRules.IsStationDemolishGhostValid(_world.Map, tile);
            }

            return false;
        }
    }
}
