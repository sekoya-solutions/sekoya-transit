#nullable enable
using System;
using System.IO;
using TransportTycoon.Presentation.Map;
using TransportTycoon.UI.Editor;
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
        private const string WebGlPreviewPath = "Builds/MapPlayWebGL";

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

            // WebGL cannot read Content/Data from disk; bake TextAsset refs into the scene first.
            EnsureMapPlayContentAssetsAssigned();
            // Localization string tables are Addressable; build for the active (WebGL) target.
            GameUiBootstrap.EnsureAll();
            GameUiBootstrap.BuildAddressablesForActiveTarget();

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
            MapPlaySession session = sessionObject.AddComponent<MapPlaySession>();
            AssignContentAssets(session);

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
            GameUiBootstrap.EnsureAll();
        }

        private static void EnsureMapPlayContentAssetsAssigned()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MapPlaySession? session = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                session = roots[index].GetComponent<MapPlaySession>();
                if (session != null)
                {
                    break;
                }
            }

            if (session == null)
            {
                throw new InvalidDataException("MapPlay scene has no MapPlaySession component.");
            }

            AssignContentAssets(session);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
        }

        private static void AssignContentAssets(MapPlaySession session)
        {
            TextAsset? cargo = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Content/Data/cargo.json");
            TextAsset? industries = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Data/industries.json");
            TextAsset? towns = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Content/Data/towns.json");
            TextAsset? economy = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Content/Data/economy.json");
            TextAsset? vehicles = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Data/vehicles.json");
            if (cargo == null || industries == null || towns == null || economy == null || vehicles == null)
            {
                throw new FileNotFoundException(
                    "Content TextAssets missing under Assets/_Project/Content/Data/.");
            }

            var so = new SerializedObject(session);
            so.FindProperty("_cargoData").objectReferenceValue = cargo;
            so.FindProperty("_industriesData").objectReferenceValue = industries;
            so.FindProperty("_townsData").objectReferenceValue = towns;
            so.FindProperty("_economyData").objectReferenceValue = economy;
            so.FindProperty("_vehiclesData").objectReferenceValue = vehicles;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
