#nullable enable
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace SekoyaTransit.UI.Tests.Shell
{
    /// <summary>
    /// The first-launch and stored-language rules (Issue #338): the player's stored choice wins, otherwise the system
    /// or browser language when it is French or English, otherwise English.
    /// </summary>
    public sealed class StartupLanguageTests
    {
        private Locale _en = null!;
        private Locale _fr = null!;
        private FakeLocalesProvider _locales = null!;

        [SetUp]
        public void SetUp()
        {
            _en = Locale.CreateLocale(new LocaleIdentifier(ShellLanguages.English));
            _fr = Locale.CreateLocale(new LocaleIdentifier(ShellLanguages.French));
            _locales = new FakeLocalesProvider(_en, _fr);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_en);
            Object.DestroyImmediate(_fr);
        }

        [TestCase("en")]
        [TestCase("fr")]
        public void StoredSelector_StoredShippedLanguage_SelectsIt(string code)
        {
            var selector = new StoredLanguageLocaleSelector(new MemoryLanguagePreference(code));

            Locale? locale = selector.GetStartupLocale(_locales);

            Assert.That(locale, Is.Not.Null);
            Assert.That(locale!.Identifier.Code, Is.EqualTo(code));
        }

        [TestCase(null)]
        [TestCase("de")]
        [TestCase("FR")]
        public void StoredSelector_NoneOrUnshipped_DefersToTheNextSelector(string? code)
        {
            var selector = new StoredLanguageLocaleSelector(new MemoryLanguagePreference(code));

            Assert.That(selector.GetStartupLocale(_locales), Is.Null);
        }

        [TestCase("fr-FR", SystemLanguage.French, "fr")]
        [TestCase("fr-CA", SystemLanguage.French, "fr")]
        [TestCase("fr-BE", SystemLanguage.Unknown, "fr")]
        [TestCase("en-US", SystemLanguage.English, "en")]
        [TestCase("en-GB", SystemLanguage.English, "en")]
        public void SystemSelector_FrenchOrEnglishSystem_SelectsThatLanguage(
            string culture,
            SystemLanguage language,
            string expected)
        {
            var selector = new FakeSystemLocaleSelector(CultureInfo.GetCultureInfo(culture), language);

            Locale? locale = selector.GetStartupLocale(_locales);

            Assert.That(locale, Is.Not.Null);
            Assert.That(locale!.Identifier.Code, Is.EqualTo(expected));
        }

        [Test]
        public void SystemSelector_InvariantCultureFrenchBrowser_SelectsFrench()
        {
            // WebGL can report no culture; the system language (the browser language) then decides.
            var selector = new FakeSystemLocaleSelector(CultureInfo.InvariantCulture, SystemLanguage.French);

            Locale? locale = selector.GetStartupLocale(_locales);

            Assert.That(locale, Is.Not.Null);
            Assert.That(locale!.Identifier.Code, Is.EqualTo(ShellLanguages.French));
        }

        [TestCase("de-DE", SystemLanguage.German)]
        [TestCase("ja-JP", SystemLanguage.Japanese)]
        public void SystemSelector_OtherLanguage_DefersToEnglish(string culture, SystemLanguage language)
        {
            var selector = new FakeSystemLocaleSelector(CultureInfo.GetCultureInfo(culture), language);

            Assert.That(selector.GetStartupLocale(_locales), Is.Null);
        }

        [Test]
        public void ShippedSelectors_Order_CommandLineThenStoredThenSystemThenEnglish()
        {
            LocalizationSettings? settings = UnityEditor.Localization.LocalizationEditorSettings.ActiveLocalizationSettings;
            Assert.That(settings, Is.Not.Null);

            List<IStartupLocaleSelector> selectors = settings!.GetStartupLocaleSelectors();

            Assert.That(selectors.Count, Is.EqualTo(4));
            Assert.That(selectors[0], Is.TypeOf<CommandLineLocaleSelector>());
            Assert.That(selectors[1], Is.TypeOf<StoredLanguageLocaleSelector>());
            Assert.That(selectors[2], Is.TypeOf<SystemLocaleSelector>());
            Assert.That(selectors[3], Is.TypeOf<SpecificLocaleSelector>());
            Assert.That(((SpecificLocaleSelector)selectors[3]).LocaleId.Code, Is.EqualTo(ShellLanguages.English));
        }

        private sealed class FakeLocalesProvider : ILocalesProvider
        {
            public FakeLocalesProvider(params Locale[] locales)
            {
                Locales = new List<Locale>(locales);
            }

            public List<Locale> Locales { get; }

            public Locale? GetLocale(LocaleIdentifier id)
            {
                foreach (Locale locale in Locales)
                {
                    if (locale.Identifier.Equals(id))
                    {
                        return locale;
                    }
                }

                return null;
            }

            public void AddLocale(Locale locale)
            {
                Locales.Add(locale);
            }

            public bool RemoveLocale(Locale locale)
            {
                return Locales.Remove(locale);
            }
        }

        private sealed class FakeSystemLocaleSelector : SystemLocaleSelector
        {
            private readonly CultureInfo _culture;
            private readonly SystemLanguage _language;

            public FakeSystemLocaleSelector(CultureInfo culture, SystemLanguage language)
            {
                _culture = culture;
                _language = language;
            }

            protected override CultureInfo GetSystemCulture()
            {
                return _culture;
            }

            protected override SystemLanguage GetApplicationSystemLanguage()
            {
                return _language;
            }
        }
    }
}
