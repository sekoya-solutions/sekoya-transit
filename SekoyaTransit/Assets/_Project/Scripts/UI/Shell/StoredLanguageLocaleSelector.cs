#nullable enable
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Startup locale selector that picks the language the player chose in the start menu (Issue #338). It returns
    /// null, so the next selector decides (the system language, then English), when the player never chose a language
    /// or the stored code is not a shipped locale. Unlike the package's PlayerPrefLocaleSelector it never stores the
    /// locale picked at startup, so a player who never chose keeps following the system language.
    /// </summary>
    [Serializable]
    public sealed class StoredLanguageLocaleSelector : IStartupLocaleSelector
    {
        [NonSerialized]
        private readonly ILanguagePreference? _preference;

        /// <summary>
        /// Initializes a selector over the PlayerPrefs preference (used by the Localization settings asset).
        /// </summary>
        public StoredLanguageLocaleSelector()
        {
        }

        /// <summary>
        /// Initializes a selector over <paramref name="preference"/>.
        /// </summary>
        public StoredLanguageLocaleSelector(ILanguagePreference preference)
        {
            _preference = preference ?? throw new ArgumentNullException(nameof(preference));
        }

        /// <inheritdoc />
        public Locale? GetStartupLocale(ILocalesProvider availableLocales)
        {
            if (availableLocales == null)
            {
                throw new ArgumentNullException(nameof(availableLocales));
            }

            string? code = (_preference ?? new PlayerPrefsLanguagePreference()).Load();
            if (!ShellLanguages.IsShipped(code))
            {
                return null;
            }

            return availableLocales.GetLocale(new LocaleIdentifier(code!));
        }
    }
}
