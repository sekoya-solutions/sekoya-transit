#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TransportTycoon.UI.Shell;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Editor
{
    /// <summary>
    /// Creates production Localization, PanelSettings, and wires the MapPlay shell (not the WebGL spike).
    /// </summary>
    public static class GameUiBootstrap
    {
        private const string LocDir = "Assets/_Project/Localization";
        private const string UiDir = "Assets/_Project/UI";
        private const string GeneratedDir = UiDir + "/Generated";
        private const string PanelPath = GeneratedDir + "/MapPlayPanelSettings.asset";
        private const string ThemePath = UiDir + "/Theme/GameTheme.tss";
        private const string UxmlPath = UiDir + "/Shell/MapPlayShell.uxml";
        private const string ScenePath = "Assets/_Project/Scenes/MapPlay.unity";
        private const string LocalizationSettingsPath = LocDir + "/Localization Settings.asset";

        private static readonly (string Key, string En)[] ShellEntries =
        {
            (GameUiKeys.Cash, "Cash: {0}"),
            (
                GameUiKeys.Hint,
                "Blue = industry, red = town, purple = road station, teal = rail station. Primary industries fill stations; town deliveries are consumed (not stocked). Factories need inputs. V buys a truck at the selected road station (idle until you set a route). C buys a locomotive at the selected rail station (add wagons in the Vehicles list; auto-orders to reachable rail stations). L opens the vehicles list."),
            (GameUiKeys.HintToolRoad, "Tool: Road (B). Click a tile to build a road."),
            (GameUiKeys.HintToolDemolishRoad, "Tool: Demolish road (X). Click a road tile to remove it."),
            (GameUiKeys.HintToolRail, "Tool: Rail (R). Click a tile to build rail track."),
            (GameUiKeys.HintToolDemolishRail, "Tool: Demolish rail (Z). Click a rail tile to remove it."),
            (GameUiKeys.HintToolStation, "Tool: Station (N). Click a tile to build a road station."),
            (
                GameUiKeys.HintToolDemolishStation,
                "Tool: Demolish station (M). Click a station to remove it."),
            (GameUiKeys.HintToolRailStation, "Tool: Rail station (T). Click a tile to build a rail station."),
            (
                GameUiKeys.HintToolDemolishRailStation,
                "Tool: Demolish rail station (Y). Click a rail station to remove it."),
            (GameUiKeys.ToolSelect, "Select"),
            (GameUiKeys.ToolRoad, "Road"),
            (GameUiKeys.ToolDemolishRoad, "Demolish road"),
            (GameUiKeys.ToolRail, "Rail"),
            (GameUiKeys.ToolDemolishRail, "Demolish rail"),
            (GameUiKeys.ToolStation, "Station"),
            (GameUiKeys.ToolDemolishStation, "Demolish station"),
            (GameUiKeys.ToolRailStation, "Rail station"),
            (GameUiKeys.ToolDemolishRailStation, "Demolish rail station"),
            (GameUiKeys.VehiclesToggle, "Vehicles (L)"),
            (GameUiKeys.VehiclesTitle, "Vehicles"),
            (GameUiKeys.VehiclesBuy, "Buy at selected station"),
            (GameUiKeys.VehiclesBuyRoad, "Buy truck ({0})"),
            (GameUiKeys.VehiclesBuyRail, "Buy locomotive ({0})"),
            (GameUiKeys.VehiclesSell, "Sell"),
            (GameUiKeys.VehiclesAddWagon, "Add wagon ({0})"),
            (GameUiKeys.VehiclesSellWagon, "Sell wagon"),
            (GameUiKeys.VehiclesEmpty, "No vehicles yet. Select a road or rail station and Buy, or press V/C."),
            (GameUiKeys.VehiclesRow, "#{0} · {1} · {2} · Cargo {3}/{4} · Income {5} · Upkeep {6}"),
            (GameUiKeys.VehiclesRowRail, "#{0} · {1} · {2} · Wagons {3}/{4} · Cargo {5}/{6} · Income {7} · Upkeep {8}"),
            (GameUiKeys.VehiclesKindRoad, "Truck"),
            (GameUiKeys.VehiclesKindRail, "Train"),
            (GameUiKeys.VehiclesStatusParked, "Parked"),
            (GameUiKeys.VehiclesStatusMoving, "Moving"),
            (GameUiKeys.VehiclesStatusLoading, "Loading"),
            (GameUiKeys.VehiclesStatusStuck, "Stuck"),
            (GameUiKeys.OrdersTitle, "Route"),
            (
                GameUiKeys.OrdersHint,
                "Select a vehicle, then click stations on the map to add stops. Reorder or remove below, then Apply."),
            (GameUiKeys.OrdersNoneSelected, "Select a vehicle to edit its route."),
            (GameUiKeys.OrdersSelected, "Editing vehicle #{0}"),
            (GameUiKeys.OrdersEmpty, "No stops yet. Click stations on the map to append."),
            (GameUiKeys.OrdersStop, "#{0} Station {1}"),
            (GameUiKeys.OrdersUp, "Up"),
            (GameUiKeys.OrdersDown, "Down"),
            (GameUiKeys.OrdersRemove, "Remove"),
            (GameUiKeys.OrdersApply, "Apply route"),
            (GameUiKeys.OrdersApplied, "Route applied."),
            (GameUiKeys.OrdersRejected, "Could not apply route (draft kept)."),
            (GameUiKeys.IndustriesToggle, "Industries (I)"),
            (GameUiKeys.IndustriesTitle, "Industries"),
            (GameUiKeys.IndustriesEmpty, "No industries on this map."),
            (GameUiKeys.IndustriesRow, "{0} #{1} · L{2} · {3} · Produced {4} · Transported {5}"),
            (GameUiKeys.IndustryForest, "Forest"),
            (GameUiKeys.IndustrySawmill, "Sawmill"),
            (GameUiKeys.IndustryFurnitureFactory, "Furniture factory"),
            (GameUiKeys.IndustryWheatFarm, "Wheat farm"),
            (GameUiKeys.IndustryFlourMill, "Flour mill"),
            (GameUiKeys.IndustryBakery, "Bakery"),
            (GameUiKeys.IndustryIronMine, "Iron mine"),
            (GameUiKeys.IndustryIronFoundry, "Iron foundry"),
            (GameUiKeys.IndustryMachineShop, "Machine shop"),
            (GameUiKeys.IndustryLivestockRanch, "Livestock ranch"),
            (GameUiKeys.IndustryButcher, "Butcher"),
            (GameUiKeys.IndustryCannery, "Cannery"),
            (GameUiKeys.IndustrySheepFarm, "Sheep farm"),
            (GameUiKeys.IndustryTextileMill, "Textile mill"),
            (GameUiKeys.IndustryClothingWorkshop, "Clothing workshop"),
        };

        [MenuItem("Transport Tycoon/UI/Ensure Game UI Assets")]
        public static void EnsureFromMenu()
        {
            EnsureAll();
            Debug.Log("Game UI assets ensured (localization, panel, MapPlay shell wiring).");
        }

        /// <summary>
        /// Batchmode: Unity -batchmode -quit -executeMethod TransportTycoon.UI.Editor.GameUiBootstrap.EnsureFromBatch
        /// </summary>
        public static void EnsureFromBatch()
        {
            EnsureAll();
        }

        public static void EnsureAll()
        {
            EnsureFolders();
            EnsureLocalization();
            EnsurePanelSettings();
            EnsureMapPlayShellWired();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(LocDir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(GeneratedDir.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureLocalization()
        {
            LocalizationSettings settings = LoadOrCreateLocalizationSettings();
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            Locale en = FindOrCreateEnglishLocale();
            if (!LocalizationEditorSettings.GetLocales().Contains(en))
            {
                LocalizationEditorSettings.AddLocale(en);
            }

            StringTableCollection? collection = LocalizationEditorSettings.GetStringTableCollection(
                GameUiKeys.Table);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    GameUiKeys.Table,
                    LocDir,
                    new List<Locale> { en });
            }

            var enTable = (StringTable)collection.GetTable(en.Identifier);
            foreach ((string key, string enText) in ShellEntries)
            {
                StringTableEntry? existing = enTable.GetEntry(key);
                if (existing == null)
                {
                    enTable.AddEntry(key, enText);
                }
                else
                {
                    existing.Value = enText;
                }
            }

            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(settings);
        }

        private static LocalizationSettings LoadOrCreateLocalizationSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, LocalizationSettingsPath);
            return settings;
        }

        private static Locale FindOrCreateEnglishLocale()
        {
            foreach (Locale locale in LocalizationEditorSettings.GetLocales())
            {
                if (locale != null && locale.Identifier.Code == "en")
                {
                    return locale;
                }
            }

            string localePath = LocDir + "/Locale-en.asset";
            var existingAsset = AssetDatabase.LoadAssetAtPath<Locale>(localePath);
            if (existingAsset != null)
            {
                return existingAsset;
            }

            Locale en = Locale.CreateLocale(new LocaleIdentifier("en"));
            AssetDatabase.CreateAsset(en, localePath);
            return en;
        }

        private static void EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null)
            {
                panel.themeStyleSheet = theme;
            }

            EditorUtility.SetDirty(panel);
        }

        private static void EnsureMapPlayShellWired()
        {
            if (!File.Exists(ScenePath.Replace('/', Path.DirectorySeparatorChar)))
            {
                throw new FileNotFoundException(
                    "MapPlay scene is missing; run MapPlaySceneBuilder first.",
                    ScenePath);
            }

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (panel == null || uxml == null)
            {
                throw new InvalidOperationException(
                    "MapPlay panel settings or MapPlayShell.uxml is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MapPlayShell? shell = null;
            UIDocument? document = null;
            GameObject? sessionObject = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (root.GetComponent("MapPlaySession") != null)
                {
                    sessionObject = root;
                }

                shell ??= root.GetComponent<MapPlayShell>();
                document ??= root.GetComponent<UIDocument>();
            }

            if (sessionObject == null)
            {
                throw new InvalidDataException("MapPlay scene has no MapPlaySession component.");
            }

            if (shell == null)
            {
                shell = sessionObject.AddComponent<MapPlayShell>();
            }

            document = shell.GetComponent<UIDocument>();
            if (document == null)
            {
                document = sessionObject.AddComponent<UIDocument>();
            }

            document.panelSettings = panel;
            document.visualTreeAsset = uxml;
            EditorUtility.SetDirty(sessionObject);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Builds Addressables for the active target (required before WebGL player builds).
        /// </summary>
        public static void BuildAddressablesForActiveTarget()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings are missing.");
            }

            AddressableAssetSettings.BuildPlayerContent();
        }
    }
}
