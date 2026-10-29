#nullable enable
using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Sound panel (Issue #68): music and effects volume sliders and a mute toggle. Every change is raised at once so
    /// the player hears it while dragging; the panel never stores or plays anything itself.
    /// </summary>
    public sealed class SoundPanelController : ShellPanelController
    {
        private readonly Label _musicLabel;
        private readonly Slider _musicSlider;
        private readonly Label _effectsLabel;
        private readonly Slider _effectsSlider;
        private readonly Toggle _mute;
        private readonly Button _close;
        private readonly Action<SoundSettings> _onChanged;
        private readonly Action _onClose;
        private readonly EventCallback<ChangeEvent<float>> _musicChanged;
        private readonly EventCallback<ChangeEvent<float>> _effectsChanged;
        private readonly EventCallback<ChangeEvent<bool>> _muteChanged;

        /// <summary>
        /// Initializes the controller for the UXML sound panel.
        /// </summary>
        public SoundPanelController(
            VisualElement panel,
            Label title,
            Label musicLabel,
            Slider musicSlider,
            Label effectsLabel,
            Slider effectsSlider,
            Toggle mute,
            Button close,
            Action<SoundSettings> onChanged,
            Action onClose,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "sound-panel--hidden", title, SoundUiKeys.Title, text, culture)
        {
            _musicLabel = musicLabel ?? throw new ArgumentNullException(nameof(musicLabel));
            _musicSlider = musicSlider ?? throw new ArgumentNullException(nameof(musicSlider));
            _effectsLabel = effectsLabel ?? throw new ArgumentNullException(nameof(effectsLabel));
            _effectsSlider = effectsSlider ?? throw new ArgumentNullException(nameof(effectsSlider));
            _mute = mute ?? throw new ArgumentNullException(nameof(mute));
            _close = close ?? throw new ArgumentNullException(nameof(close));
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
            _musicSlider.lowValue = 0f;
            _musicSlider.highValue = 1f;
            _effectsSlider.lowValue = 0f;
            _effectsSlider.highValue = 1f;
            _musicChanged = evt => SetSettings(Settings.WithMusicVolume(evt.newValue), notify: true);
            _effectsChanged = evt => SetSettings(Settings.WithEffectsVolume(evt.newValue), notify: true);
            _muteChanged = evt => SetSettings(Settings.WithMuted(evt.newValue), notify: true);
            _musicSlider.RegisterValueChangedCallback(_musicChanged);
            _effectsSlider.RegisterValueChangedCallback(_effectsChanged);
            _mute.RegisterValueChangedCallback(_muteChanged);
            _close.clicked += _onClose;
            ShowSettings();
        }

        /// <summary>
        /// Gets the settings the panel shows.
        /// </summary>
        public SoundSettings Settings { get; private set; } = SoundSettings.Default;

        /// <summary>
        /// Shows <paramref name="settings"/>; <paramref name="notify"/> also raises the change (player input).
        /// </summary>
        public void SetSettings(SoundSettings settings, bool notify)
        {
            bool changed = settings != Settings;
            Settings = settings;
            ShowSettings();
            if (notify && changed)
            {
                _onChanged(settings);
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _musicSlider.UnregisterValueChangedCallback(_musicChanged);
            _effectsSlider.UnregisterValueChangedCallback(_effectsChanged);
            _mute.UnregisterValueChangedCallback(_muteChanged);
            _close.clicked -= _onClose;
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            if (!force)
            {
                return;
            }

            _musicLabel.text = Text.Get(SoundUiKeys.Music);
            _effectsLabel.text = Text.Get(SoundUiKeys.Effects);
            _mute.text = Text.Get(SoundUiKeys.Mute);
            _close.text = Text.Get(SoundUiKeys.Close);
        }

        private void ShowSettings()
        {
            _musicSlider.SetValueWithoutNotify(Settings.MusicVolume);
            _effectsSlider.SetValueWithoutNotify(Settings.EffectsVolume);
            _mute.SetValueWithoutNotify(Settings.IsMuted);
        }
    }
}
