#nullable enable
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Shell;
using UnityEngine;

namespace SekoyaTransit.Presentation.Audio
{
    /// <summary>
    /// Plays the looping ambient music and the sound effects of Issue #68 (ADR 0018): one 2D source for music and one
    /// for effects, whose volumes follow the player's <see cref="SoundSettings"/>. No mixer asset. Holds no game state;
    /// <see cref="MapPlaySession"/> decides when a sound plays. Browsers start the audio after the player's first
    /// interaction.
    /// </summary>
    public sealed class MapPlayAudio : MonoBehaviour
    {
        [SerializeField]
        private AudioClip? _music;

        [SerializeField]
        private AudioClip? _click;

        [SerializeField]
        private AudioClip? _build;

        [SerializeField]
        private AudioClip? _demolish;

        [SerializeField]
        private AudioClip? _cashIn;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Real seconds between two cash-in sounds; deliveries paid in between stay silent.")]
        private float _cashInMinimumGapSeconds = 0.5f;

        private AudioSource? _musicSource;
        private AudioSource? _effectsSource;
        private SoundCooldown? _cashInCooldown;
        private SoundSettings _settings = SoundSettings.Default;

        /// <summary>
        /// Gets the settings the volumes follow.
        /// </summary>
        public SoundSettings Settings => _settings;

        /// <summary>
        /// Returns the effect that gives feedback for a map action.
        /// </summary>
        public static SoundEffect EffectFor(MapPlayerAction action)
        {
            switch (action)
            {
                case MapPlayerAction.Built:
                    return SoundEffect.Build;
                case MapPlayerAction.Demolished:
                    return SoundEffect.Demolish;
                default:
                    return SoundEffect.Click;
            }
        }

        /// <summary>
        /// Applies new volumes to the music and effects sources at once.
        /// </summary>
        public void ApplySettings(SoundSettings settings)
        {
            _settings = settings;
            if (_musicSource != null)
            {
                _musicSource.volume = settings.EffectiveMusicVolume;
            }

            if (_effectsSource != null)
            {
                _effectsSource.volume = settings.EffectiveEffectsVolume;
            }
        }

        /// <summary>
        /// Plays <paramref name="effect"/> over any effect already playing. Nothing plays when muted, when the effects
        /// volume is zero, or for a cash-in within the minimum gap of the last one.
        /// </summary>
        public void Play(SoundEffect effect)
        {
            AudioClip? clip = ClipFor(effect);
            if (clip == null || _effectsSource == null || _settings.EffectiveEffectsVolume <= 0f)
            {
                return;
            }

            if (effect == SoundEffect.CashIn
                && _cashInCooldown != null
                && !_cashInCooldown.TryStart(Time.unscaledTime))
            {
                return;
            }

            _effectsSource.PlayOneShot(clip);
        }

        private void Awake()
        {
            _musicSource = CreateSource("Music");
            _musicSource.loop = true;
            _effectsSource = CreateSource("Effects");
            _cashInCooldown = new SoundCooldown(Mathf.Max(0f, _cashInMinimumGapSeconds));
            ApplySettings(_settings);
        }

        private void Start()
        {
            if (_music == null || _musicSource == null)
            {
                return;
            }

            _musicSource.clip = _music;
            _musicSource.Play();
        }

        private AudioSource CreateSource(string name)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(transform, worldPositionStays: false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private AudioClip? ClipFor(SoundEffect effect)
        {
            switch (effect)
            {
                case SoundEffect.Click:
                    return _click;
                case SoundEffect.Build:
                    return _build;
                case SoundEffect.Demolish:
                    return _demolish;
                case SoundEffect.CashIn:
                    return _cashIn;
                default:
                    return null;
            }
        }
    }
}
