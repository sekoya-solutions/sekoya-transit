#nullable enable
using System;
using System.IO;
using TransportTycoon.Presentation.Map;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TransportTycoon.Presentation.Editor
{
    /// <summary>
    /// Creates the production MapPlay scene under _Project (not spike or art-pilot scenes).
    /// </summary>
    public static class MapPlaySceneBuilder
    {
        private const string SceneDirectory = "Assets/_Project/Scenes";
        private const string ScenePath = SceneDirectory + "/MapPlay.unity";
        private const string WebGlPreviewPath = "Builds/Issue40/MapPlayWebGL";

        [MenuItem("Transport Tycoon/Build Map Play Scene")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log("MapPlay scene written to " + ScenePath);
        }

        /// <summary>
        /// Batchmode entry point: Unity -batchmode -quit -executeMethod TransportTycoon.Presentation.Editor.MapPlaySceneBuilder.BuildFromBatch
        /// </summary>
        public static void BuildFromBatch()
        {
            Build();
        }

        /// <summary>
        /// Batchmode WebGL preview: start Unity with -buildTarget WebGL, then
        /// -executeMethod TransportTycoon.Presentation.Editor.MapPlaySceneBuilder.BuildWebGLPreview
        /// </summary>
        public static void BuildWebGLPreview()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                throw new InvalidOperationException("Start Unity with -buildTarget WebGL before building the MapPlay preview.");
            }

            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("MapPlay scene is missing; run BuildFromBatch first.", ScenePath);
            }

            // Local previews use Development + uncompressed assets for faster iteration.
            // Release/shipping WebGL builds must not use this path.
            WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            bool previousDevelopment = EditorUserBuildSettings.development;
            string previousCodeOptimization = EditorUserBuildSettings.GetPlatformSettings(
                BuildPipeline.GetBuildTargetName(BuildTarget.WebGL),
                "CodeOptimization");
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                EditorUserBuildSettings.development = true;
                // Unity WebGL "Shorter Build Time" (enum name BuildTimes); avoids Runtime/Disk LTO.
                EditorUserBuildSettings.SetPlatformSettings(
                    BuildPipeline.GetBuildTargetName(BuildTarget.WebGL),
                    "CodeOptimization",
                    "BuildTimes");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = WebGlPreviewPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
                EditorUserBuildSettings.development = previousDevelopment;
                if (!string.IsNullOrEmpty(previousCodeOptimization))
                {
                    EditorUserBuildSettings.SetPlatformSettings(
                        BuildPipeline.GetBuildTargetName(BuildTarget.WebGL),
                        "CodeOptimization",
                        previousCodeOptimization);
                }
            }

            Debug.Log(
                "MapPlay WebGL Development preview: " + report.summary.result +
                ", size=" + (report.summary.totalSize / (1024 * 1024)) + " MB" +
                ", time=" + report.summary.totalTime +
                ", options=Development, codeOptimization=BuildTimes");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                throw new InvalidDataException("Assets/_Project is missing.");
            }

            if (!AssetDatabase.IsValidFolder(SceneDirectory))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sessionObject = new GameObject("MapPlaySession");
            sessionObject.AddComponent<MapPlaySession>();

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<MapCameraController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }
    }
}
