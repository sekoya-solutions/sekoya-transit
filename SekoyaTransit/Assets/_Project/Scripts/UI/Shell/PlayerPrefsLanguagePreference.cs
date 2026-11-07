#nullable enable
using System;
using UnityEngine;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// <see cref="ILanguagePreference"/> stored with Unity <see cref="PlayerPrefs"/>: the registry on Windows and the
    /// browser's IndexedDB on WebGL, like the sound settings.
    /// </summary>
    public sealed class PlayerPrefsLanguagePreference : ILanguagePreference
    {
        /// <summary>
        /// The PlayerPrefs key of the stored locale code.
        /// </summary>
        public const string Key = "settings.language";

        /// <inheritdoc />
        public string? Load()
        {
            string code = PlayerPrefs.GetString(Key, string.Empty);
            return code.Length == 0 ? null : code;
        }

        /// <inheritdoc />
        public void Store(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("Code cannot be null or empty.", nameof(code));
            }

            PlayerPrefs.SetString(Key, code);
            // WebGL writes PlayerPrefs to IndexedDB only on Save; closing the tab does not save them.
            PlayerPrefs.Save();
        }
    }
}
