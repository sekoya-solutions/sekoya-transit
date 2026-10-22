#nullable enable
using TransportTycoon.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// MapPlay tile hover, sticky left-click selection, and B-toggled 1x1 placement ghost preview.
    /// </summary>
    public sealed class MapTileInteractionController : MonoBehaviour
    {
        private GeneratedWorld? _world;
        private Camera? _camera;
        private MapTileOverlayView? _overlays;
        private int _sideLengthTiles;
        private bool _configured;
        private bool _placementPreviewEnabled;
        private TileCoordinate? _hoverTile;
        private TileCoordinate? _selectedTile;

        /// <summary>
        /// Gets whether placement-preview (ghost) mode is on.
        /// </summary>
        public bool IsPlacementPreviewEnabled => _placementPreviewEnabled;

        /// <summary>
        /// Gets the sticky-selected tile, if any.
        /// </summary>
        public TileCoordinate? SelectedTile => _selectedTile;

        /// <summary>
        /// Binds the controller to a Core world and camera, creating overlay sprites.
        /// </summary>
        public void Configure(GeneratedWorld world, Camera camera, Transform? overlayParent)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new System.ArgumentNullException(nameof(world));
            }

            if (camera is null)
            {
                throw new System.ArgumentNullException(nameof(camera));
            }
#pragma warning restore CA1510

            _overlays?.Dispose();
            _world = world;
            _camera = camera;
            _sideLengthTiles = world.Map.Width;
            _overlays = new MapTileOverlayView(overlayParent);
            _hoverTile = null;
            _selectedTile = null;
            _placementPreviewEnabled = false;
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
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
            {
                _placementPreviewEnabled = !_placementPreviewEnabled;
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

        private void HandleLeftClick(TileCoordinate? pointed)
        {
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

            if (_placementPreviewEnabled)
            {
                // Ghost replaces hover highlight while preview mode is on.
                _overlays.SetHover(null);
                if (_hoverTile.HasValue)
                {
                    TileCoordinate tile = _hoverTile.Value;
                    int objectId = _world.Map.GetObjectId(tile);
                    bool valid = TilePlacementPreviewRules.IsPlaceholderPlacementValid(objectId);
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
    }
}
