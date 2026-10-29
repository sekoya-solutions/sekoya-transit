#nullable enable
using System;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Audio
{
    /// <summary>
    /// Keeps the player's <see cref="SoundSettings"/> in per-player preferences, never in a save file (Issue #68).
    /// Changes are kept in memory at once and written to storage only after they stop for
    /// <see cref="WriteDelaySeconds"/>, so dragging a slider does not write storage every frame.
    /// </summary>
    public sealed class SoundSettingsStore
    {
        /// <summary>
        /// Preference key of the music volume.
        /// </summary>
        public const string MusicVolumeKey = "sound.music_volume";

        /// <summary>
        /// Preference key of the effects volume.
        /// </summary>
        public const string EffectsVolumeKey = "sound.effects_volume";

        /// <summary>
        /// Preference key of the mute switch (1 when muted).
        /// </summary>
        public const string MutedKey = "sound.muted";

        /// <summary>
        /// Real seconds without a change before the settings are written to storage.
        /// </summary>
        public const float WriteDelaySeconds = 0.5f;

        private readonly IPlayerPreferences _preferences;
        private float? _lastChangeSeconds;

        /// <summary>
        /// Initializes the store over <paramref name="preferences"/>.
        /// </summary>
        public SoundSettingsStore(IPlayerPreferences preferences)
        {
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        }

        /// <summary>
        /// Gets whether a change waits to be written to storage.
        /// </summary>
        public bool HasPendingWrite => _lastChangeSeconds.HasValue;

        /// <summary>
        /// Reads the stored settings; a value never stored keeps its <see cref="SoundSettings.Default"/>.
        /// </summary>
        public SoundSettings Load()
        {
            SoundSettings defaults = SoundSettings.Default;
            return new SoundSettings(
                _preferences.GetFloat(MusicVolumeKey, defaults.MusicVolume),
                _preferences.GetFloat(EffectsVolumeKey, defaults.EffectsVolume),
                _preferences.GetInt(MutedKey, defaults.IsMuted ? 1 : 0) != 0);
        }

        /// <summary>
        /// Keeps <paramref name="settings"/> in memory and schedules the write.
        /// </summary>
        public void Change(SoundSettings settings, float nowSeconds)
        {
            _preferences.SetFloat(MusicVolumeKey, settings.MusicVolume);
            _preferences.SetFloat(EffectsVolumeKey, settings.EffectsVolume);
            _preferences.SetInt(MutedKey, settings.IsMuted ? 1 : 0);
            _lastChangeSeconds = nowSeconds;
        }

        /// <summary>
        /// Writes a pending change once no change came for <see cref="WriteDelaySeconds"/>.
        /// </summary>
        public void WriteIfDue(float nowSeconds)
        {
            if (_lastChangeSeconds.HasValue && nowSeconds - _lastChangeSeconds.Value >= WriteDelaySeconds)
            {
                Flush();
            }
        }

        /// <summary>
        /// Writes a pending change now (scene unload, quit).
        /// </summary>
        public void Flush()
        {
            if (!_lastChangeSeconds.HasValue)
            {
                return;
            }

            _lastChangeSeconds = null;
            _preferences.Save();
        }
    }
}
