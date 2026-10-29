#nullable enable
using System.Linq;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;

namespace SekoyaTransit.UI.Tests
{
    /// <summary>
    /// English is the only shipped locale in V1 (ADR 0011): a locale without a GameUi table would show
    /// "No translation found" texts to players whose system language selects it.
    /// </summary>
    public sealed class ShippedLocalesTests
    {
        [Test]
        public void Locales_Shipped_AreEnglishOnly()
        {
            string[] codes = LocalizationEditorSettings.GetLocales().Select(locale => locale.Identifier.Code).ToArray();

            CollectionAssert.AreEqual(new[] { "en" }, codes);
        }

        [Test]
        public void StringDatabase_Settings_UsesFallback()
        {
            LocalizationSettings? settings = LocalizationEditorSettings.ActiveLocalizationSettings;

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings!.GetStringDatabase().UseFallback, Is.True);
        }
    }
}
