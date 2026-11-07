#nullable enable
using System;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The language buttons of the start menu (Issue #338): one per shipped language, named in its own language, the
    /// current one highlighted. Pressing another language selects its locale and stores the choice. The highlight
    /// follows the selected locale reported through <see cref="SetCurrent"/>, not the button pressed, so it always
    /// shows the language really in use.
    /// </summary>
    public sealed class LanguageChooserController : IDisposable
    {
        /// <summary>
        /// USS class of the button of the current language.
        /// </summary>
        public const string SelectedClass = "new-game-language--selected";

        private readonly Button[] _buttons;
        private readonly string[] _codes;
        private readonly Action[] _handlers;
        private readonly ShellTextCache _text;
        private readonly ILanguagePreference _preference;
        private readonly Func<string, bool> _selectLocale;
        private int _lastTextVersion = int.MinValue;

        /// <summary>
        /// Initializes the chooser.
        /// </summary>
        /// <param name="buttons">One button per shipped language, in <see cref="ShellLanguages.Shipped"/> order.</param>
        /// <param name="text">Resolved texts holding the language names.</param>
        /// <param name="preference">Where the player's choice is stored.</param>
        /// <param name="selectLocale">Selects the locale of a code; returns false when that locale is not available.</param>
        public LanguageChooserController(
            Button[] buttons,
            ShellTextCache text,
            ILanguagePreference preference,
            Func<string, bool> selectLocale)
        {
            _buttons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _preference = preference ?? throw new ArgumentNullException(nameof(preference));
            _selectLocale = selectLocale ?? throw new ArgumentNullException(nameof(selectLocale));
            _codes = ShellLanguages.Shipped();
            if (_buttons.Length != _codes.Length)
            {
                throw new ArgumentException("One button per shipped language is required.", nameof(buttons));
            }

            _handlers = new Action[_codes.Length];
            for (int index = 0; index < _codes.Length; index++)
            {
                if (_buttons[index] == null)
                {
                    throw new ArgumentException("Language buttons cannot be null.", nameof(buttons));
                }

                string code = _codes[index];
                _handlers[index] = () => OnLanguagePressed(code);
                _buttons[index].clicked += _handlers[index];
            }

            Refresh(force: true);
        }

        /// <summary>
        /// Gets the code of the selected locale, or null until the Localization settings have selected one.
        /// </summary>
        public string? CurrentCode { get; private set; }

        /// <summary>
        /// Records the selected locale's code (null when unknown) and updates the highlight.
        /// </summary>
        public void SetCurrent(string? code)
        {
            CurrentCode = code;
            RefreshSelection();
        }

        /// <summary>
        /// Selects and stores <paramref name="code"/>. Ignored while the selected locale is unknown (the Localization
        /// settings are still loading) and when it is already the current language.
        /// </summary>
        public void OnLanguagePressed(string code)
        {
            if (!ShellLanguages.IsShipped(code))
            {
                throw new ArgumentOutOfRangeException(nameof(code), code, "Not a shipped language.");
            }

            if (CurrentCode == null || string.Equals(code, CurrentCode, StringComparison.Ordinal))
            {
                return;
            }

            if (_selectLocale(code))
            {
                _preference.Store(code);
            }
        }

        /// <summary>
        /// Applies the language names when the texts changed (or always when <paramref name="force"/>) and the
        /// highlight.
        /// </summary>
        public void Refresh(bool force)
        {
            if (force || _lastTextVersion != _text.Version)
            {
                _lastTextVersion = _text.Version;
                for (int index = 0; index < _codes.Length; index++)
                {
                    _buttons[index].text = _text.Get(ShellLanguages.NameKey(_codes[index]));
                }
            }

            RefreshSelection();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            for (int index = 0; index < _buttons.Length; index++)
            {
                _buttons[index].clicked -= _handlers[index];
            }
        }

        private void RefreshSelection()
        {
            for (int index = 0; index < _codes.Length; index++)
            {
                _buttons[index].EnableInClassList(
                    SelectedClass,
                    string.Equals(_codes[index], CurrentCode, StringComparison.Ordinal));
            }
        }
    }
}
