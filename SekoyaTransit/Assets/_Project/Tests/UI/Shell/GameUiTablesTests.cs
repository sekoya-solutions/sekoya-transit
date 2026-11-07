#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SekoyaTransit.UI.Tests.Shell
{
    /// <summary>
    /// The shipped GameUi string tables (Issue #338): every key has an English and a French text, both with the same
    /// format placeholders, and the French texts follow French typography.
    /// </summary>
    public sealed class GameUiTablesTests
    {
        private const string CargoContentPath = "Assets/_Project/Content/Data/cargo.json";
        private static readonly Regex Placeholder = new Regex(@"\{\d+\}", RegexOptions.CultureInvariant);

        private StringTableCollection _collection = null!;
        private StringTable _english = null!;
        private StringTable _french = null!;

        [SetUp]
        public void SetUp()
        {
            StringTableCollection? collection = LocalizationEditorSettings.GetStringTableCollection(GameUiKeys.Table);
            Assert.That(collection, Is.Not.Null, "GameUi string table collection");
            _collection = collection!;
            _english = (StringTable)_collection.GetTable(ShellLanguages.English);
            _french = (StringTable)_collection.GetTable(ShellLanguages.French);
            Assert.That(_english, Is.Not.Null, "English GameUi table");
            Assert.That(_french, Is.Not.Null, "French GameUi table");
        }

        [Test]
        public void Tables_EveryKey_HasEnglishAndFrenchText()
        {
            var missing = new List<string>();
            foreach (SharedTableData.SharedTableEntry entry in _collection.SharedData.Entries)
            {
                if (string.IsNullOrWhiteSpace(_english.GetEntry(entry.Key)?.Value))
                {
                    missing.Add("en " + entry.Key);
                }

                if (string.IsNullOrWhiteSpace(_french.GetEntry(entry.Key)?.Value))
                {
                    missing.Add("fr " + entry.Key);
                }
            }

            Assert.That(_collection.SharedData.Entries.Count, Is.GreaterThan(0));
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void Tables_EveryKey_HasTheSamePlaceholdersInBothLanguages()
        {
            var mismatches = new List<string>();
            foreach (SharedTableData.SharedTableEntry entry in _collection.SharedData.Entries)
            {
                string english = _english.GetEntry(entry.Key)?.Value ?? string.Empty;
                string french = _french.GetEntry(entry.Key)?.Value ?? string.Empty;
                if (!Placeholders(english).SequenceEqual(Placeholders(french)))
                {
                    mismatches.Add(entry.Key + ": \"" + english + "\" / \"" + french + "\"");
                }
            }

            Assert.That(mismatches, Is.Empty);
        }

        [Test]
        public void FrenchTable_SignsKeptAttached_UseNonBreakingSpaces()
        {
            var plainSpaces = new List<string>();
            foreach (SharedTableData.SharedTableEntry entry in _collection.SharedData.Entries)
            {
                string french = _french.GetEntry(entry.Key)?.Value ?? string.Empty;
                if (Regex.IsMatch(french, @" [:;!?%»]|« "))
                {
                    plainSpaces.Add(entry.Key + ": " + french);
                }
            }

            Assert.That(plainSpaces, Is.Empty);
        }

        [Test]
        public void LanguageNames_AreTheSameInBothTables()
        {
            Assert.That(_english.GetEntry(ShellLanguages.EnglishNameKey)?.Value, Is.EqualTo("English"));
            Assert.That(_french.GetEntry(ShellLanguages.EnglishNameKey)?.Value, Is.EqualTo("English"));
            Assert.That(_english.GetEntry(ShellLanguages.FrenchNameKey)?.Value, Is.EqualTo("Français"));
            Assert.That(_french.GetEntry(ShellLanguages.FrenchNameKey)?.Value, Is.EqualTo("Français"));
        }

        [Test]
        public void CargoContent_EveryCargo_IsNamedByItsShippedCargoKey()
        {
            CargoFile? file = JsonUtility.FromJson<CargoFile>(File.ReadAllText(CargoContentPath));
            Assert.That(file?.cargo, Is.Not.Null.And.Not.Empty);

            foreach (CargoRow row in file!.cargo!)
            {
                string key = GameUiKeys.CargoNameKey(row.id);
                Assert.That(row.nameKey, Is.EqualTo(key), row.id);
                CollectionAssert.Contains(GameUiKeys.CargoNameKeys, key);
                Assert.That(_collection.SharedData.Contains(key), Is.True, key);
            }

            Assert.That(GameUiKeys.CargoNameKeys.Length, Is.EqualTo(file.cargo!.Length));
        }

        private static IEnumerable<string> Placeholders(string text)
        {
            return Placeholder.Matches(text)
                .Cast<Match>()
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
        }

        // JsonUtility needs public fields named like the JSON properties.
#pragma warning disable CS0649, SA1307, SA1401
        [Serializable]
        private sealed class CargoFile
        {
            public CargoRow[]? cargo;
        }

        [Serializable]
        private sealed class CargoRow
        {
            public string id = string.Empty;
            public string nameKey = string.Empty;
        }
#pragma warning restore CS0649, SA1307, SA1401
    }
}
