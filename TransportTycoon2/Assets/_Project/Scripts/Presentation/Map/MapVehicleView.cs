#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Renders road vehicles as colored diamond placeholders that follow Core positions.
    /// </summary>
    public sealed class MapVehicleView : IDisposable
    {
        private const float VehicleWorldZ = -1.2f;

        private readonly GeneratedWorld _world;
        private readonly GameObject _root;
        private readonly Texture2D _diamondTexture;
        private readonly Sprite _diamondSprite;
        private readonly Dictionary<int, SpriteRenderer> _markers = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, TextMesh> _cargoLabels = new Dictionary<int, TextMesh>();
        private bool _disposed;

        /// <summary>
        /// Initializes a vehicle view bound to a Core world.
        /// </summary>
        public MapVehicleView(GeneratedWorld world, Transform? parent)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _diamondTexture = CreateDiamondTexture();
            _diamondSprite = Sprite.Create(
                _diamondTexture,
                new Rect(0f, 0f, _diamondTexture.width, _diamondTexture.height),
                new Vector2(0.5f, 0.5f),
                _diamondTexture.width);

            _root = new GameObject("MapVehicles");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
        }

        /// <summary>
        /// Syncs marker GameObjects to the current Core vehicle list and positions.
        /// </summary>
        public void Sync()
        {
            ThrowIfDisposed();

            var seen = new HashSet<int>();
            for (int index = 0; index < _world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = _world.RoadVehicles[index];
                seen.Add(vehicle.Id);
                if (!_markers.TryGetValue(vehicle.Id, out SpriteRenderer? renderer) || renderer == null)
                {
                    renderer = CreateMarker(vehicle.Id);
                    _markers[vehicle.Id] = renderer;
                }

                Vector3 position = ResolveWorldPosition(vehicle);
                renderer.transform.position = position;
                renderer.color = vehicle.Cargo.IsEmpty
                    ? MapPlaceholderPalette.RoadVehicle
                    : MapPlaceholderPalette.RoadVehicleLoaded;
                renderer.enabled = true;

                if (!_cargoLabels.TryGetValue(vehicle.Id, out TextMesh? cargoLabel) || cargoLabel == null)
                {
                    cargoLabel = CreateCargoLabel(renderer.transform);
                    _cargoLabels[vehicle.Id] = cargoLabel;
                }

                cargoLabel.text = vehicle.Cargo.TotalAmount.ToString();
            }

            var removeIds = new List<int>();
            foreach (KeyValuePair<int, SpriteRenderer> pair in _markers)
            {
                if (!seen.Contains(pair.Key))
                {
                    removeIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                int id = removeIds[index];
                if (_markers.TryGetValue(id, out SpriteRenderer? renderer) && renderer != null)
                {
                    DestroyUnityObject(renderer.gameObject);
                }

                _markers.Remove(id);
                _cargoLabels.Remove(id);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroyUnityObject(_root);
            DestroyUnityObject(_diamondSprite);
            DestroyUnityObject(_diamondTexture);
            _markers.Clear();
            _cargoLabels.Clear();
        }

        private SpriteRenderer CreateMarker(int vehicleId)
        {
            var go = new GameObject("RoadVehicle_" + vehicleId);
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = new Vector3(0.65f, 0.65f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _diamondSprite;
            renderer.sortingOrder = 20;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static TextMesh CreateCargoLabel(Transform parent)
        {
            var go = new GameObject("CargoAmount");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.55f, -0.1f);
            go.transform.localScale = new Vector3(1f / 0.65f, 1f / 0.65f, 1f);
            var textMesh = go.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.07f;
            textMesh.fontSize = 32;
            textMesh.color = Color.white;
            return textMesh;
        }

        private Vector3 ResolveWorldPosition(RoadVehicle vehicle)
        {
            if (!RoadVehicleMovement.TryGetPresentationSegment(
                    _world,
                    vehicle,
                    out TileCoordinate fromTile,
                    out TileCoordinate toTile,
                    out FixedPoint fraction))
            {
                return Vector3.zero;
            }

            float t = (float)((double)fraction.RawValue / FixedPoint.OneRawValue);
            float tileX = fromTile.X + ((toTile.X - fromTile.X) * t);
            float tileY = fromTile.Y + ((toTile.Y - fromTile.Y) * t);
            Vector3 world = IsometricProjection.TileToWorld(tileX, tileY);
            return new Vector3(world.x, world.y, VehicleWorldZ);
        }

        private static Texture2D CreateDiamondTexture()
        {
            const int width = 64;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "VehicleDiamond",
            };
            var pixels = new Color32[width * height];
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float dx = Mathf.Abs(px + 0.5f - (width * 0.5f)) / (width * 0.5f);
                    float dy = Mathf.Abs(py + 0.5f - (height * 0.5f)) / (height * 0.5f);
                    bool inside = dx + dy <= 1f;
                    pixels[(py * width) + px] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DestroyUnityObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(unityObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MapVehicleView));
            }
        }
    }
}
