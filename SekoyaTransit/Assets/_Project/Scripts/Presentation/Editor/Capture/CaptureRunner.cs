#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Shell;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SekoyaTransit.Presentation.Editor.Capture
{
    /// <summary>
    /// Makes shots one after another (ADR 0036, Issue #352). For each shot it enters Play mode in the MapPlay scene,
    /// loads the shot's save through the game's normal load path, pauses the game, applies the HUD, panels, speed and
    /// first camera view, then starts the Recorder with a fixed frame rate and resumes the game on the first recorded
    /// frame. The Recorder's fixed frame rate makes every frame one fixed step of game time, so a run does not depend
    /// on the machine's speed; the effects timed in real time (toasts, cash popups, goals) follow the same steps
    /// through <see cref="SekoyaTransit.UI.Shell.PresentationClock"/>. Each frame the camera follows
    /// <see cref="CameraPath"/> from the recorded frame index.
    /// Play mode is left between shots, so every shot starts from a fresh load. Entering Play mode does not reload the
    /// scripting domain in this project (Enter Play Mode options), so this static state lives through it.
    /// </summary>
    internal static class CaptureRunner
    {
        private const string ScenePath = "Assets/_Project/Scenes/MapPlay.unity";

        // Failure detection only: a load that has not finished after this long is reported as stuck.
        private const double LoadTimeoutSeconds = 180d;

        private static readonly Queue<Shot> _pending = new Queue<Shot>();
        private static Shot? _shot;
        private static Stage _stage = Stage.Idle;
        private static Action<bool>? _onFinished;
        private static string _savesDirectory = string.Empty;
        private static string _outputDirectory = string.Empty;
        private static MapPlaySession? _session;
        private static MapCameraController? _camera;
        private static Task? _load;
        private static RecorderController? _recorder;
        private static RecorderControllerSettings? _recorderSettings;
        private static double _stageStartedAt;
        private static int _firstRecordedFrame;
        private static bool _resumed;
        private static bool _anyFailed;
        private static readonly List<string> _written = new List<string>();

        private enum Stage
        {
            Idle,
            EnteringPlayMode,
            WaitingForGame,
            Loading,
            Recording,
            LeavingPlayMode,
        }

        /// <summary>
        /// Gets whether a capture is running.
        /// </summary>
        public static bool IsRunning => _stage != Stage.Idle;

        /// <summary>
        /// Starts making <paramref name="shots"/>; <paramref name="onFinished"/> receives true when every shot was
        /// written. Must be called in Edit mode.
        /// </summary>
        public static void Run(IReadOnlyList<Shot> shots, string savesDirectory, string outputDirectory, Action<bool> onFinished)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A capture is already running.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Leave Play mode before capturing.");
            }

            if (shots.Count == 0)
            {
                throw new ArgumentException("No shot to make.", nameof(shots));
            }

            _pending.Clear();
            foreach (Shot shot in shots)
            {
                string save = Path.Combine(savesDirectory, shot.Save);
                if (!File.Exists(save))
                {
                    throw new FileNotFoundException("The save of shot " + shot.Name + " is missing.", save);
                }

                _pending.Enqueue(shot);
            }

            _savesDirectory = savesDirectory;
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(outputDirectory);
            _onFinished = onFinished;
            _anyFailed = false;
            _written.Clear();
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
            StartNextShot();
        }

        private static void StartNextShot()
        {
            if (_pending.Count == 0)
            {
                Finish();
                return;
            }

            _shot = _pending.Dequeue();
            Debug.Log("Capture: shot " + _shot.Name + " (" + _shot.Kind + ", " + _shot.Width + "x" + _shot.Height + ").");
            SetStage(Stage.EnteringPlayMode);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && _stage == Stage.EnteringPlayMode)
            {
                SetStage(Stage.WaitingForGame);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && _stage != Stage.Idle)
            {
                bool expected = _stage == Stage.LeavingPlayMode;
                CleanUpShot();
                if (!expected)
                {
                    Fail("Play mode ended before the shot was finished.");
                }

                StartNextShot();
            }
        }

        private static void OnEditorUpdate()
        {
            try
            {
                switch (_stage)
                {
                    case Stage.WaitingForGame:
                        WaitForGame();
                        break;
                    case Stage.Loading:
                        WaitForLoad();
                        break;
                    case Stage.Recording:
                        WaitForRecording();
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
            }
        }

        private static void WaitForGame()
        {
            _session ??= Object.FindFirstObjectByType<MapPlaySession>();
            if (_session == null || !_session.IsReadyForSaveLoad)
            {
                FailIfStuck("The MapPlay game did not become ready to load a save.");
                return;
            }

            Shot shot = _shot!;
            byte[] data = File.ReadAllBytes(Path.Combine(_savesDirectory, shot.Save));
            _load = _session.LoadSaveFileAsync("Capture save " + shot.Save, data);
            SetStage(Stage.Loading);
        }

        private static void WaitForLoad()
        {
            if (_load == null || !_load.IsCompleted)
            {
                FailIfStuck("The save did not finish loading.");
                return;
            }

            if (_load.IsFaulted || _session == null || !_session.HasWorld)
            {
                Fail("The save " + _shot!.Save + " could not be loaded; see the log above.");
                return;
            }

            Shot shot = _shot!;
            _session.SetSimulationPaused(true);
            _session.SetSimulationSpeed(shot.Speed);
            MapPlayShell shell = _session.GetComponent<MapPlayShell>();
            shell.SetHudVisible(shot.ShowHud);
            foreach (ShotPanel panel in shot.Panels)
            {
                OpenPanel(shell, panel);
            }

            Camera? camera = Camera.main;
            _camera = camera != null ? camera.GetComponent<MapCameraController>() : null;
            if (_camera == null || !_camera.IsConfigured)
            {
                Fail("The map camera is missing or not set up.");
                return;
            }

            ApplyView(0f);
            StartRecorder(shot);
        }

        private static void StartRecorder(Shot shot)
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = shot.FrameRate;
            settings.CapFrameRate = true;
            settings.ExitPlayMode = false;
            string outputBase = Path.Combine(_outputDirectory, shot.Name);
            var input = new GameViewInputSettings { OutputWidth = shot.Width, OutputHeight = shot.Height };
            if (shot.Kind == ShotKind.Screenshot)
            {
                var image = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                image.name = shot.Name;
                image.Enabled = true;
                image.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
                image.CaptureAlpha = false;
                image.imageInputSettings = input;
                image.OutputFile = outputBase;
                settings.AddRecorderSettings(image);
                settings.SetRecordModeToSingleFrame(shot.FrameCount);
            }
            else
            {
                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = shot.Name;
                movie.Enabled = true;
                movie.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                };
                movie.CaptureAlpha = false;
                movie.CaptureAudio = true;
                movie.ImageInputSettings = input;
                movie.OutputFile = outputBase;
                settings.AddRecorderSettings(movie);
                settings.SetRecordModeToFrameInterval(0, shot.FrameCount - 1);
            }

            DeleteOldOutput(shot);
            _recorderSettings = settings;
            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            if (!_recorder.StartRecording())
            {
                Fail("The Recorder did not start; see the log above.");
                return;
            }

            _firstRecordedFrame = Time.frameCount;
            _resumed = false;

            // Real-time effects (toasts, cash popups, goals) follow the recorded frames from here on.
            PresentationClock.BeginFixedStep(1f / shot.FrameRate);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            SetStage(Stage.Recording);
        }

        // Runs right before the map camera draws each frame: resumes the game on the first recorded frame and moves the
        // camera to the view of the current recorded frame.
        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_stage != Stage.Recording || _camera == null || camera.gameObject != _camera.gameObject || _shot == null)
            {
                return;
            }

            if (!_resumed && _session != null)
            {
                _session.SetSimulationPaused(false);
                _resumed = true;
            }

            int frame = Math.Max(0, Time.frameCount - _firstRecordedFrame);
            ApplyView(frame / (float)_shot.FrameRate);
        }

        private static void WaitForRecording()
        {
            if (_recorder != null && _recorder.IsRecording())
            {
                return;
            }

            Shot shot = _shot!;
            string output = Path.Combine(_outputDirectory, shot.Name + (shot.Kind == ShotKind.Screenshot ? ".png" : ".mp4"));
            if (File.Exists(output))
            {
                _written.Add(output);
                Debug.Log("Capture: wrote " + output + " (" + new FileInfo(output).Length + " bytes).");
            }
            else
            {
                _anyFailed = true;
                Debug.LogError("Capture: shot " + shot.Name + " finished without writing " + output + ".");
            }

            LeavePlayMode();
        }

        private static void ApplyView(float time)
        {
            CameraKey view = CameraPath.Evaluate(_shot!.Camera, time);
            _camera!.ShowView(view.TileX, view.TileY, view.Zoom);
        }

        private static void OpenPanel(MapPlayShell shell, ShotPanel panel)
        {
            switch (panel)
            {
                case ShotPanel.Routes:
                    shell.ToggleRoutesPanel();
                    break;
                case ShotPanel.Contracts:
                    shell.ToggleContractsPanel();
                    break;
                case ShotPanel.Vehicles:
                    shell.ToggleVehiclesPanel();
                    break;
                case ShotPanel.Unlocks:
                    shell.ToggleUnlocksPanel();
                    break;
                case ShotPanel.Finance:
                    shell.ToggleFinancePanel();
                    break;
                case ShotPanel.Achievements:
                    shell.ToggleAchievementsPanel();
                    break;
                case ShotPanel.Industries:
                    shell.SetIndustriesPanelOpen(true);
                    break;
                case ShotPanel.Towns:
                    shell.SetTownsPanelOpen(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(panel), panel, "Unknown panel.");
            }
        }

        private static void DeleteOldOutput(Shot shot)
        {
            string output = Path.Combine(_outputDirectory, shot.Name + (shot.Kind == ShotKind.Screenshot ? ".png" : ".mp4"));
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }

        private static void FailIfStuck(string problem)
        {
            if (EditorApplication.timeSinceStartup - _stageStartedAt > LoadTimeoutSeconds)
            {
                Fail(problem);
            }
        }

        private static void Fail(string problem)
        {
            _anyFailed = true;
            Debug.LogError("Capture: shot " + (_shot?.Name ?? "?") + " failed: " + problem);
            if (EditorApplication.isPlaying)
            {
                LeavePlayMode();
            }
        }

        private static void LeavePlayMode()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            PresentationClock.EndFixedStep();
            if (_recorder != null && _recorder.IsRecording())
            {
                _recorder.StopRecording();
            }

            SetStage(Stage.LeavingPlayMode);
            EditorApplication.ExitPlaymode();
        }

        private static void CleanUpShot()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            PresentationClock.EndFixedStep();
            _recorder = null;
            if (_recorderSettings != null)
            {
                Object.DestroyImmediate(_recorderSettings);
                _recorderSettings = null;
            }

            _session = null;
            _camera = null;
            _load = null;
        }

        private static void Finish()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            _stage = Stage.Idle;
            _shot = null;
            Debug.Log("Capture finished: " + _written.Count + " file(s) written" + (_anyFailed ? ", with failures." : "."));
            Action<bool>? onFinished = _onFinished;
            _onFinished = null;
            onFinished?.Invoke(!_anyFailed);
        }

        private static void SetStage(Stage stage)
        {
            _stage = stage;
            _stageStartedAt = EditorApplication.timeSinceStartup;
        }
    }
}
