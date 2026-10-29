#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using SekoyaTransit.UI.Shell;
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

namespace SekoyaTransit.UI.Editor
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
        private const string CargoIconsDir = IconSvgImportSettings.IconsRoot + "Cargo";
        private const string CargoFallbackIconPath = IconSvgImportSettings.IconsRoot + "Common/cargo_generic.svg";

        /// <summary>
        /// Generated cargo icon set wired into the MapPlay shell (Issue #182).
        /// </summary>
        public const string CargoIconSetPath = GeneratedDir + "/CargoIcons.asset";

        private static readonly (string Key, string En)[] ShellEntries =
        {
            (GameUiKeys.Cash, "{0}"),
            (GameUiKeys.CashTooltip, "Cash"),
            (GameUiKeys.Score, "Score: {0}"),
            (GameUiKeys.CalendarDate, "Date: {0}"),
            (GameUiKeys.SpeedX1, "Normal speed (x1)"),
            (GameUiKeys.SpeedX2, "Fast (x2)"),
            (GameUiKeys.SpeedX3, "Fastest (x3)"),
            (GameUiKeys.GameOverTitle, "Game Over"),
            (GameUiKeys.GameOverMessage, "Your company has gone bankrupt."),
            (
                GameUiKeys.Hint,
                "Blue = industry, red = town, purple = road station, teal = rail station. Primary industries fill stations; town deliveries are consumed (not stocked). Factories need inputs. V buys a starter truck at the selected road station (idle until you set a route). C buys a starter locomotive at the selected rail station (add wagons in Vehicles). L opens vehicles; U opens unlocks; I industries; T towns."),
            (GameUiKeys.HintToolRoad, "Tool: Road (B). Click a tile to build a road."),
            (GameUiKeys.HintToolRail, "Tool: Rail (R). Click a tile to build rail track."),
            (GameUiKeys.HintToolStation, "Tool: Road station (N). Click a tile to build a road station."),
            (GameUiKeys.HintToolRailStation, "Tool: Rail station (G). Click a tile to build a rail station."),
            (GameUiKeys.HintToolDemolish, "Tool: Delete (X). Click a tile to remove its station, or its road and rail."),
            (GameUiKeys.ToolSelect, "Select (Esc)"),
            (GameUiKeys.ToolRoad, "Road (B)"),
            (GameUiKeys.ToolRail, "Rail (R)"),
            (GameUiKeys.ToolStation, "Road station (N)"),
            (GameUiKeys.ToolRailStation, "Rail station (G)"),
            (GameUiKeys.ToolDemolish, "Delete (X)"),
            (GameUiKeys.VehiclesToggle, "Vehicles (L)"),
            (GameUiKeys.UnlocksToggle, "Unlocks (U)"),
            (GameUiKeys.VehiclesTitle, "Vehicles"),
            (GameUiKeys.UnlocksTitle, "Vehicle unlocks"),
            (GameUiKeys.UnlocksEmpty, "No vehicle types."),
            (GameUiKeys.VehiclesBuyHint, "Select a road or rail station to buy vehicles of its type and to edit routes."),
            (GameUiKeys.VehiclesBuyOption, "Buy {0} ({1})"),
            (GameUiKeys.UnlocksRowFuture, "{0} · from {1} · unlock {2}"),
            (GameUiKeys.UnlocksRowUnlockable, "{0} · unlock {1}"),
            (GameUiKeys.UnlocksRowUnlocked, "{0} · Unlocked"),
            (GameUiKeys.UnlocksAction, "Unlock ({0})"),
            (GameUiKeys.UnlocksActionUnlocked, "Unlocked"),
            (GameUiKeys.VehiclesSell, "Sell"),
            (GameUiKeys.VehiclesAddWagon, "Add wagon ({0})"),
            (GameUiKeys.VehiclesSellWagon, "Sell wagon"),
            (GameUiKeys.VehiclesEmpty, "No vehicles yet. Select a road or rail station and buy from the list, or press V/C."),
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
            (GameUiKeys.TownsToggle, "Towns (T)"),
            (GameUiKeys.TownsTitle, "Towns"),
            (GameUiKeys.TownsEmpty, "No towns on this map."),
            (GameUiKeys.TownsSummary, "{0} · Pop {1} · Growth {2}/{3}"),
            (GameUiKeys.TownsNeedLine, "{0} {1}/{2}"),
            (GameUiKeys.TownsPassengerLine, "Passengers · Generated {0} · Transported {1}"),
            (GameUiKeys.TownsMailLine, "Mail · Generated {0} · Transported {1}"),
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
            (GameUiKeys.VehicleTruckMk1, "Truck Mk1"),
            (GameUiKeys.VehicleTruckMk2, "Truck Mk2"),
            (GameUiKeys.VehicleTruckMk3, "Truck Mk3"),
            (GameUiKeys.VehicleTruckMk4, "Truck Mk4"),
            (GameUiKeys.VehicleTruckMk5, "Truck Mk5"),
            (GameUiKeys.VehicleLocoMk1, "Loco Mk1"),
            (GameUiKeys.VehicleLocoMk2, "Loco Mk2"),
            (GameUiKeys.VehicleLocoMk3, "Loco Mk3"),
            (GameUiKeys.VehicleLocoMk4, "Loco Mk4"),
            (GameUiKeys.VehicleRailWagon, "Rail wagon"),
            (GoalsUiKeys.FinanceToggle, "Finance (F)"),
            (GoalsUiKeys.FinanceTitle, "Finance"),
            (GoalsUiKeys.FinanceHeadingCurrent, "This year ({0})"),
            (GoalsUiKeys.FinanceHeadingPrevious, "Last year ({0})"),
            (GoalsUiKeys.FinanceTotalIncome, "Total income"),
            (GoalsUiKeys.FinanceTotalSpending, "Total spending"),
            (GoalsUiKeys.FinanceNetResult, "Net result"),
            (GoalsUiKeys.FinanceDeliveryIncome, "Deliveries"),
            (GoalsUiKeys.FinanceContractBonus, "Contract bonuses"),
            (GoalsUiKeys.FinanceVehicleSales, "Vehicle sales"),
            (GoalsUiKeys.FinanceRunningCosts, "Running costs"),
            (GoalsUiKeys.FinanceVehiclePurchases, "Vehicle purchases"),
            (GoalsUiKeys.FinanceVehicleUnlocks, "Vehicle unlocks"),
            (GoalsUiKeys.AchievementsToggle, "Achievements (H)"),
            (GoalsUiKeys.AchievementsTitle, "Achievements"),
            (GoalsUiKeys.AchievementsEmpty, "No achievements defined."),
            (GoalsUiKeys.AchievementsRow, "{0} · {1} / {2}"),
            (GoalsUiKeys.AchievementsRowCompleted, "{0} · {1} / {2} · Completed {3}"),
            (GoalsUiKeys.ContractsToggle, "Contracts (K)"),
            (GoalsUiKeys.ContractsTitle, "Contracts"),
            (GoalsUiKeys.ContractsEmpty, "No open contracts."),
            (GoalsUiKeys.ContractsRowTown, "{0} to town #{1} · {2} / {3} · by {4} · bonus {5}"),
            (GoalsUiKeys.ContractsRowIndustry, "{0} to {1} #{2} · {3} / {4} · by {5} · bonus {6}"),
            (GoalsUiKeys.ToastAchievement, "Achievement completed: {0}"),
            (GoalsUiKeys.ToastContractOfferedTown, "New contract: deliver {1} {0} to town #{2} by {3}"),
            (GoalsUiKeys.ToastContractOfferedIndustry, "New contract: deliver {1} {0} to {2} #{3} by {4}"),
            (GoalsUiKeys.ToastContractCompletedTown, "Contract completed: {1} {0} to town #{2}. Bonus {3}"),
            (GoalsUiKeys.ToastContractCompletedIndustry, "Contract completed: {1} {0} to {2} #{3}. Bonus {4}"),
            (GoalsUiKeys.ToastContractExpiredTown, "Contract expired: {1} {0} to town #{2}"),
            (GoalsUiKeys.ToastContractExpiredIndustry, "Contract expired: {1} {0} to {2} #{3}"),
            (GoalsUiKeys.GameOverScore, "Final score: {0}"),
            (GoalsUiKeys.GameOverDate, "Bankruptcy date: {0}"),
            (NewGameUiKeys.Title, "New game"),
            (NewGameUiKeys.SeedLabel, "Seed"),
            (NewGameUiKeys.Random, "Random"),
            (NewGameUiKeys.SeedError, "Enter a whole number from 0 to 18446744073709551615."),
            (NewGameUiKeys.SizeLabel, "Map size"),
            (NewGameUiKeys.SizeSmall, "Small 128×128"),
            (NewGameUiKeys.SizeMedium, "Medium 256×256"),
            (NewGameUiKeys.SizeLarge, "Large 512×512"),
            (NewGameUiKeys.Start, "Start"),
            (NewGameUiKeys.SeedInfo, "Seed {0} · {1}×{1}"),
            (NewGameUiKeys.GameOverNewGame, "New game"),
            (SaveLoadUiKeys.Toggle, "Save / Load"),
            (SaveLoadUiKeys.Title, "Save / Load"),
            (SaveLoadUiKeys.Close, "Close"),
            (SaveLoadUiKeys.SlotAutosave, "Autosave"),
            (SaveLoadUiKeys.SlotManual, "Slot {0}"),
            (SaveLoadUiKeys.SlotInfo, "{0} · Seed {1} · {2}×{2}"),
            (SaveLoadUiKeys.SlotEmpty, "Empty"),
            (SaveLoadUiKeys.SlotUnreadable, "Unreadable save"),
            (SaveLoadUiKeys.Save, "Save"),
            (SaveLoadUiKeys.Load, "Load"),
            (SaveLoadUiKeys.ConfirmText, "Unsaved progress will be lost. Load anyway?"),
            (SaveLoadUiKeys.ConfirmYes, "Load anyway"),
            (SaveLoadUiKeys.ConfirmNo, "Cancel"),
            (SaveLoadUiKeys.StatusSaved, "Game saved."),
            (SaveLoadUiKeys.StatusSaveFailed, "The game could not be saved."),
            (SaveLoadUiKeys.StatusLoadFailed, "The game could not be loaded."),
            (SaveLoadUiKeys.StatusNotSupported, "This save is damaged or was made by a newer version of the game."),
            (SaveLoadUiKeys.StatusAutosaveFailed, "The autosave failed; the game continues."),
            (SaveLoadUiKeys.Continue, "Continue"),
            (SaveLoadUiKeys.LoadGame, "Load game"),
            (SoundUiKeys.Toggle, "Sound"),
            (SoundUiKeys.Title, "Sound"),
            (SoundUiKeys.Music, "Music volume"),
            (SoundUiKeys.Effects, "Effects volume"),
            (SoundUiKeys.Mute, "Mute all sound"),
            (SoundUiKeys.Close, "Close"),
            ("achievement.town_1k", "Grow a town to 1,000 inhabitants"),
            ("achievement.town_5k", "Grow a town to 5,000 inhabitants"),
            ("achievement.town_10k", "Grow a town to 10,000 inhabitants"),
            ("achievement.cash_1m", "Hold 1,000,000 in cash"),
            ("achievement.cash_10m", "Hold 10,000,000 in cash"),
            ("achievement.vehicles_10", "Own 10 vehicles"),
            ("achievement.deliver_bread_100", "Deliver 100 bread to towns"),
        };

        [MenuItem("Sekoya Transit/UI/Ensure Game UI Assets")]
        public static void EnsureFromMenu()
        {
            EnsureAll();
            Debug.Log("Game UI assets ensured (localization, panel, MapPlay shell wiring).");
        }

        /// <summary>
        /// Batchmode: Unity -batchmode -quit -executeMethod SekoyaTransit.UI.Editor.GameUiBootstrap.EnsureFromBatch
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
            EnsureIconImports();
            EnsureCargoIconSet();
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

        private static void EnsureIconImports()
        {
            string root = IconSvgImportSettings.IconsRoot.TrimEnd('/');
            foreach (string file in Directory.GetFiles(root, "*.svg", SearchOption.AllDirectories))
            {
                IconSvgImportSettings.EnsureVectorImage(file.Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        // One entry per Art/Icons/Cargo/<cargo id>.svg, sorted by id; the generic crate is the fallback.
        private static void EnsureCargoIconSet()
        {
            VectorImage fallback = AssetDatabase.LoadAssetAtPath<VectorImage>(CargoFallbackIconPath)
                ?? throw new FileNotFoundException("Generic cargo icon is missing.", CargoFallbackIconPath);
            string[] files = Directory.GetFiles(CargoIconsDir, "*.svg", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            var icons = new List<KeyValuePair<string, VectorImage>>(files.Length);
            foreach (string file in files)
            {
                string path = file.Replace(Path.DirectorySeparatorChar, '/');
                VectorImage icon = AssetDatabase.LoadAssetAtPath<VectorImage>(path)
                    ?? throw new InvalidDataException("Cargo icon did not import as a vector image: " + path);
                icons.Add(new KeyValuePair<string, VectorImage>(Path.GetFileNameWithoutExtension(path), icon));
            }

            var set = AssetDatabase.LoadAssetAtPath<CargoIconSet>(CargoIconSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CargoIconSet>();
                AssetDatabase.CreateAsset(set, CargoIconSetPath);
            }

            set.SetIcons(icons, fallback);
            EditorUtility.SetDirty(set);
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
            var shellObject = new SerializedObject(shell);
            shellObject.FindProperty("_cargoIcons").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CargoIconSet>(CargoIconSetPath);
            shellObject.ApplyModifiedPropertiesWithoutUndo();
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
