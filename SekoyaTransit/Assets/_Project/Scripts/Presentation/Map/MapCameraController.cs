#nullable enable
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Orthographic map camera: WASD/arrows and right-drag pan, scroll zoom, diamond soft clamp.
    /// Scroll zoom and right-drag pan are suppressed while the pointer is over interactive shell UI.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraController : MonoBehaviour
    {
        /// <summary>
        /// Closest zoom: a tile is at most about 135 px wide on a 1080p screen, so the 100 px Kenney road tiles in the
        /// 128 px atlas stay sharp (ADR 0032).
        /// </summary>
        public const float ClosestZoomOrthographicSize = 4f;

        [SerializeField]
        private float _minOrthographicSize = ClosestZoomOrthographicSize;

        [SerializeField]
        private float _maxOrthographicSize = 140f;

        [SerializeField]
        private float _zoomFactor = 0.85f;

        [SerializeField]
        private float _keyboardPanSpeed = 1.5f;

        private Camera _camera = null!;
        private float _diamondCenterX;
        private float _diamondCenterY;
        private float _diamondHalfWidth;
        private float _diamondHalfHeight;
        private Func<Vector2, bool>? _isPointerOverUi;
        private bool _configured;

        /// <summary>
        /// Gets whether <see cref="Configure"/> has been called with map bounds.
        /// </summary>
        public bool IsConfigured => _configured;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        /// <summary>
        /// Sets map diamond bounds from tile side length and applies an initial view.
        /// </summary>
        /// <param name="sideLengthTiles">Map side length in tiles.</param>
        /// <param name="initialOrthographicSize">Initial orthographic size.</param>
        /// <param name="isPointerOverUi">
        /// When true for the current pointer, scroll zoom and right-drag pan are ignored
        /// so UI Toolkit scroll views can consume the wheel.
        /// </param>
        public void Configure(
            int sideLengthTiles,
            float initialOrthographicSize,
            Func<Vector2, bool>? isPointerOverUi = null)
        {
            MapCameraMath.GetMapDiamond(
                sideLengthTiles,
                out _diamondCenterX,
                out _diamondCenterY,
                out _diamondHalfWidth,
                out _diamondHalfHeight);

            _isPointerOverUi = isPointerOverUi;
            float size = MapCameraMath.ClampZoom(initialOrthographicSize, _minOrthographicSize, _maxOrthographicSize);
            ApplyView(new Vector2(_diamondCenterX, _diamondCenterY), size);
            _configured = true;
        }

        /// <summary>
        /// Centers the camera on a map tile without changing the current zoom.
        /// </summary>
        public void CenterOnTile(int tileX, int tileY)
        {
            if (!_configured)
            {
                return;
            }

            Vector3 world = IsometricProjection.TileToWorld(tileX, tileY);
            ApplyView(new Vector2(world.x, world.y), _camera.orthographicSize);
        }

        private void OnDestroy()
        {
            _isPointerOverUi = null;
            _configured = false;
        }

        private void Update()
        {
            if (!_configured)
            {
                return;
            }

            float size = _camera.orthographicSize;
            Vector2 center = new Vector2(transform.position.x, transform.position.y);

            Keyboard? keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var move = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    move.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    move.y -= 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    move.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    move.x += 1f;
                }

                center += MapCameraMath.KeyboardPanDelta(move, size, _keyboardPanSpeed, Time.unscaledDeltaTime);
            }

            Mouse? mouse = Mouse.current;
            if (mouse != null)
            {
                bool pointerOverUi = _isPointerOverUi != null
                    && _isPointerOverUi(mouse.position.ReadValue());

                if (!pointerOverUi)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (scroll != 0f)
                    {
                        size = MapCameraMath.ApplyScrollZoom(
                            size,
                            scroll,
                            _zoomFactor,
                            _minOrthographicSize,
                            _maxOrthographicSize);
                    }

                    if (mouse.rightButton.isPressed)
                    {
                        Vector2 delta = mouse.delta.ReadValue();
                        center += MapCameraMath.MouseDragPanDelta(delta, size, Screen.height);
                    }
                }
            }

            ApplyView(center, size);
        }

        private void ApplyView(Vector2 center, float orthographicSize)
        {
            float size = MapCameraMath.ClampZoom(orthographicSize, _minOrthographicSize, _maxOrthographicSize);
            float halfHeight = size;
            float aspect = _camera.pixelHeight > 0
                ? (float)_camera.pixelWidth / _camera.pixelHeight
                : (Screen.height > 0 ? (float)Screen.width / Screen.height : _camera.aspect);
            if (aspect <= 0f)
            {
                aspect = 1f;
            }

            float halfWidth = size * aspect;

            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                center,
                _diamondCenterX,
                _diamondCenterY,
                _diamondHalfWidth,
                _diamondHalfHeight,
                halfWidth,
                halfHeight);

            _camera.orthographicSize = size;
            transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
        }
    }
}
