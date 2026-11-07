#nullable enable
using System.Linq;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;

namespace SekoyaTransit.UI.Tests
{
    /// <summary>
    /// English and French are the shipped locales (ADR 0011, Issue #338): any other locale would have no GameUi table
    /// and show "No translation found" texts to players whose system language selects it.
    /// </summary>
    public sealed class ShippedLocalesTests
    {
        private static readonly string[] ShippedCodes = { ShellLanguages.English, ShellLanguages.French };

        [Test]
        public void Locales_Shipped_AreEnglishAndFrench()
        {
            string[] codes = LocalizationEditorSettings.GetLocales()
                .Select(locale => locale.Identifier.Code)
                .OrderBy(code => code, System.StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(ShippedCodes, codes);
            CollectionAssert.AreEquivalent(ShellLanguages.Shipped(), codes);
        }

        [Test]
        public void LocaleAssets_InProject_AreEnglishAndFrench()
        {
            // Issue #330: the Localization package registers every Locale asset it imports, so any other Locale file in
            // the project would come back into the shipped locales on the next fresh import.
            string[] codes = AssetDatabase.FindAssets("t:Locale")
                .Select(guid => AssetDatabase.LoadAssetAtPath<Locale>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(locale => locale != null)
                .Select(locale => locale.Identifier.Code)
                .OrderBy(code => code, System.StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(ShippedCodes, codes);
        }

        [Test]
        public void StringDatabase_Settings_UsesFallback()
        {
            LocalizationSettings? settings = LocalizationEditorSettings.ActiveLocalizationSettings;

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings!.GetStringDatabase().UseFallback, Is.True);
        }

        [Test]
        public void FrenchLocale_Fallback_IsEnglish()
        {
            Locale french = LocalizationEditorSettings.GetLocales().Single(
                locale => locale.Identifier.Code == ShellLanguages.French);

            FallbackLocale? fallback = french.Metadata.GetMetadata<FallbackLocale>();

            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback!.Locale, Is.Not.Null);
            Assert.That(fallback.Locale.Identifier.Code, Is.EqualTo(ShellLanguages.English));
        }
    }
}
