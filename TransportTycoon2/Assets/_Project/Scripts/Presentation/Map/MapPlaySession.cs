#nullable enable
using System;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Play Mode bootstrap: generates a Core world and renders it with <see cref="ChunkedMapView"/>.
    /// Default map size is S (128) for Issue #37 iteration.
    /// </summary>
    public sealed class MapPlaySession : MonoBehaviour
    {
        [SerializeField]
        private int _mapSizeTiles = 128;

        [SerializeField]
        private ulong _worldSeed = 37001UL;

        [SerializeField]
        private float _orthographicSize = 48f;

        private ChunkedMapView? _mapView;

        private void Start()
        {
            MapSize mapSize = ResolveMapSize(_mapSizeTiles);
            WorldGenerationSettings settings = CreateDemoSettings(mapSize);
            GeneratedWorld world = WorldGenerator.Generate(settings, _worldSeed);
            _mapView = new ChunkedMapView(world, transform);

            Camera? camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("MapCamera");
                camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = _orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
            Vector3 center = IsometricProjection.MapCenter((int)mapSize);
            camera.transform.position = new Vector3(center.x, center.y, -10f);
            camera.transform.rotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            _mapView?.Dispose();
            _mapView = null;
        }

        /// <summary>
        /// Marks a tile dirty and rebuilds its chunk. Intended for EditMode/smoke tests of dirty rebuild.
        /// </summary>
        public int NotifyTileChangedForTests(int tileX, int tileY)
        {
            if (_mapView == null)
            {
                throw new InvalidOperationException("Map view is not initialized.");
            }

            _mapView.MarkTileDirty(tileX, tileY);
            return _mapView.RebuildDirtyChunks();
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
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0),
                    new IndustryPlacementRule("mine", 1, Array.Empty<string>(), new[] { "metal" }, 2, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 2, 60),
                    new IndustryPlacementRule(
                        "furniture_factory",
                        3,
                        new[] { "planks", "metal" },
                        new[] { "furniture" },
                        1,
                        60),
                });
        }
    }
}
