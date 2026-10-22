#nullable enable
using System;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Play Mode bootstrap: generates a Core world and renders it with <see cref="ChunkedMapView"/>.
    /// Default map size is S (128). Initial zoom favors inspection over fitting the whole map.
    /// </summary>
    public sealed class MapPlaySession : MonoBehaviour
    {
        [SerializeField]
        private int _mapSizeTiles = 128;

        [SerializeField]
        private ulong _worldSeed = 37001UL;

        [SerializeField]
        private float _orthographicSize = 20f;

        private ChunkedMapView? _mapView;
        private MapTileInteractionController? _tileInteraction;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;

        private void Start()
        {
            MapSize mapSize = ResolveMapSize(_mapSizeTiles);
            WorldGenerationSettings settings = CreateDemoSettings(mapSize);
            GeneratedWorld world = WorldGenerator.Generate(settings, _worldSeed);
            _commandProcessor = new SimulationCommandProcessor<GeneratedWorld>(world);
            _mapView = new ChunkedMapView(world, transform);

            Camera? camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("MapCamera");
                camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
            camera.transform.rotation = Quaternion.identity;
            if (camera.transform.position.z == 0f)
            {
                camera.transform.position = new Vector3(0f, 0f, -10f);
            }

            MapCameraController controller = camera.GetComponent<MapCameraController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<MapCameraController>();
            }

            controller.Configure((int)mapSize, _orthographicSize);

            _tileInteraction = GetComponent<MapTileInteractionController>();
            if (_tileInteraction == null)
            {
                _tileInteraction = gameObject.AddComponent<MapTileInteractionController>();
            }

            _tileInteraction.Configure(
                world,
                camera,
                transform,
                _commandProcessor,
                coordinate => { NotifyTileChanged(coordinate); });
        }

        private void OnDestroy()
        {
            _mapView?.Dispose();
            _mapView = null;
            _tileInteraction = null;
        }

        /// <summary>
        /// Marks a tile dirty and rebuilds its chunk. Intended for EditMode/smoke tests of dirty rebuild.
        /// </summary>
        public int NotifyTileChangedForTests(int tileX, int tileY)
        {
            return NotifyTileChanged(new TileCoordinate(tileX, tileY));
        }

        private int NotifyTileChanged(TileCoordinate coordinate)
        {
            if (_mapView == null)
            {
                throw new InvalidOperationException("Map view is not initialized.");
            }

            _mapView.MarkTileDirty(coordinate.X, coordinate.Y);
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
