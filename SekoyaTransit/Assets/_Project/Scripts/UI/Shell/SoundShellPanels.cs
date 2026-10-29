#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Hosts the sound panel and its top bar button (Issue #68), so <see cref="MapPlayShell"/> only wires them in.
    /// Owns the localized-text cache the panel reads. Playing and storing the settings happen in Presentation: this
    /// host only raises the settings the player chose.
    /// </summary>
    public sealed class SoundShellPanels
    {
        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly HashSet<string> _requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action _onBeforeOpen;
        private readonly Button _toggle;
        private readonly SoundPanelController _panel;
        private readonly Action _toggleHandler;
        private int _lastTextVersion = int.MinValue;

        /// <summary>
        /// Queries the elements from the UXML root. <paramref name="onBeforeOpen"/> runs before the panel opens so the
        /// shell can close its other panels.
        /// </summary>
        public SoundShellPanels(VisualElement root, Func<CultureInfo> culture, Action onBeforeOpen)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            _onBeforeOpen = onBeforeOpen ?? throw new ArgumentNullException(nameof(onBeforeOpen));
            VisualElement? panel = root.Q<VisualElement>("sound-panel");
            Label? title = root.Q<Label>("sound-title");
            Label? musicLabel = root.Q<Label>("sound-music-label");
            Slider? musicSlider = root.Q<Slider>("sound-music");
            Label? effectsLabel = root.Q<Label>("sound-effects-label");
            Slider? effectsSlider = root.Q<Slider>("sound-effects");
            Toggle? mute = root.Q<Toggle>("sound-mute");
            Button? close = root.Q<Button>("sound-close");
            Button? toggle = root.Q<Button>("sound-toggle");
            if (panel == null
                || title == null
                || musicLabel == null
                || musicSlider == null
                || effectsLabel == null
                || effectsSlider == null
                || mute == null
                || close == null
                || toggle == null)
            {
                throw new InvalidOperationException("MapPlayShell UXML must define the sound panel and its toggle.");
            }

            _toggle = toggle;
            panel.pickingMode = PickingMode.Position;
            _panel = new SoundPanelController(
                panel,
                title,
                musicLabel,
                musicSlider,
                effectsLabel,
                effectsSlider,
                mute,
                close,
                settings => SettingsChanged?.Invoke(settings),
                () => SetOpen(false),
                _text,
                culture);
            _toggleHandler = Toggle;
            _toggle.clicked += _toggleHandler;
        }

        /// <summary>
        /// Raised when the player changes a volume or the mute toggle.
        /// </summary>
        public event Action<SoundSettings>? SettingsChanged;

        /// <summary>
        /// Gets whether the panel is open.
        /// </summary>
        public bool IsOpen => _panel.IsOpen;

        /// <summary>
        /// Gets the panel controller (pointer tracking and tests).
        /// </summary>
        public SoundPanelController Panel => _panel;

        /// <summary>
        /// Opens or closes the panel (top bar button).
        /// </summary>
        public void Toggle()
        {
            SetOpen(!_panel.IsOpen);
        }

        /// <summary>
        /// Opens or closes the panel.
        /// </summary>
        public void SetOpen(bool open)
        {
            if (open == _panel.IsOpen)
            {
                return;
            }

            if (open)
            {
                _onBeforeOpen();
            }

            _panel.SetOpen(open);
        }

        /// <summary>
        /// Shows the stored settings without raising <see cref="SettingsChanged"/>.
        /// </summary>
        public void SetSettings(SoundSettings settings)
        {
            _panel.SetSettings(settings, notify: false);
        }

        /// <summary>
        /// Returns whether <paramref name="element"/> is the panel.
        /// </summary>
        public bool IsPanel(VisualElement? element)
        {
            return element == _panel.Panel;
        }

        /// <summary>
        /// Loads every string through localization. Call once the localization system is initialized and again on a
        /// locale change.
        /// </summary>
        public void LoadTexts()
        {
            _requestedKeys.Clear();
            for (int index = 0; index < SoundUiKeys.StaticKeys.Length; index++)
            {
                string key = SoundUiKeys.StaticKeys[index];
                if (!_requestedKeys.Add(key))
                {
                    continue;
                }

                string captured = key;
                var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(GameUiKeys.Table, captured);
                operation.Completed += handle =>
                {
                    if (!string.IsNullOrEmpty(handle.Result))
                    {
                        _text.Set(captured, handle.Result);
                    }
                };
            }
        }

        /// <summary>
        /// Per-frame update: the button tooltip follows localization and the open panel repaints.
        /// </summary>
        public void Update()
        {
            if (_lastTextVersion != _text.Version)
            {
                _lastTextVersion = _text.Version;
                ShellTooltip.SetIconButtonName(_toggle, _text.Get(SoundUiKeys.Toggle));
            }

            _panel.Refresh(force: false);
        }

        /// <summary>
        /// Releases callbacks.
        /// </summary>
        public void Dispose()
        {
            _toggle.clicked -= _toggleHandler;
            _panel.Dispose();
            SettingsChanged = null;
        }
    }
}
