#nullable enable
using System;
using System.IO;
using SekoyaTransit.Presentation.Audio;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SekoyaTransit.Presentation.Editor
{
    /// <summary>
    /// Creates the production MapPlay scene under _Project (not spike or art-pilot scenes).
    /// </summary>
    public static class MapPlaySceneBuilder
    {
        private const string SceneDirectory = "Assets/_Project/Scenes";
        private const string ScenePath = SceneDirectory + "/MapPlay.unity";
        private const string WebGlPreviewPath = "Builds/MapPlayWebGL";
        private const string WebGlDemoPath = "Builds/WebGLDemo";

        [MenuItem("Sekoya Transit/Build Map Play Scene")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log("MapPlay scene written to " + ScenePath);
        }

        /// <summary>
        /// Batchmode entry point: Unity -batchmode -quit -executeMethod SekoyaTransit.Presentation.Editor.MapPlaySceneBuilder.BuildFromBatch
        /// </summary>
        public static void BuildFromBatch()
        {
            Build();
        }

        /// <summary>
        /// Batchmode entry point that updates the existing MapPlay scene in place instead of rebuilding it: applies the
        /// audio import settings, assigns the content, audio (Issue #68) and map art assets, and the closest camera zoom
        /// (Issue #69).
        /// Unity -batchmode -quit -executeMethod SekoyaTransit.Presentation.Editor.MapPlaySceneBuilder.EnsureMapPlaySceneFromBatch
        /// </summary>
        public static void EnsureMapPlaySceneFromBatch()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("MapPlay scene is missing; run BuildFromBatch first.", ScenePath);
            }

            EnsureMapPlayContentAssetsAssigned();
        }

        /// <summary>
        /// Batchmode WebGL preview: start Unity with -buildTarget WebGL, then
        /// -executeMethod SekoyaTransit.Presentation.Editor.MapPlaySceneBuilder.BuildWebGLPreview
        /// </summary>
        public static void BuildWebGLPreview()
        {
            // Local previews use Development + uncompressed assets for faster iteration.
            // Release/shipping WebGL builds must not use this path.
            BuildWebGLPlayer(
                WebGlPreviewPath,
                WebGLCompressionFormat.Disabled,
                decompressionFallback: false,
                development: true,
                // Unity WebGL "Shorter Build Time" (enum name BuildTimes); avoids Runtime/Disk LTO.
                codeOptimization: "BuildTimes",
                label: "MapPlay WebGL Development preview");
        }

        /// <summary>
        /// Batchmode release WebGL demo for GitHub Pages (Issue #71, ADR 0034): start Unity with -buildTarget WebGL, then
        /// -executeMethod SekoyaTransit.Presentation.Editor.MapPlaySceneBuilder.BuildWebGLDemo
        /// The output folder is the GameCI -customBuildPath argument, or Builds/WebGLDemo when it is absent.
        /// </summary>
        public static void BuildWebGLDemo()
        {
            // GitHub Pages cannot send Content-Encoding headers, so the loader decompresses the gzip files itself.
            BuildWebGLPlayer(
                CommandLineValue("-customBuildPath") ?? WebGlDemoPath,
                WebGLCompressionFormat.Gzip,
                decompressionFallback: true,
                development: false,
                codeOptimization: "RuntimeSpeed",
                label: "MapPlay WebGL release demo");
        }

        private static void BuildWebGLPlayer(
            string outputPath,
            WebGLCompressionFormat compression,
            bool decompressionFallback,
            bool development,
            string codeOptimization,
            string label)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                throw new InvalidOperationException("Start Unity with -buildTarget WebGL before building the MapPlay player.");
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

            // Player settings are restored afterwards so a build never leaves the project changed.
            WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            bool previousDevelopment = EditorUserBuildSettings.development;
            string previousCodeOptimization = EditorUserBuildSettings.GetPlatformSettings(
                BuildPipeline.GetBuildTargetName(BuildTarget.WebGL),
                "CodeOptimization");
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
                EditorUserBuildSettings.development = development;
                EditorUserBuildSettings.SetPlatformSettings(
                    BuildPipeline.GetBuildTargetName(BuildTarget.WebGL),
                    "CodeOptimization",
                    codeOptimization);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = development ? BuildOptions.Development : BuildOptions.None,
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
                label + ": " + report.summary.result +
                ", size=" + (report.summary.totalSize / (1024 * 1024)) + " MB" +
                ", time=" + report.summary.totalTime +
                ", output=" + outputPath +
                ", development=" + development + ", compression=" + compression +
                ", codeOptimization=" + codeOptimization);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string? CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return null;
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
            AssignMapArt(session);
            AudioImportSettings.EnsureAll();
            AssignAudioAssets(sessionObject);

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
            AssignMapArt(session);
            ApplyCameraZoomLimits(roots);
            AudioImportSettings.EnsureAll();
            AssignAudioAssets(session.gameObject);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
        }

        private static void AssignAudioAssets(GameObject sessionObject)
        {
            MapPlayAudio audio = sessionObject.GetComponent<MapPlayAudio>();
            if (audio == null)
            {
                audio = sessionObject.AddComponent<MapPlayAudio>();
            }

            var so = new SerializedObject(audio);
            so.FindProperty("_music").objectReferenceValue = LoadClip("Music/LofiLoop.ogg");
            so.FindProperty("_click").objectReferenceValue = LoadClip("Sfx/click_001_padded.wav");
            so.FindProperty("_build").objectReferenceValue = LoadClip("Sfx/impactPlank_medium_000.ogg");
            so.FindProperty("_demolish").objectReferenceValue = LoadClip("Sfx/impactPlate_heavy_000.ogg");
            so.FindProperty("_cashIn").objectReferenceValue = LoadClip("Sfx/snd_purchase.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sessionObject);
        }

        private static void AssignMapArt(MapPlaySession session)
        {
            MapArtSet art = AssetDatabase.LoadAssetAtPath<MapArtSet>(MapArtBaker.ArtSetPath)
                ?? throw new FileNotFoundException(
                    "Map art is missing; run Sekoya Transit / Art / Bake Map Art.",
                    MapArtBaker.ArtSetPath);
            var so = new SerializedObject(session);
            so.FindProperty("_mapArt").objectReferenceValue = art;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The closest zoom moved from 2 to 4 with the map art (ADR 0032); existing scenes keep their serialized value.
        private static void ApplyCameraZoomLimits(GameObject[] roots)
        {
            for (int index = 0; index < roots.Length; index++)
            {
                MapCameraController? controller = roots[index].GetComponentInChildren<MapCameraController>();
                if (controller == null)
                {
                    continue;
                }

                var so = new SerializedObject(controller);
                so.FindProperty("_minOrthographicSize").floatValue = MapCameraController.ClosestZoomOrthographicSize;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }

        private static AudioClip LoadClip(string relativePath)
        {
            string path = AudioImportSettings.AudioRoot + relativePath;
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path)
                ?? throw new FileNotFoundException("Audio clip is missing (see CREDITS.md).", path);
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
            TextAsset? achievements = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Data/achievements.json");
            TextAsset? contracts = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Data/contracts.json");
            TextAsset? worldGen = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Data/worldgen.json");
            if (cargo == null || industries == null || towns == null || economy == null || vehicles == null ||
                achievements == null || contracts == null || worldGen == null)
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
            so.FindProperty("_achievementsData").objectReferenceValue = achievements;
            so.FindProperty("_contractsData").objectReferenceValue = contracts;
            so.FindProperty("_worldGenData").objectReferenceValue = worldGen;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
