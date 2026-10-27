#nullable enable
using Unity.Profiling;
using UnityEngine;

namespace TransportTycoon.Presentation.Diagnostics
{
    /// <summary>
    /// Development players only: logs the averaged frame rate, main-thread time, script Update time and managed
    /// allocations every few seconds, so CPU fixes can be measured before and after in a WebGL browser console
    /// (Issue #70). Release players never create it.
    /// </summary>
    public sealed class FrameStatsLogger : MonoBehaviour
    {
        private const double WindowSeconds = 5d;
        private const double NanosecondsPerMillisecond = 1_000_000d;

        private readonly FrameStatsWindow _window = new FrameStatsWindow(WindowSeconds);
        private ProfilerRecorder _mainThread;
        private ProfilerRecorder _scripts;
        private ProfilerRecorder _gcAllocated;

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInDevelopmentPlayer()
        {
            var host = new GameObject(nameof(FrameStatsLogger));
            DontDestroyOnLoad(host);
            host.AddComponent<FrameStatsLogger>();
        }
#endif

        private void OnEnable()
        {
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _scripts = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate");
            _gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void OnDisable()
        {
            _mainThread.Dispose();
            _scripts.Dispose();
            _gcAllocated.Dispose();
        }

        private void Update()
        {
            // Recorder values are those of the previous completed frame, which pairs with the previous frame's delta.
            bool completed = _window.AddFrame(
                Time.unscaledDeltaTime,
                ReadMilliseconds(_mainThread),
                ReadMilliseconds(_scripts),
                _gcAllocated.Valid ? _gcAllocated.LastValue : (long?)null,
                out FrameStatsSummary summary);
            if (completed)
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this, "{0}", summary.ToLogLine());
            }
        }

        private static double? ReadMilliseconds(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue / NanosecondsPerMillisecond : (double?)null;
        }
    }
}
