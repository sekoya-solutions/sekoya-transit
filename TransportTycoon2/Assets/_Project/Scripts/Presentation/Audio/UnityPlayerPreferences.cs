#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Audio
{
    /// <summary>
    /// <see cref="IPlayerPreferences"/> backed by Unity <see cref="PlayerPrefs"/> (the registry on Windows, IndexedDB
    /// on WebGL).
    /// </summary>
    public sealed class UnityPlayerPreferences : IPlayerPreferences
    {
        /// <inheritdoc />
        public float GetFloat(string key, float fallback)
        {
            return PlayerPrefs.GetFloat(key, fallback);
        }

        /// <inheritdoc />
        public int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(key, fallback);
        }

        /// <inheritdoc />
        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }

        /// <inheritdoc />
        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }

        /// <inheritdoc />
        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
