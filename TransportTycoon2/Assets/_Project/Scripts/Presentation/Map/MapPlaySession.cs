#nullable enable
using System;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Play Mode bootstrap: generates a Core world and renders it with <see cref="ChunkedMapView"/>.
    /// Default map size is S (128). Initial zoom favors inspection over fitting the whole map.
    /// Advances the simulation so road vehicles move each tick.
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
        private MapVehicleView? _vehicleView;
        private MapStationCargoLabelView? _stationCargoLabels;
        private SimulationCommandProcessor<GeneratedWorld>? _commandProcessor;
        private GeneratedWorld? _world;
        private SimulationLoop? _simulationLoop;

        private void Start()
        {
            MapSize mapSize = ResolveMapSize(_mapSizeTiles);
            WorldGenerationSettings settings = CreateDemoSettings(mapSize);
            _world = WorldGenerator.Generate(settings, _worldSeed);
            _commandProcessor = new SimulationCommandProcessor<GeneratedWorld>(_world);
            _mapView = new ChunkedMapView(_world, transform);
            _vehicleView = new MapVehicleView(_world, transform);
            _stationCargoLabels = new MapStationCargoLabelView(_world, transform);
            _simulationLoop = new SimulationLoop(TimeSpan.FromMilliseconds(50), Tick.Zero);

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
                _world,
                camera,
                transform,
                _commandProcessor,
                coordinate =>
                {
                    NotifyTileChanged(coordinate);
                    SeedStationCargoIfNeeded(coordinate);
                });

            RoadStationCargoSeeding.SeedAtIndustryAdjacentStations(_world);
            _vehicleView.Sync();
            _stationCargoLabels.Sync();
        }

        private void SeedStationCargoIfNeeded(TileCoordinate coordinate)
        {
            if (_world == null)
            {
                return;
            }

            if (!_world.TryGetRoadStationAt(coordinate, out RoadStation? station) || station == null)
            {
                return;
            }

            if (!station.Cargo.IsEmpty)
            {
                return;
            }

            int townCount = _world.TownAnchors.Count;
            var neighbors = new[]
            {
                new TileCoordinate(coordinate.X, coordinate.Y + 1),
                new TileCoordinate(coordinate.X + 1, coordinate.Y),
                new TileCoordinate(coordinate.X, coordinate.Y - 1),
                new TileCoordinate(coordinate.X - 1, coordinate.Y)
            };

            for (int index = 0; index < neighbors.Length; index++)
            {
                TileCoordinate neighbor = neighbors[index];
                if (!_world.Map.Contains(neighbor))
                {
                    continue;
                }

                if (_world.Map.GetObjectId(neighbor) >= townCount)
                {
                    station.TryAddCargo(
                        RoadStationCargoSeeding.DemoCargoId,
                        RoadStationCargoSeeding.DemoAmountPerStation);
                    return;
                }
            }
        }

        private void Update()
        {
            if (_simulationLoop == null
                || _world == null
                || _vehicleView == null
                || _stationCargoLabels == null)
            {
                return;
            }

            _simulationLoop.Advance(
                TimeSpan.FromSeconds(Time.deltaTime),
                tick =>
                {
                    RoadVehicleMovement.AdvanceAll(_world);
                    RoadStationCargoDecay.AdvanceAll(_world, tick);
                });
            _vehicleView.Sync();
            _stationCargoLabels.Sync();
        }

        private void OnDestroy()
        {
            _mapView?.Dispose();
            _mapView = null;
            _vehicleView?.Dispose();
            _vehicleView = null;
            _stationCargoLabels?.Dispose();
            _stationCargoLabels = null;
            _tileInteraction = null;
            _world = null;
            _simulationLoop = null;
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
