#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Orthographic map camera: WASD/arrows and right-drag pan, scroll zoom, soft map clamp.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraController : MonoBehaviour
    {
        [SerializeField]
        private float _minOrthographicSize = 2f;

        [SerializeField]
        private float _maxOrthographicSize = 140f;

        [SerializeField]
        private float _zoomFactor = 0.85f;

        [SerializeField]
        private float _keyboardPanSpeed = 1.5f;

        private Camera _camera = null!;
        private float _mapMinX;
        private float _mapMaxX;
        private float _mapMinY;
        private float _mapMaxY;
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
        /// Sets map bounds from tile side length and applies an initial view.
        /// </summary>
        public void Configure(int sideLengthTiles, float initialOrthographicSize)
        {
            MapCameraMath.GetMapWorldBounds(
                sideLengthTiles,
                out _mapMinX,
                out _mapMaxX,
                out _mapMinY,
                out _mapMaxY);

            Vector3 mapCenter = IsometricProjection.MapCenter(sideLengthTiles);
            float size = MapCameraMath.ClampZoom(initialOrthographicSize, _minOrthographicSize, _maxOrthographicSize);
            ApplyView(new Vector2(mapCenter.x, mapCenter.y), size);
            _configured = true;
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

            ApplyView(center, size);
        }

        private void ApplyView(Vector2 center, float orthographicSize)
        {
            float size = MapCameraMath.ClampZoom(orthographicSize, _minOrthographicSize, _maxOrthographicSize);
            float halfHeight = size;
            // Prefer Screen aspect so clamp matches the pixels the player sees (WebGL-safe).
            float aspect = Screen.height > 0
                ? (float)Screen.width / Screen.height
                : _camera.aspect;
            if (aspect <= 0f)
            {
                aspect = 1f;
            }

            float halfWidth = size * aspect;

            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                center,
                _mapMinX,
                _mapMaxX,
                _mapMinY,
                _mapMaxY,
                halfWidth,
                halfHeight);

            _camera.orthographicSize = size;
            transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
        }
    }
}
