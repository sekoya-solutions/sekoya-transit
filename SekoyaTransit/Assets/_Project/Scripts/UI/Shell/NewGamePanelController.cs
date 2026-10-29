#nullable enable
using System;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The full-screen new game screen (Issue #67): paints the <see cref="NewGameViewModel"/> into the UXML elements
    /// and turns clicks into view-model calls. It never touches Core; Start only raises a <see cref="NewGameSelection"/>.
    /// </summary>
    public sealed class NewGamePanelController
    {
        private const string HiddenClass = "new-game-panel--hidden";
        private const string SelectedSizeClass = "new-game-size--selected";
        private const string ErrorHiddenClass = "new-game-error--hidden";

        private readonly VisualElement _panel;
        private readonly Label _title;
        private readonly Label _seedLabel;
        private readonly TextField _seedField;
        private readonly Button _randomButton;
        private readonly Label _seedError;
        private readonly Label _sizeLabel;
        private readonly Button[] _sizeButtons;
        private readonly Button _startButton;
        private readonly NewGameViewModel _model;
        private readonly ShellTextCache _text;
        private readonly Action<NewGameSelection> _onStart;
        private readonly Action[] _sizeHandlers;
        private readonly Action _randomHandler;
        private readonly Action _startHandler;
        private readonly EventCallback<ChangeEvent<string>> _seedChangedHandler;
        private int _lastTextVersion = int.MinValue;

        /// <summary>
        /// Initializes the controller over existing UXML elements and shows the screen.
        /// </summary>
        public NewGamePanelController(
            VisualElement panel,
            Label title,
            Label seedLabel,
            TextField seedField,
            Button randomButton,
            Label seedError,
            Label sizeLabel,
            Button[] sizeButtons,
            Button startButton,
            NewGameViewModel model,
            ShellTextCache text,
            Action<NewGameSelection> onStart)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _seedLabel = seedLabel ?? throw new ArgumentNullException(nameof(seedLabel));
            _seedField = seedField ?? throw new ArgumentNullException(nameof(seedField));
            _randomButton = randomButton ?? throw new ArgumentNullException(nameof(randomButton));
            _seedError = seedError ?? throw new ArgumentNullException(nameof(seedError));
            _sizeLabel = sizeLabel ?? throw new ArgumentNullException(nameof(sizeLabel));
            _sizeButtons = sizeButtons ?? throw new ArgumentNullException(nameof(sizeButtons));
            _startButton = startButton ?? throw new ArgumentNullException(nameof(startButton));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _onStart = onStart ?? throw new ArgumentNullException(nameof(onStart));

            NewGameMapSize[] sizes = NewGameMapSizes.All();
            if (_sizeButtons.Length != sizes.Length)
            {
                throw new ArgumentException("One size button per map size is required.", nameof(sizeButtons));
            }

            _seedField.maxLength = ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
            _seedChangedHandler = evt => OnSeedTextChanged(evt.newValue);
            _seedField.RegisterValueChangedCallback(_seedChangedHandler);
            _randomHandler = OnRandomPressed;
            _randomButton.clicked += _randomHandler;
            _startHandler = OnStartPressed;
            _startButton.clicked += _startHandler;
            _sizeHandlers = new Action[sizes.Length];
            for (int index = 0; index < sizes.Length; index++)
            {
                NewGameMapSize size = sizes[index];
                _sizeHandlers[index] = () => OnSizePressed(size);
                _sizeButtons[index].clicked += _sizeHandlers[index];
            }

            Refresh(force: true);
            SetOpen(true);
        }

        /// <summary>
        /// Gets the panel root (pointer tracking).
        /// </summary>
        public VisualElement Panel => _panel;

        /// <summary>
        /// Gets whether the screen is shown.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Gets the screen state.
        /// </summary>
        public NewGameViewModel Model => _model;

        /// <summary>
        /// Shows or hides the screen.
        /// </summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (open)
            {
                _panel.RemoveFromClassList(HiddenClass);
            }
            else
            {
                _panel.AddToClassList(HiddenClass);
            }
        }

        /// <summary>
        /// The player typed in the seed field.
        /// </summary>
        public void OnSeedTextChanged(string text)
        {
            _model.SetSeedText(text);
            Refresh(force: false);
        }

        /// <summary>
        /// The Random button was pressed: roll a new seed into the field.
        /// </summary>
        public void OnRandomPressed()
        {
            _model.Randomize();
            _seedField.SetValueWithoutNotify(_model.SeedText);
            Refresh(force: false);
        }

        /// <summary>
        /// A size toggle was pressed.
        /// </summary>
        public void OnSizePressed(NewGameMapSize size)
        {
            _model.SelectSize(size);
            Refresh(force: false);
        }

        /// <summary>
        /// The Start button was pressed: raises the selection unless the seed is invalid.
        /// </summary>
        public void OnStartPressed()
        {
            if (!IsOpen || !_model.TryCreateSelection(out NewGameSelection selection))
            {
                return;
            }

            _onStart(selection);
        }

        /// <summary>
        /// Repaints the screen; localized text follows the text cache version.
        /// </summary>
        public void Refresh(bool force)
        {
            if (force || _lastTextVersion != _text.Version)
            {
                _lastTextVersion = _text.Version;
                _title.text = _text.Get(NewGameUiKeys.Title);
                _seedLabel.text = _text.Get(NewGameUiKeys.SeedLabel);
                _randomButton.text = _text.Get(NewGameUiKeys.Random);
                _seedError.text = _text.Get(NewGameUiKeys.SeedError);
                _sizeLabel.text = _text.Get(NewGameUiKeys.SizeLabel);
                _startButton.text = _text.Get(NewGameUiKeys.Start);
                NewGameMapSize[] sizes = NewGameMapSizes.All();
                for (int index = 0; index < sizes.Length; index++)
                {
                    _sizeButtons[index].text = _text.Get(NewGameUiKeys.SizeKey(sizes[index]));
                }
            }

            if (!string.Equals(_seedField.value, _model.SeedText, StringComparison.Ordinal))
            {
                _seedField.SetValueWithoutNotify(_model.SeedText);
            }

            _seedError.EnableInClassList(ErrorHiddenClass, _model.IsSeedValid);
            _startButton.SetEnabled(_model.CanStart);
            NewGameMapSize[] all = NewGameMapSizes.All();
            for (int index = 0; index < all.Length; index++)
            {
                _sizeButtons[index].EnableInClassList(SelectedSizeClass, all[index] == _model.SelectedSize);
            }
        }

        /// <summary>
        /// Releases callbacks.
        /// </summary>
        public void Dispose()
        {
            _seedField.UnregisterValueChangedCallback(_seedChangedHandler);
            _randomButton.clicked -= _randomHandler;
            _startButton.clicked -= _startHandler;
            for (int index = 0; index < _sizeButtons.Length; index++)
            {
                _sizeButtons[index].clicked -= _sizeHandlers[index];
            }
        }
    }
}
