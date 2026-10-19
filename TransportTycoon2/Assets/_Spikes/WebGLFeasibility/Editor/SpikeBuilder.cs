#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace TransportTycoon.Spikes.WebGL.Editor
{
    // Batch-mode entry points that generate the spike assets and scene, then build the players.
    public static class SpikeBuilder
    {
        private const string Root = "Assets/_Spikes/WebGLFeasibility";
        private const string LocDir = Root + "/Localization";
        private const string ScenePath = Root + "/Scenes/SpikeScene.unity";
        private const string MaterialPath = Root + "/Generated/SpikeSprite.mat";
        private const string PanelPath = Root + "/Generated/SpikePanelSettings.asset";

        public static void CreateLocalization()
        {
            Directory.CreateDirectory(LocDir);

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, LocDir + "/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            Locale en = Locale.CreateLocale(new LocaleIdentifier("en"));
            Locale fr = Locale.CreateLocale(new LocaleIdentifier("fr"));
            AssetDatabase.CreateAsset(en, LocDir + "/Locale-en.asset");
            AssetDatabase.CreateAsset(fr, LocDir + "/Locale-fr.asset");
            LocalizationEditorSettings.AddLocale(en);
            LocalizationEditorSettings.AddLocale(fr);

            StringTableCollection collection = LocalizationEditorSettings.CreateStringTableCollection(
                "SpikeStrings", LocDir, new List<Locale> { en, fr });

            var entries = new (string Key, string En, string Fr)[]
            {
                ("title", "Transport Tycoon 2 - WebGL spike", "Transport Tycoon 2 - spike WebGL"),
                ("renderer", "Switch renderer", "Changer de rendu"),
                ("vehiclesMinus", "Vehicles -1000", "Vehicules -1000"),
                ("vehiclesPlus", "Vehicles +1000", "Vehicules +1000"),
                ("benchmark", "Benchmark", "Banc d'essai"),
                ("saveTest", "Save test", "Test de sauvegarde"),
                ("verifySaves", "Verify saves", "Verifier sauvegardes"),
                ("english", "English", "Anglais"),
                ("french", "French", "Francais"),
            };

            var enTable = (StringTable)collection.GetTable(en.Identifier);
            var frTable = (StringTable)collection.GetTable(fr.Identifier);
            foreach ((string key, string enText, string frText) in entries)
            {
                enTable.AddEntry(key, enText);
                frTable.AddEntry(key, frText);
            }

            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(frTable);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log("Spike localization assets created");
        }

        public static void CreateScene()
        {
            Directory.CreateDirectory(Root + "/Generated");
            Directory.CreateDirectory(Root + "/Scenes");

            Shader? shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                throw new InvalidOperationException("2D sprite shader not found");
            }

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            string[] themes = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            Debug.Log("ThemeStyleSheet assets found: " + themes.Length);
            if (themes.Length > 0)
            {
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themes[0]));
            }

            AssetDatabase.CreateAsset(panel, PanelPath);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<SpikeCameraRig>();

            var appObject = new GameObject("SpikeApp");
            var document = appObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var app = appObject.AddComponent<SpikeApp>();
            appObject.AddComponent<SpikeUi>();

            var serialized = new SerializedObject(app);
            serialized.FindProperty("spriteMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Spike scene created");
        }

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Spike/Windows/SpikeWindows.exe");
        }

        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            Build(BuildTarget.WebGL, "Builds/Spike/WebGL");
        }

        private static void Build(BuildTarget target, string output)
        {
            PlayerSettings.enableFrameTimingStats = true;
            // Addressables content is platform-specific: switch before building it.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"Build {target}: {report.summary.result} size={report.summary.totalSize / (1024 * 1024)}MB time={report.summary.totalTime}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
