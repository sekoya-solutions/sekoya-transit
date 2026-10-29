#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace SekoyaTransit.Spikes.WebGL
{
    // Composition root of the throwaway spike: owns both map candidates, vehicles, benchmark and save tests.
    public sealed class SpikeApp : MonoBehaviour
    {
        private const int WarmupFrames = 60;
        private const float PhaseSeconds = 5f;

        [SerializeField] private Material spriteMaterial = null!;

        private readonly Dictionary<SpikeMapMode, long> _buildMs = new Dictionary<SpikeMapMode, long>();
        private readonly StringBuilder _results = new StringBuilder();
        private Texture2D _texture = null!;
        private SpikeCameraRig _rig = null!;
        private SpikeMeshMap? _meshMap;
        private SpikeTilemapMap? _tilemapMap;
        private SpikeVehicles _vehicles = null!;
        private SpikeMapMode _mode = SpikeMapMode.Mesh;
        private bool _busy;

        public event Action<string>? LineAdded;

        public SpikeMapMode Mode => _mode;

        public int VehicleCount => _vehicles.Count;

        public bool Busy => _busy;

        public string Results => _results.ToString();

        public void SetMode(SpikeMapMode mode)
        {
            _mode = mode;
            if (mode == SpikeMapMode.Mesh && _meshMap == null)
            {
                var sw = Stopwatch.StartNew();
                _meshMap = new SpikeMeshMap(spriteMaterial, _texture);
                Record(mode, sw.ElapsedMilliseconds);
            }
            else if (mode == SpikeMapMode.Tilemap && _tilemapMap == null)
            {
                var sw = Stopwatch.StartNew();
                _tilemapMap = new SpikeTilemapMap(spriteMaterial, _texture);
                Record(mode, sw.ElapsedMilliseconds);
            }

            _meshMap?.Root.SetActive(mode == SpikeMapMode.Mesh);
            _tilemapMap?.Root.SetActive(mode == SpikeMapMode.Tilemap);
        }

        public void SetVehicleCount(int count)
        {
            _vehicles.SetCount(Mathf.Max(0, count));
        }

        public void RunBenchmark()
        {
            if (!_busy)
            {
                StartCoroutine(Guard(Benchmark()));
            }
        }

        public void RunSaveWrite()
        {
            if (!_busy)
            {
                StartCoroutine(Guard(SpikeSaveTest.Write(Log)));
            }
        }

        public void RunSaveVerify()
        {
            if (!_busy)
            {
                StartCoroutine(Guard(SpikeSaveTest.Verify(Log)));
            }
        }

        public void Log(string line)
        {
            _results.AppendLine(line);
            UnityEngine.Debug.Log("[spike] " + line);
            SpikeWeb.Publish(_results.ToString());
            LineAdded?.Invoke(line);
        }

        private void Awake()
        {
            _texture = SpikeIso.CreateDiamondTexture();
            _rig = FindFirstObjectByType<SpikeCameraRig>();
            _vehicles = new SpikeVehicles(spriteMaterial);
            Application.targetFrameRate = -1;
        }

        private void Start()
        {
            SpikeWeb.Init();
            SetMode(SpikeMapMode.Mesh);
            SetVehicleCount(2000);
            _rig.SetView(SpikeIso.TileToWorld(256f, 256f), 20f);

            string args = SpikeWeb.IsAvailable ? SpikeWeb.Query() : string.Join(" ", Environment.GetCommandLineArgs());
            Log($"env platform={Application.platform} unity={Application.unityVersion} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}");
            if (args.Contains("benchmark"))
            {
                StartCoroutine(Guard(AutoRun(args.Contains("no-quit"))));
            }
            else if (args.Contains("verify"))
            {
                StartCoroutine(Guard(SpikeSaveTest.Verify(Log)));
            }
        }

        private void Update()
        {
            _vehicles.Tick(Time.deltaTime);
        }

        private IEnumerator Guard(IEnumerator inner)
        {
            _busy = true;
            yield return inner;
            _busy = false;
        }

        private IEnumerator AutoRun(bool keepOpen)
        {
            yield return SpikeSaveTest.Write(Log);
            yield return Benchmark();
            if (!SpikeWeb.IsAvailable && !keepOpen)
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "spike-results.txt"), _results.ToString());
                Application.Quit();
            }
        }

        private IEnumerator Benchmark()
        {
            _rig.ManualControl = false;
            QualitySettings.vSyncCount = 0;
            SpikeMapMode original = _mode;
            int originalVehicles = _vehicles.Count;

            foreach (SpikeMapMode mode in new[] { SpikeMapMode.Mesh, SpikeMapMode.Tilemap })
            {
                foreach (int vehicles in new[] { 0, 2000, 10000, 30000 })
                {
                    SetMode(mode);
                    SetVehicleCount(vehicles);
                    yield return null;
                    foreach ((string name, float size) in new[] { ("close", 6f), ("mid", 30f), ("far", 130f) })
                    {
                        yield return MeasurePhase(mode, vehicles, name, size);
                    }
                }
            }

            _rig.ManualControl = true;
            SetMode(original);
            SetVehicleCount(originalVehicles);
            Log("benchmark.done");
        }

        private IEnumerator MeasurePhase(SpikeMapMode mode, int vehicles, string phase, float size)
        {
            var frames = new List<float>(1024);
            var timings = new FrameTiming[1];
            double cpuMain = 0;
            double cpuRender = 0;
            double gpu = 0;
            float elapsed = 0f;
            int frame = 0;
            while (elapsed < PhaseSeconds)
            {
                float t = elapsed / PhaseSeconds;
                Vector3 from = SpikeIso.TileToWorld(150f, 350f);
                Vector3 to = SpikeIso.TileToWorld(350f, 150f);
                Vector3 center = phase == "far" ? SpikeIso.TileToWorld(256f, 256f) : Vector3.Lerp(from, to, t);
                _rig.SetView(center, size);

                yield return null;
                frame++;
                if (frame > WarmupFrames)
                {
                    frames.Add(Time.unscaledDeltaTime);
                    elapsed += Time.unscaledDeltaTime;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        cpuMain += timings[0].cpuMainThreadFrameTime;
                        cpuRender += timings[0].cpuRenderThreadFrameTime;
                        gpu += timings[0].gpuFrameTime;
                    }
                }
            }

            frames.Sort();
            float total = 0f;
            foreach (float f in frames)
            {
                total += f;
            }

            float avgFps = frames.Count / total;
            float p95 = frames[(int)(frames.Count * 0.95f)] * 1000f;
            float p99 = frames[(int)(frames.Count * 0.99f)] * 1000f;
            float max = frames[frames.Count - 1] * 1000f;
            long managedMb = GC.GetTotalMemory(false) / (1024 * 1024);
            long unityMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            Log($"bench mode={mode} vehicles={vehicles} phase={phase} frames={frames.Count} avgFps={avgFps:F1} p95Ms={p95:F1} p99Ms={p99:F1} maxMs={max:F1} cpuMainMs={cpuMain / frames.Count:F2} cpuRenderMs={cpuRender / frames.Count:F2} gpuMs={gpu / frames.Count:F2} managedMb={managedMb} unityMb={unityMb}");
        }

        private void Record(SpikeMapMode mode, long ms)
        {
            _buildMs[mode] = ms;
            Log($"build mode={mode} ms={ms} tiles={SpikeIso.MapSize * SpikeIso.MapSize}");
        }
    }
}
