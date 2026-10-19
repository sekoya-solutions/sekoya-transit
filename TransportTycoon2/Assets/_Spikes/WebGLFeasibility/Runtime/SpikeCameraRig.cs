#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace TransportTycoon.Spikes.WebGL
{
    // Minimal manual camera (pan and zoom) so the spike can be driven by hand; the benchmark takes it over.
    public sealed class SpikeCameraRig : MonoBehaviour
    {
        private Camera _camera = null!;

        public bool ManualControl { get; set; } = true;

        public void SetView(Vector2 center, float orthographicSize)
        {
            transform.position = new Vector3(center.x, center.y, -10f);
            _camera.orthographicSize = orthographicSize;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            if (!ManualControl)
            {
                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Mouse? mouse = Mouse.current;
            float size = _camera.orthographicSize;

            var move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) { move.y += 1f; }
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) { move.y -= 1f; }
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) { move.x -= 1f; }
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) { move.x += 1f; }
            }

            Vector3 position = transform.position + (Vector3)(move * (size * 1.5f * Time.unscaledDeltaTime));

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    size = Mathf.Clamp(scroll > 0f ? size * 0.85f : size / 0.85f, 2f, 140f);
                }

                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    position -= (Vector3)(delta * (2f * size / Screen.height));
                }
            }

            _camera.orthographicSize = size;
            transform.position = position;
        }
    }
}
