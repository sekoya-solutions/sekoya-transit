#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The player's sound preferences (Issue #68): music and effects volumes from 0 to 1 and a mute switch. A
    /// per-player preference, never part of a save file. Volumes are clamped on construction.
    /// </summary>
    public readonly struct SoundSettings : IEquatable<SoundSettings>
    {
        /// <summary>
        /// Volume of music the first time the game runs.
        /// </summary>
        public const float DefaultMusicVolume = 0.5f;

        /// <summary>
        /// Volume of sound effects the first time the game runs.
        /// </summary>
        public const float DefaultEffectsVolume = 0.8f;

        /// <summary>
        /// Initializes the settings; volumes outside 0 to 1 (or not a number) are clamped.
        /// </summary>
        public SoundSettings(float musicVolume, float effectsVolume, bool isMuted)
        {
            MusicVolume = Clamp01(musicVolume);
            EffectsVolume = Clamp01(effectsVolume);
            IsMuted = isMuted;
        }

        /// <summary>
        /// Gets the settings used the first time the game runs.
        /// </summary>
        public static SoundSettings Default => new SoundSettings(DefaultMusicVolume, DefaultEffectsVolume, false);

        /// <summary>
        /// Gets the music volume chosen by the player, from 0 to 1.
        /// </summary>
        public float MusicVolume { get; }

        /// <summary>
        /// Gets the sound effects volume chosen by the player, from 0 to 1.
        /// </summary>
        public float EffectsVolume { get; }

        /// <summary>
        /// Gets whether all sound is muted; the chosen volumes are kept for when sound is turned back on.
        /// </summary>
        public bool IsMuted { get; }

        /// <summary>
        /// Gets the volume music plays at: 0 when muted.
        /// </summary>
        public float EffectiveMusicVolume => IsMuted ? 0f : MusicVolume;

        /// <summary>
        /// Gets the volume sound effects play at: 0 when muted.
        /// </summary>
        public float EffectiveEffectsVolume => IsMuted ? 0f : EffectsVolume;

        /// <summary>
        /// Returns a copy with another music volume.
        /// </summary>
        public SoundSettings WithMusicVolume(float volume)
        {
            return new SoundSettings(volume, EffectsVolume, IsMuted);
        }

        /// <summary>
        /// Returns a copy with another effects volume.
        /// </summary>
        public SoundSettings WithEffectsVolume(float volume)
        {
            return new SoundSettings(MusicVolume, volume, IsMuted);
        }

        /// <summary>
        /// Returns a copy with sound muted or not.
        /// </summary>
        public SoundSettings WithMuted(bool isMuted)
        {
            return new SoundSettings(MusicVolume, EffectsVolume, isMuted);
        }

        /// <inheritdoc />
        public bool Equals(SoundSettings other)
        {
            return MusicVolume.Equals(other.MusicVolume)
                && EffectsVolume.Equals(other.EffectsVolume)
                && IsMuted == other.IsMuted;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is SoundSettings other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MusicVolume.GetHashCode();
                hash = (hash * 397) ^ EffectsVolume.GetHashCode();
                return (hash * 397) ^ IsMuted.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two settings by value.
        /// </summary>
        public static bool operator ==(SoundSettings left, SoundSettings right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two settings by value.
        /// </summary>
        public static bool operator !=(SoundSettings left, SoundSettings right)
        {
            return !left.Equals(right);
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
