#nullable enable

namespace TransportTycoon.Presentation.Audio
{
    /// <summary>
    /// Per-player key-value preferences; <see cref="UnityPlayerPreferences"/> stores them with Unity PlayerPrefs, and
    /// tests use an in-memory fake.
    /// </summary>
    public interface IPlayerPreferences
    {
        /// <summary>
        /// Gets the stored float, or <paramref name="fallback"/> when none is stored.
        /// </summary>
        float GetFloat(string key, float fallback);

        /// <summary>
        /// Gets the stored integer, or <paramref name="fallback"/> when none is stored.
        /// </summary>
        int GetInt(string key, int fallback);

        /// <summary>
        /// Stores a float in memory until <see cref="Save"/>.
        /// </summary>
        void SetFloat(string key, float value);

        /// <summary>
        /// Stores an integer in memory until <see cref="Save"/>.
        /// </summary>
        void SetInt(string key, int value);

        /// <summary>
        /// Writes the stored values to persistent storage.
        /// </summary>
        void Save();
    }
}
