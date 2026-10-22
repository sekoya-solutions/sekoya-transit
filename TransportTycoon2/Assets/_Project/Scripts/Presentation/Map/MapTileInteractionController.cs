#nullable enable
using System;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// MapPlay tile hover, sticky selection, and B/X/N/M road and station placement modes.
    /// </summary>
    public sealed class MapTileInteractionController : MonoBehaviour
    {
        private GeneratedWorld? _world;
        private Camera? _camera;
        private MapTileOverlayView? _overlays;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;
        private Action<TileCoordinate>? _onTileChanged;
        private int _sideLengthTiles;
        private bool _configured;
        private bool _buildModeEnabled;
        private bool _demolishModeEnabled;
        private bool _stationBuildModeEnabled;
        private bool _stationDemolishModeEnabled;
        private TileCoordinate? _hoverTile;
        private TileCoordinate? _selectedTile;

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
        /// Binds the controller to a Core world, camera, command processor, and dirty-tile callback.
        /// </summary>
        public void Configure(
            GeneratedWorld world,
            Camera camera,
            Transform? overlayParent,
            SimulationCommandProcessor<GeneratedWorld> commandProcessor,
            Action<TileCoordinate> onTileChanged)
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
            _sideLengthTiles = world.Map.Width;
            _overlays = new MapTileOverlayView(overlayParent);
            _hoverTile = null;
            _selectedTile = null;
            _buildModeEnabled = false;
            _demolishModeEnabled = false;
            _stationBuildModeEnabled = false;
            _stationDemolishModeEnabled = false;
            _configured = true;
            RefreshOverlays();
        }

        private void OnDestroy()
        {
            _overlays?.Dispose();
            _overlays = null;
            _configured = false;
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
                if (keyboard.bKey.wasPressedThisFrame)
                {
                    ToggleRoadBuildMode();
                }

                if (keyboard.xKey.wasPressedThisFrame)
                {
                    ToggleRoadDemolishMode();
                }

                if (keyboard.nKey.wasPressedThisFrame)
                {
                    ToggleStationBuildMode();
                }

                if (keyboard.mKey.wasPressedThisFrame)
                {
                    ToggleStationDemolishMode();
                }
            }

            TileCoordinate? pointed = TryPickTileUnderPointer();
            _hoverTile = pointed;

            Mouse? mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                HandleLeftClick(pointed);
            }

            RefreshOverlays();
        }

        private void ToggleRoadBuildMode()
        {
            _buildModeEnabled = !_buildModeEnabled;
            if (_buildModeEnabled)
            {
                _demolishModeEnabled = false;
                _stationBuildModeEnabled = false;
                _stationDemolishModeEnabled = false;
            }
        }

        private void ToggleRoadDemolishMode()
        {
            _demolishModeEnabled = !_demolishModeEnabled;
            if (_demolishModeEnabled)
            {
                _buildModeEnabled = false;
                _stationBuildModeEnabled = false;
                _stationDemolishModeEnabled = false;
            }
        }

        private void ToggleStationBuildMode()
        {
            _stationBuildModeEnabled = !_stationBuildModeEnabled;
            if (_stationBuildModeEnabled)
            {
                _buildModeEnabled = false;
                _demolishModeEnabled = false;
                _stationDemolishModeEnabled = false;
            }
        }

        private void ToggleStationDemolishMode()
        {
            _stationDemolishModeEnabled = !_stationDemolishModeEnabled;
            if (_stationDemolishModeEnabled)
            {
                _buildModeEnabled = false;
                _demolishModeEnabled = false;
                _stationBuildModeEnabled = false;
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
