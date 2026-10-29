#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using SekoyaTransit.UI.Shell;
using UnityEngine;
using UnityEngine.Rendering;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Renders road and rail vehicles as colored diamond placeholders that follow Core positions.
    /// Parked idle / loco-only trains are hidden (V1; V2 will show them in depots). Rail wagons follow
    /// a history of locomotive world positions (snake trail) so they stay on the path the loco traveled.
    /// </summary>
    public sealed class MapVehicleView : IDisposable
    {
        private const float VehicleWorldZ = -1.2f;

        /// <summary>
        /// World-unit gap along the loco trail between loco and first wagon / between wagons
        /// (~one orthogonal tile step in isometric projection).
        /// </summary>
        private const float WagonTrailSpacing = 0.56f;

        // Placeholder diamonds are drawn smaller than a tile; baked vehicle sprites are already sized to the tile.
        private const float PlaceholderMarkerScale = 0.65f;

        private static readonly Color SelectedTint = new Color(0.55f, 1f, 1f, 1f);

        private readonly GeneratedWorld _world;
        private readonly MapArtSet? _art;
        private readonly float _markerScale;
        private const float CargoLabelCharacterSize = 0.07f;
        private const float CargoLabelIconSize = 0.18f;

        private readonly GameObject _root;
        private readonly Texture2D _diamondTexture;
        private readonly Sprite _diamondSprite;
        private readonly Dictionary<int, SpriteRenderer> _roadMarkers = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, MapIconLabel> _roadCargoLabels = new Dictionary<int, MapIconLabel>();
        private readonly Dictionary<int, SpriteRenderer> _railMarkers = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, MapIconLabel> _railCargoLabels = new Dictionary<int, MapIconLabel>();
        private readonly Dictionary<int, SpriteRenderer[]> _railWagonMarkers = new Dictionary<int, SpriteRenderer[]>();
        private readonly Dictionary<int, RailWagonTrailHistory> _railTrails = new Dictionary<int, RailWagonTrailHistory>();
        private readonly Dictionary<int, MapHeading> _roadHeadings = new Dictionary<int, MapHeading>();
        private readonly Dictionary<int, MapHeading> _railHeadings = new Dictionary<int, MapHeading>();

        // Last cargo content key shown by each label: TextMesh rebuilds its mesh on every text assignment (Issue #70).
        private readonly Dictionary<int, int> _roadCargoShown = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _railCargoShown = new Dictionary<int, int>();

        // Scratch collections reused by every Sync, so a frame allocates no managed memory (Issue #70).
        private readonly HashSet<int> _seenRoadIds = new HashSet<int>();
        private readonly HashSet<int> _seenRailIds = new HashSet<int>();
        private readonly List<int> _staleIds = new List<int>();
        private float[] _wagonX = Array.Empty<float>();
        private float[] _wagonY = Array.Empty<float>();
        private Func<int?>? _selectedVehicleIdProvider;
        private Func<VehicleListKind?>? _selectedVehicleKindProvider;
        private bool _disposed;

        /// <summary>
        /// Initializes a vehicle view bound to a Core world. With <paramref name="art"/>, vehicles use the baked
        /// heading sprites of ADR 0032; without it, the placeholder colored diamonds.
        /// </summary>
        public MapVehicleView(GeneratedWorld world, Transform? parent, MapArtSet? art = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _art = art;
            _markerScale = art != null ? 1f : PlaceholderMarkerScale;
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
        /// Optional providers for the vehicle currently selected in the Vehicles route editor.
        /// Selected moving vehicles use a distinct highlight color on the map.
        /// </summary>
        public void SetSelectedVehicleProviders(
            Func<int?>? selectedVehicleIdProvider,
            Func<VehicleListKind?>? selectedVehicleKindProvider)
        {
            _selectedVehicleIdProvider = selectedVehicleIdProvider;
            _selectedVehicleKindProvider = selectedVehicleKindProvider;
        }

        /// <summary>
        /// Optional provider for the road vehicle id currently selected in the Vehicles route editor.
        /// Prefer <see cref="SetSelectedVehicleProviders"/> when rail selection is also used.
        /// </summary>
        public void SetSelectedVehicleIdProvider(Func<int?>? selectedVehicleIdProvider)
        {
            _selectedVehicleIdProvider = selectedVehicleIdProvider;
            _selectedVehicleKindProvider = null;
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
            foreach (KeyValuePair<int, SpriteRenderer[]> pair in _railWagonMarkers)
            {
                DestroyWagonMarkers(pair.Value);
            }

            _railWagonMarkers.Clear();
            _railTrails.Clear();
            _roadHeadings.Clear();
            _railHeadings.Clear();
            _roadCargoShown.Clear();
            _railCargoShown.Clear();
        }

        private void SyncRoadVehicles()
        {
            int? selectedId = _selectedVehicleIdProvider?.Invoke();
            HashSet<int> seen = _seenRoadIds;
            seen.Clear();
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
                // A position that cannot be resolved hides the vehicle for the frame instead of drawing it at the map
                // origin (Issue #193).
                bool resolved = TryResolveRoadWorldPosition(vehicle, _roadHeadings, out Vector3 position);
                bool visible = resolved && !ShouldHideParkedVehicle(vehicle, status);
                renderer.enabled = visible;
                if (_roadCargoLabels.TryGetValue(vehicle.Id, out MapIconLabel? existingLabel) && existingLabel != null)
                {
                    existingLabel.GameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                renderer.transform.position = position;
                renderer.sortingOrder = MapSortingOrder.ForWorldY(position.y);
                VehicleListKind? selectedKind = _selectedVehicleKindProvider?.Invoke();
                bool isSelected = selectedId.HasValue
                    && selectedId.Value == vehicle.Id
                    && (!selectedKind.HasValue || selectedKind.Value == VehicleListKind.Road);
                if (_art != null)
                {
                    renderer.sprite = _art.GetVehicle(MapVehicleArt.Truck, HeadingOf(_roadHeadings, vehicle.Id));
                    renderer.color = isSelected ? SelectedTint : Color.white;
                }
                else if (isSelected)
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

                if (!_roadCargoLabels.TryGetValue(vehicle.Id, out MapIconLabel? cargoLabel) || cargoLabel == null)
                {
                    cargoLabel = CreateCargoLabel(renderer.transform);
                    _roadCargoLabels[vehicle.Id] = cargoLabel;
                    _roadCargoShown.Remove(vehicle.Id);
                }

                cargoLabel.GameObject.SetActive(true);
                ShowCargo(cargoLabel, _roadCargoShown, vehicle.Id, vehicle.Cargo);
            }

            RemoveStaleMarkers(_roadMarkers, _roadCargoLabels, _roadCargoShown, seen);
            RemoveStaleHeadings(_roadHeadings, seen);
        }

        private void SyncRailVehicles()
        {
            HashSet<int> seen = _seenRailIds;
            seen.Clear();
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
                // A position that cannot be resolved hides the train for the frame instead of drawing it at the map
                // origin, and is not recorded in the wagon trail (Issue #193).
                bool resolved = TryResolveRailWorldPosition(vehicle, _railHeadings, out Vector3 locoPosition);
                bool visible = resolved && !ShouldHideRailVehicle(vehicle, status);
                renderer.enabled = visible;
                if (_railCargoLabels.TryGetValue(vehicle.Id, out MapIconLabel? existingLabel) && existingLabel != null)
                {
                    existingLabel.GameObject.SetActive(visible);
                }

                SpriteRenderer[] wagons = EnsureRailWagonMarkers(vehicle.Id, vehicle.WagonCount);
                for (int wagonIndex = 0; wagonIndex < wagons.Length; wagonIndex++)
                {
                    wagons[wagonIndex].enabled = visible;
                }

                if (!visible)
                {
                    if (resolved
                        && _railTrails.TryGetValue(vehicle.Id, out RailWagonTrailHistory? hiddenTrail)
                        && hiddenTrail != null)
                    {
                        hiddenTrail.Clear();
                    }

                    continue;
                }

                renderer.transform.position = locoPosition;
                renderer.sortingOrder = MapSortingOrder.ForWorldY(locoPosition.y);
                int? selectedId = _selectedVehicleIdProvider?.Invoke();
                VehicleListKind? selectedKind = _selectedVehicleKindProvider?.Invoke();
                bool isSelected = selectedId.HasValue
                    && selectedId.Value == vehicle.Id
                    && selectedKind.HasValue
                    && selectedKind.Value == VehicleListKind.Rail;
                if (_art != null)
                {
                    renderer.sprite = _art.GetVehicle(MapVehicleArt.Locomotive, HeadingOf(_railHeadings, vehicle.Id));
                    renderer.color = isSelected ? SelectedTint : Color.white;
                }
                else if (isSelected)
                {
                    renderer.color = MapPlaceholderPalette.RailVehicleSelected;
                }
                else if (vehicle.Cargo.IsEmpty)
                {
                    renderer.color = MapPlaceholderPalette.RailVehicle;
                }
                else
                {
                    renderer.color = MapPlaceholderPalette.RailVehicleLoaded;
                }

                PlaceRailWagonsFromTrail(vehicle, locoPosition, wagons);

                if (!_railCargoLabels.TryGetValue(vehicle.Id, out MapIconLabel? cargoLabel) || cargoLabel == null)
                {
                    cargoLabel = CreateCargoLabel(renderer.transform);
                    _railCargoLabels[vehicle.Id] = cargoLabel;
                    _railCargoShown.Remove(vehicle.Id);
                }

                cargoLabel.GameObject.SetActive(true);
                ShowCargo(cargoLabel, _railCargoShown, vehicle.Id, vehicle.Cargo);
            }

            RemoveStaleRailMarkers(seen);
            RemoveStaleHeadings(_railHeadings, seen);
        }

        private SpriteRenderer[] EnsureRailWagonMarkers(int vehicleId, int wagonCount)
        {
            if (_railWagonMarkers.TryGetValue(vehicleId, out SpriteRenderer[]? existing) && existing != null)
            {
                if (existing.Length == wagonCount)
                {
                    return existing;
                }

                DestroyWagonMarkers(existing);
            }

            var created = new SpriteRenderer[wagonCount];
            for (int index = 0; index < wagonCount; index++)
            {
                created[index] = CreateMarker(
                    "RailWagon_" + vehicleId + "_" + index,
                    sortingOrder: 20);
            }

            _railWagonMarkers[vehicleId] = created;
            return created;
        }

        private void RemoveStaleRailMarkers(HashSet<int> seen)
        {
            RemoveStaleMarkers(_railMarkers, _railCargoLabels, _railCargoShown, seen);
            List<int> removeIds = _staleIds;
            removeIds.Clear();
            foreach (KeyValuePair<int, SpriteRenderer[]> pair in _railWagonMarkers)
            {
                if (!seen.Contains(pair.Key))
                {
                    removeIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                int id = removeIds[index];
                if (_railWagonMarkers.TryGetValue(id, out SpriteRenderer[]? wagons) && wagons != null)
                {
                    DestroyWagonMarkers(wagons);
                }

                _railWagonMarkers.Remove(id);
                _railTrails.Remove(id);
            }
        }

        private static void DestroyWagonMarkers(SpriteRenderer[] wagons)
        {
            for (int index = 0; index < wagons.Length; index++)
            {
                SpriteRenderer wagon = wagons[index];
                if (wagon != null)
                {
                    DestroyUnityObject(wagon.gameObject);
                }
            }
        }

        private void PlaceRailWagonsFromTrail(
            RailVehicle vehicle,
            Vector3 locoPosition,
            SpriteRenderer[] wagons)
        {
            if (wagons.Length == 0)
            {
                return;
            }

            if (!_railTrails.TryGetValue(vehicle.Id, out RailWagonTrailHistory? trail) || trail == null)
            {
                trail = new RailWagonTrailHistory();
                _railTrails[vehicle.Id] = trail;
            }

            // Cold start / teleport: seed behind the loco along graph tiles so wagons are not
            // stacked on the loco before the first movement samples arrive.
            float neededLength = WagonTrailSpacing * wagons.Length;
            if (trail.Count < 2 || ApproximateTrailLength(trail) < neededLength * 0.9f)
            {
                TrySeedTrailFromGraph(vehicle, locoPosition, wagons.Length, trail);
            }

            trail.Record(locoPosition.x, locoPosition.y);

            if (_wagonX.Length < wagons.Length)
            {
                _wagonX = new float[wagons.Length];
                _wagonY = new float[wagons.Length];
            }

            float[] outX = _wagonX;
            float[] outY = _wagonY;
            trail.SampleWagons(
                locoPosition.x,
                locoPosition.y,
                wagons.Length,
                WagonTrailSpacing,
                outX,
                outY);

            MapHeading heading = HeadingOf(_railHeadings, vehicle.Id);
            float aheadX = locoPosition.x;
            float aheadY = locoPosition.y;
            for (int wagonIndex = 0; wagonIndex < wagons.Length; wagonIndex++)
            {
                SpriteRenderer wagon = wagons[wagonIndex];
                wagon.transform.position = new Vector3(outX[wagonIndex], outY[wagonIndex], VehicleWorldZ);
                wagon.sortingOrder = MapSortingOrder.ForWorldY(outY[wagonIndex]);
                if (_art != null)
                {
                    // Each wagon faces the car ahead of it, so it turns where the track turns.
                    heading = MapHeadings.FromWorldDelta(aheadX - outX[wagonIndex], aheadY - outY[wagonIndex], heading);
                    wagon.sprite = _art.GetVehicle(MapVehicleArt.Wagon, heading);
                    wagon.color = Color.white;
                }
                else
                {
                    wagon.color = MapPlaceholderPalette.RailWagon;
                }

                aheadX = outX[wagonIndex];
                aheadY = outY[wagonIndex];
            }
        }

        private void TrySeedTrailFromGraph(
            RailVehicle vehicle,
            Vector3 locoPosition,
            int wagonCount,
            RailWagonTrailHistory trail)
        {
            var trailTiles = new TileCoordinate[wagonCount];
            if (!RailVehicleMovement.TryGetWagonTrailTiles(_world, vehicle, trailTiles))
            {
                return;
            }

            // Oldest first: farthest wagon tile → closest → loco tip.
            var seedX = new float[wagonCount + 1];
            var seedY = new float[wagonCount + 1];
            for (int index = 0; index < wagonCount; index++)
            {
                TileCoordinate tile = trailTiles[wagonCount - 1 - index];
                Vector3 world = IsometricProjection.TileToWorld(tile.X, tile.Y);
                seedX[index] = world.x;
                seedY[index] = world.y;
            }

            seedX[wagonCount] = locoPosition.x;
            seedY[wagonCount] = locoPosition.y;
            trail.SeedOldestFirst(seedX, seedY);
        }

        private static float ApproximateTrailLength(RailWagonTrailHistory trail)
        {
            // Sample with a throwaway loco tip at the last recorded point to measure buffer span.
            // Count alone is enough for the cold-start gate; length check uses wagon spacing budget.
            return trail.Count * WagonTrailSpacing;
        }

        private void RemoveStaleMarkers(
            Dictionary<int, SpriteRenderer> markers,
            Dictionary<int, MapIconLabel> cargoLabels,
            Dictionary<int, int> cargoShown,
            HashSet<int> seen)
        {
            List<int> removeIds = _staleIds;
            removeIds.Clear();
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
                cargoShown.Remove(id);
            }
        }

        // "40 [wood]" per carried cargo, square brackets standing for the cargo icon; "0" when empty (Issue #193).
        private void ShowCargo(MapIconLabel label, Dictionary<int, int> shown, int vehicleId, CargoInventory cargo)
        {
            int key = CargoKey(cargo);
            if (shown.TryGetValue(vehicleId, out int shownKey) && shownKey == key)
            {
                return;
            }

            shown[vehicleId] = key;
            label.Begin();
            if (cargo.IsEmpty)
            {
                label.AddText("0");
            }

            for (int index = 0; index < cargo.Stacks.Count; index++)
            {
                CargoAmount stack = cargo.Stacks[index];
                label.AddText(stack.Amount.ToString(CultureInfo.InvariantCulture));
                label.AddIcon(_art?.GetCargoIcon(stack.CargoId));
            }

            label.End();
        }

        private static int CargoKey(CargoInventory cargo)
        {
            unchecked
            {
                int key = 17;
                for (int index = 0; index < cargo.Stacks.Count; index++)
                {
                    CargoAmount stack = cargo.Stacks[index];
                    key = (key * 31) + stack.Amount;
                    key = (key * 31) + StringComparer.Ordinal.GetHashCode(stack.CargoId);
                }

                return key;
            }
        }

        private SpriteRenderer CreateMarker(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = new Vector3(_markerScale, _markerScale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _diamondSprite;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private MapIconLabel CreateCargoLabel(Transform parent)
        {
            var label = new MapIconLabel(parent, "CargoAmount", CargoLabelCharacterSize, CargoLabelIconSize);
            Transform transform = label.GameObject.transform;

            // 0.36 world units above the vehicle pivot, whatever the marker scale.
            transform.localPosition = new Vector3(0f, 0.36f / _markerScale, -0.1f);
            transform.localScale = new Vector3(1f / _markerScale, 1f / _markerScale, 1f);
            return label;
        }

        private static MapHeading HeadingOf(Dictionary<int, MapHeading> headings, int vehicleId)
        {
            return headings.TryGetValue(vehicleId, out MapHeading heading) ? heading : MapHeading.East;
        }

        private void RemoveStaleHeadings(Dictionary<int, MapHeading> headings, HashSet<int> seen)
        {
            List<int> removeIds = _staleIds;
            removeIds.Clear();
            foreach (KeyValuePair<int, MapHeading> pair in headings)
            {
                if (!seen.Contains(pair.Key))
                {
                    removeIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                headings.Remove(removeIds[index]);
            }
        }

        /// <summary>
        /// Parked road markers stay off the map only when the vehicle has no looping route (&lt; 2 stops).
        /// </summary>
        public static bool ShouldHideParkedVehicle(RoadVehicle vehicle, VehicleListStatusKind status)
        {
            return status == VehicleListStatusKind.Parked && vehicle.Orders.Count < 2;
        }

        /// <summary>
        /// Rail markers stay off the map when the loco has no wagons (V1 has no depot) or when
        /// parked idle without a looping route (&lt; 2 stops). Mid-route parked trains with wagons show.
        /// </summary>
        public static bool ShouldHideRailVehicle(RailVehicle vehicle, VehicleListStatusKind status)
        {
            if (vehicle.WagonCount < 1)
            {
                return true;
            }

            return status == VehicleListStatusKind.Parked && vehicle.Orders.Count < 2;
        }

        /// <summary>
        /// Backward-compatible alias for <see cref="ShouldHideRailVehicle"/>.
        /// </summary>
        public static bool ShouldHideParkedRailVehicle(RailVehicle vehicle, VehicleListStatusKind status)
        {
            return ShouldHideRailVehicle(vehicle, status);
        }

        // Also records the vehicle's heading from its current segment; a vehicle standing still keeps its last one.
        private bool TryResolveRoadWorldPosition(
            RoadVehicle vehicle,
            Dictionary<int, MapHeading> headings,
            out Vector3 position)
        {
            if (!RoadVehicleMovement.TryGetPresentationSegment(
                    _world,
                    vehicle,
                    out TileCoordinate fromTile,
                    out TileCoordinate toTile,
                    out FixedPoint fraction))
            {
                position = Vector3.zero;
                return false;
            }

            RecordHeading(headings, vehicle.Id, fromTile, toTile);
            position = InterpolateWorldPosition(fromTile, toTile, fraction);
            return true;
        }

        private bool TryResolveRailWorldPosition(
            RailVehicle vehicle,
            Dictionary<int, MapHeading> headings,
            out Vector3 position)
        {
            if (!RailVehicleMovement.TryGetPresentationSegment(
                    _world,
                    vehicle,
                    out TileCoordinate fromTile,
                    out TileCoordinate toTile,
                    out FixedPoint fraction))
            {
                position = Vector3.zero;
                return false;
            }

            RecordHeading(headings, vehicle.Id, fromTile, toTile);
            position = InterpolateWorldPosition(fromTile, toTile, fraction);
            return true;
        }

        private static void RecordHeading(
            Dictionary<int, MapHeading> headings,
            int vehicleId,
            TileCoordinate fromTile,
            TileCoordinate toTile)
        {
            headings[vehicleId] = MapHeadings.FromTileDelta(
                toTile.X - fromTile.X,
                toTile.Y - fromTile.Y,
                HeadingOf(headings, vehicleId));
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
