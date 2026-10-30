#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Tests.Shell
{
    /// <summary>
    /// Checks the shipped icon assets of Issue #182: cargo icons, button icons in USS, and the tooltip texts.
    /// </summary>
    public sealed class ShellIconAssetsTests
    {
        private const string CargoIconSetPath = "Assets/_Project/UI/Generated/CargoIcons.asset";
        private const string CargoIconsDir = "Assets/_Project/Art/Icons/Cargo";
        private const string CargoDataPath = "Assets/_Project/Content/Data/cargo.json";
        private const string UssPath = "Assets/_Project/UI/Shell/MapPlayShell.uss";
        private const string UxmlPath = "Assets/_Project/UI/Shell/MapPlayShell.uxml";

        private static readonly string[] IconButtonNames =
        {
            "tool-select",
            "tool-road",
            "tool-rail",
            "tool-station",
            "tool-rail-station",
            "tool-demolish",
            "speed-x1",
            "speed-x2",
            "speed-x3",
            "vehicles-toggle",
            "unlocks-toggle",
            "industries-toggle",
            "towns-toggle",
            "finance-toggle",
            "achievements-toggle",
            "contracts-toggle",
            "save-load-toggle",
            "sound-toggle",
        };

        private static readonly string[] TooltipKeys =
        {
            GameUiKeys.ToolLabelKey(BuildToolId.Select),
            GameUiKeys.ToolLabelKey(BuildToolId.Road),
            GameUiKeys.ToolLabelKey(BuildToolId.Rail),
            GameUiKeys.ToolLabelKey(BuildToolId.Station),
            GameUiKeys.ToolLabelKey(BuildToolId.RailStation),
            GameUiKeys.ToolLabelKey(BuildToolId.Demolish),
            GameUiKeys.SpeedX1,
            GameUiKeys.SpeedX2,
            GameUiKeys.SpeedX3,
            GameUiKeys.VehiclesToggle,
            GameUiKeys.UnlocksToggle,
            GameUiKeys.IndustriesToggle,
            GameUiKeys.TownsToggle,
            GoalsUiKeys.FinanceToggle,
            GoalsUiKeys.AchievementsToggle,
            GoalsUiKeys.AchievementsClose,
            GoalsUiKeys.ContractsToggle,
            SaveLoadUiKeys.Toggle,
            SoundUiKeys.Toggle,
            GameUiKeys.CashTooltip,
        };

        [Test]
        public void CargoIconSet_EveryShippedCargo_HasItsOwnIcon()
        {
            CargoIconSet set = LoadCargoIconSet();
            foreach (string cargoId in LoadShippedCargoIds())
            {
                Assert.That(set.HasOwnIcon(cargoId), Is.True, "No icon for cargo " + cargoId);
                Assert.That(set.GetIcon(cargoId), Is.Not.Null.And.Not.SameAs(set.Fallback), cargoId);
            }
        }

        [Test]
        public void CargoIconSet_UnknownCargo_ResolvesToTheGenericIcon()
        {
            CargoIconSet set = LoadCargoIconSet();

            Assert.That(set.Fallback, Is.Not.Null);
            Assert.That(set.HasOwnIcon("not_a_cargo"), Is.False);
            Assert.That(set.GetIcon("not_a_cargo"), Is.SameAs(set.Fallback));
        }

        [Test]
        public void CargoIconSet_ListsEveryCargoIconFile()
        {
            var expected = new List<string>();
            foreach (string file in Directory.GetFiles(CargoIconsDir, "*.svg"))
            {
                expected.Add(Path.GetFileNameWithoutExtension(file));
            }

            Assert.That(LoadCargoIconSet().CargoIds, Is.EquivalentTo(expected));
        }

        [Test]
        public void Uss_EveryIconButton_HasAnImportedVectorIcon()
        {
            string uss = File.ReadAllText(UssPath);
            string uxml = File.ReadAllText(UxmlPath);
            foreach (string name in IconButtonNames)
            {
                Assert.That(uxml, Does.Contain("name=\"" + name + "\""), name + " is not in the UXML");
                Match rule = Regex.Match(
                    uss,
                    "#" + Regex.Escape(name) + @"\s*\{\s*background-image:\s*url\(""([^""]+)""\)");
                Assert.That(rule.Success, Is.True, "No icon rule for #" + name);
                AssertVectorImage(rule.Groups[1].Value);
            }

            Match cashIcon = Regex.Match(uss, @"\.shell-cash-icon\s*\{[^}]*background-image:\s*url\(""([^""]+)""\)");
            Assert.That(cashIcon.Success, Is.True, "No dollar icon rule");
            AssertVectorImage(cashIcon.Groups[1].Value);
        }

        [TestCase(@"\.shell-window__close\s*\{[^}]*background-image:\s*url\(""([^""]+)""\)")]
        [TestCase(@"\.achievements-milestone--bronze \.achievements-milestone__trophy\s*\{\s*background-image:\s*url\(""([^""]+)""\)")]
        [TestCase(@"\.achievements-milestone--silver \.achievements-milestone__trophy\s*\{\s*background-image:\s*url\(""([^""]+)""\)")]
        [TestCase(@"\.achievements-milestone--gold \.achievements-milestone__trophy\s*\{\s*background-image:\s*url\(""([^""]+)""\)")]
        public void Uss_AchievementsWindowIcon_IsAnImportedVectorImage(string rulePattern)
        {
            Match rule = Regex.Match(File.ReadAllText(UssPath), rulePattern);

            Assert.That(rule.Success, Is.True, "No icon rule matching " + rulePattern);
            AssertVectorImage(rule.Groups[1].Value);
        }

        [Test]
        public void Localization_EveryIconButtonTooltip_HasEnglishText()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameUiKeys.Table);
            Assert.That(collection, Is.Not.Null);
            var table = (StringTable)collection.GetTable(new LocaleIdentifier("en"));
            Assert.That(table, Is.Not.Null);
            foreach (string key in TooltipKeys)
            {
                StringTableEntry? entry = table.GetEntry(key);
                Assert.That(entry?.Value, Is.Not.Null.And.Not.Empty, key);
            }
        }

        private static void AssertVectorImage(string projectPath)
        {
            string assetPath = projectPath.TrimStart('/');
            Assert.That(
                AssetDatabase.LoadAssetAtPath<VectorImage>(assetPath),
                Is.Not.Null,
                assetPath + " is not imported as a vector image");
        }

        private static CargoIconSet LoadCargoIconSet()
        {
            CargoIconSet? set = AssetDatabase.LoadAssetAtPath<CargoIconSet>(CargoIconSetPath);
            Assert.That(set, Is.Not.Null, "Run Sekoya Transit/UI/Ensure Game UI Assets");
            return set!;
        }

        private static IEnumerable<string> LoadShippedCargoIds()
        {
            CargoFile? file = JsonUtility.FromJson<CargoFile>(File.ReadAllText(CargoDataPath));
            Assert.That(file?.cargo, Is.Not.Null.And.Not.Empty);
            foreach (CargoEntry entry in file!.cargo!)
            {
                yield return entry.id ?? throw new InvalidDataException("A cargo entry has no id.");
            }
        }

        [Serializable]
        private sealed class CargoFile
        {
            public CargoEntry[]? cargo;
        }

        [Serializable]
        private sealed class CargoEntry
        {
            public string? id;
        }
    }
}
