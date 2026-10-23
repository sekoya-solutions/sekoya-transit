#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using TransportTycoon.UI.Shell;
using UnityEngine;
using UnityEngine.Rendering;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Renders road and rail vehicles as colored diamond placeholders that follow Core positions.
    /// Parked idle vehicles are hidden (V1; V2 will show them in depots). Vehicles list UI stays road-only.
    /// </summary>
    public sealed class MapVehicleView : IDisposable
    {
        private const float VehicleWorldZ = -1.2f;

        private readonly GeneratedWorld _world;
        private readonly GameObject _root;
        private readonly Texture2D _diamondTexture;
        private readonly Sprite _diamondSprite;
        private readonly Dictionary<int, SpriteRenderer> _roadMarkers = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, TextMesh> _roadCargoLabels = new Dictionary<int, TextMesh>();
        private readonly Dictionary<int, SpriteRenderer> _railMarkers = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, TextMesh> _railCargoLabels = new Dictionary<int, TextMesh>();
        private Func<int?>? _selectedVehicleIdProvider;
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
        /// Optional provider for the road vehicle id currently selected in the Vehicles route editor.
        /// Selected moving road vehicles use a distinct highlight color on the map.
        /// </summary>
        public void SetSelectedVehicleIdProvider(Func<int?>? selectedVehicleIdProvider)
        {
            _selectedVehicleIdProvider = selectedVehicleIdProvider;
        }

        /// <summary>
        /// Syncs marker GameObjects to the current Core road and rail vehicle lists and positions.
        /// </summary>
        public void Sync()
        {
            ThrowIfDisposed();
            SyncRoadVehicles();
            SyncRailVehicles();
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
            _roadMarkers.Clear();
            _roadCargoLabels.Clear();
            _railMarkers.Clear();
            _railCargoLabels.Clear();
        }

        private void SyncRoadVehicles()
        {
            int? selectedId = _selectedVehicleIdProvider?.Invoke();
            var seen = new HashSet<int>();
            for (int index = 0; index < _world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = _world.RoadVehicles[index];
                seen.Add(vehicle.Id);
                if (!_roadMarkers.TryGetValue(vehicle.Id, out SpriteRenderer? renderer) || renderer == null)
                {
                    renderer = CreateMarker("RoadVehicle_" + vehicle.Id, sortingOrder: 20);
                    _roadMarkers[vehicle.Id] = renderer;
                }

                // Hide only truly idle parked vehicles (no route). Mid-route vehicles sit at
                // nodes with Parked status between edges for a tick — hiding those caused flicker.
                VehicleListStatusKind status = VehicleListStatus.Resolve(
                    vehicle.IsStuck,
                    vehicle.IsOnEdge,
                    vehicle.IsServicingStop);
                bool visible = !ShouldHideParkedVehicle(vehicle, status);
                renderer.enabled = visible;
                if (_roadCargoLabels.TryGetValue(vehicle.Id, out TextMesh? existingLabel) && existingLabel != null)
                {
                    existingLabel.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                renderer.transform.position = ResolveRoadWorldPosition(vehicle);
                bool isSelected = selectedId.HasValue && selectedId.Value == vehicle.Id;
                if (isSelected)
                {
                    renderer.color = MapPlaceholderPalette.RoadVehicleSelected;
                }
                else if (vehicle.Cargo.IsEmpty)
                {
                    renderer.color = MapPlaceholderPalette.RoadVehicle;
                }
                else
                {
                    renderer.color = MapPlaceholderPalette.RoadVehicleLoaded;
                }

                if (!_roadCargoLabels.TryGetValue(vehicle.Id, out TextMesh? cargoLabel) || cargoLabel == null)
                {
                    cargoLabel = CreateCargoLabel(renderer.transform);
                    _roadCargoLabels[vehicle.Id] = cargoLabel;
                }

                cargoLabel.gameObject.SetActive(true);
                cargoLabel.text = vehicle.Cargo.TotalAmount.ToString();
            }

            RemoveStaleMarkers(_roadMarkers, _roadCargoLabels, seen);
        }

        private void SyncRailVehicles()
        {
            var seen = new HashSet<int>();
            for (int index = 0; index < _world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = _world.RailVehicles[index];
                seen.Add(vehicle.Id);
                if (!_railMarkers.TryGetValue(vehicle.Id, out SpriteRenderer? renderer) || renderer == null)
                {
                    renderer = CreateMarker("RailVehicle_" + vehicle.Id, sortingOrder: 21);
                    _railMarkers[vehicle.Id] = renderer;
                }

                VehicleListStatusKind status = VehicleListStatus.Resolve(
                    vehicle.IsStuck,
                    vehicle.IsOnEdge,
                    vehicle.IsServicingStop);
                bool visible = !ShouldHideParkedRailVehicle(vehicle, status);
                renderer.enabled = visible;
                if (_railCargoLabels.TryGetValue(vehicle.Id, out TextMesh? existingLabel) && existingLabel != null)
                {
                    existingLabel.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                renderer.transform.position = ResolveRailWorldPosition(vehicle);
                renderer.color = vehicle.Cargo.IsEmpty
                    ? MapPlaceholderPalette.RailVehicle
                    : MapPlaceholderPalette.RailVehicleLoaded;

                if (!_railCargoLabels.TryGetValue(vehicle.Id, out TextMesh? cargoLabel) || cargoLabel == null)
                {
                    cargoLabel = CreateCargoLabel(renderer.transform);
                    _railCargoLabels[vehicle.Id] = cargoLabel;
                }

                cargoLabel.gameObject.SetActive(true);
                cargoLabel.text = vehicle.Cargo.TotalAmount.ToString();
            }

            RemoveStaleMarkers(_railMarkers, _railCargoLabels, seen);
        }

        private static void RemoveStaleMarkers(
            Dictionary<int, SpriteRenderer> markers,
            Dictionary<int, TextMesh> cargoLabels,
            HashSet<int> seen)
        {
            var removeIds = new List<int>();
            foreach (KeyValuePair<int, SpriteRenderer> pair in markers)
            {
                if (!seen.Contains(pair.Key))
                {
                    removeIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                int id = removeIds[index];
                if (markers.TryGetValue(id, out SpriteRenderer? renderer) && renderer != null)
                {
                    DestroyUnityObject(renderer.gameObject);
                }

                markers.Remove(id);
                cargoLabels.Remove(id);
            }
        }

        private SpriteRenderer CreateMarker(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = new Vector3(0.65f, 0.65f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _diamondSprite;
            renderer.sortingOrder = sortingOrder;
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

        /// <summary>
        /// Parked road markers stay off the map only when the vehicle has no looping route (&lt; 2 stops).
        /// </summary>
        public static bool ShouldHideParkedVehicle(RoadVehicle vehicle, VehicleListStatusKind status)
        {
            return status == VehicleListStatusKind.Parked && vehicle.Orders.Count < 2;
        }

        /// <summary>
        /// Parked rail markers stay off the map only when the train has no looping route (&lt; 2 stops).
        /// </summary>
        public static bool ShouldHideParkedRailVehicle(RailVehicle vehicle, VehicleListStatusKind status)
        {
            return status == VehicleListStatusKind.Parked && vehicle.Orders.Count < 2;
        }

        private Vector3 ResolveRoadWorldPosition(RoadVehicle vehicle)
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

            return InterpolateWorldPosition(fromTile, toTile, fraction);
        }

        private Vector3 ResolveRailWorldPosition(RailVehicle vehicle)
        {
            if (!RailVehicleMovement.TryGetPresentationSegment(
                    _world,
                    vehicle,
                    out TileCoordinate fromTile,
                    out TileCoordinate toTile,
                    out FixedPoint fraction))
            {
                return Vector3.zero;
            }

            return InterpolateWorldPosition(fromTile, toTile, fraction);
        }

        private static Vector3 InterpolateWorldPosition(
            TileCoordinate fromTile,
            TileCoordinate toTile,
            FixedPoint fraction)
        {
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
