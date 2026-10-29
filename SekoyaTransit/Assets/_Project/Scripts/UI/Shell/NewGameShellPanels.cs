#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Hosts the new game screen, the in-game seed display, and the game-over New game button (Issue #67), so
    /// <see cref="MapPlayShell"/> only wires them in. While the screen is open the rest of the shell chrome is hidden.
    /// </summary>
    public sealed class NewGameShellPanels
    {
        private const string NewGameRootClass = "shell-root--new-game";

        private readonly VisualElement _root;
        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly HashSet<string> _requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly NewGamePanelController _screen;
        private readonly Label _seedInfoLabel;
        private readonly Label _gameOverSeed;
        private readonly Button _gameOverNewGame;
        private readonly Action _gameOverNewGameHandler;
        private bool _hasGameInfo;
        private NewGameSelection _gameInfo;
        private int _lastTextVersion = int.MinValue;

        /// <summary>
        /// Queries the elements from the UXML root and shows the new game screen with a random seed.
        /// </summary>
        /// <param name="root">The shell root.</param>
        /// <param name="randomSeed">Supplies the seed shown first and rolled by the Random button.</param>
        public NewGameShellPanels(VisualElement root, Func<ulong> randomSeed)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            if (randomSeed is null)
            {
                throw new ArgumentNullException(nameof(randomSeed));
            }

            VisualElement? panel = root.Q<VisualElement>("new-game-panel");
            Label? gameTitle = root.Q<Label>("new-game-game-title");
            Label? title = root.Q<Label>("new-game-title");
            Label? seedLabel = root.Q<Label>("new-game-seed-label");
            TextField? seedField = root.Q<TextField>("new-game-seed");
            Button? randomButton = root.Q<Button>("new-game-random");
            Label? seedError = root.Q<Label>("new-game-seed-error");
            Label? sizeLabel = root.Q<Label>("new-game-size-label");
            Button? sizeSmall = root.Q<Button>("new-game-size-small");
            Button? sizeMedium = root.Q<Button>("new-game-size-medium");
            Button? sizeLarge = root.Q<Button>("new-game-size-large");
            Button? startButton = root.Q<Button>("new-game-start");
            Label? seedInfoLabel = root.Q<Label>("seed-info-label");
            Label? gameOverSeed = root.Q<Label>("game-over-seed");
            Button? gameOverNewGame = root.Q<Button>("game-over-new-game");
            if (panel == null
                || gameTitle == null
                || title == null
                || seedLabel == null
                || seedField == null
                || randomButton == null
                || seedError == null
                || sizeLabel == null
                || sizeSmall == null
                || sizeMedium == null
                || sizeLarge == null
                || startButton == null
                || seedInfoLabel == null
                || gameOverSeed == null
                || gameOverNewGame == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define the new game screen, the seed info label, and the game-over seed label and New game button.");
            }

            // The game name is the Player Settings product name (Issue #198), a proper noun that is not localized.
            gameTitle.text = Application.productName;
            _seedInfoLabel = seedInfoLabel;
            _gameOverSeed = gameOverSeed;
            _gameOverNewGame = gameOverNewGame;
            panel.pickingMode = PickingMode.Position;
            _screen = new NewGamePanelController(
                panel,
                title,
                seedLabel,
                seedField,
                randomButton,
                seedError,
                sizeLabel,
                new[] { sizeSmall, sizeMedium, sizeLarge },
                startButton,
                new NewGameViewModel(randomSeed),
                _text,
                selection => NewGameRequested?.Invoke(selection));
            _root.AddToClassList(NewGameRootClass);
            _gameOverNewGameHandler = OnGameOverNewGamePressed;
            _gameOverNewGame.clicked += _gameOverNewGameHandler;
        }

        /// <summary>
        /// Raised when the player presses Start with a valid seed.
        /// </summary>
        public event Action<NewGameSelection>? NewGameRequested;

        /// <summary>
        /// Raised when the player presses New game on the game-over screen.
        /// </summary>
        public event Action? GameOverNewGameRequested;

        /// <summary>
        /// Gets whether the new game screen is shown.
        /// </summary>
        public bool IsNewGameScreenOpen => _screen.IsOpen;

        /// <summary>
        /// Gets the new game screen root (pointer tracking).
        /// </summary>
        public VisualElement Panel => _screen.Panel;

        /// <summary>
        /// Gets the new game screen controller.
        /// </summary>
        public NewGamePanelController Screen => _screen;

        /// <summary>
        /// Hides the new game screen and shows the game chrome.
        /// </summary>
        public void CloseNewGameScreen()
        {
            _screen.SetOpen(false);
            _root.RemoveFromClassList(NewGameRootClass);
        }

        /// <summary>
        /// Records the seed and map size of the running game for the side panel and the game-over screen.
        /// </summary>
        public void SetGameInfo(NewGameSelection selection)
        {
            _hasGameInfo = true;
            _gameInfo = selection;
            _lastTextVersion = int.MinValue;
            RefreshGameInfo();
        }

        /// <summary>
        /// The game-over New game button was pressed.
        /// </summary>
        public void OnGameOverNewGamePressed()
        {
            GameOverNewGameRequested?.Invoke();
        }

        /// <summary>
        /// Loads every string through localization. Call once the localization system is initialized and again on a
        /// locale change.
        /// </summary>
        public void LoadTexts()
        {
            _requestedKeys.Clear();
            for (int index = 0; index < NewGameUiKeys.StaticKeys.Length; index++)
            {
                string key = NewGameUiKeys.StaticKeys[index];
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
        /// Per-frame update: texts follow localization.
        /// </summary>
        public void Update()
        {
            _screen.Refresh(force: false);
            RefreshGameInfo();
        }

        /// <summary>
        /// Releases callbacks.
        /// </summary>
        public void Dispose()
        {
            _screen.Dispose();
            _gameOverNewGame.clicked -= _gameOverNewGameHandler;
            NewGameRequested = null;
            GameOverNewGameRequested = null;
        }

        private void RefreshGameInfo()
        {
            if (_lastTextVersion == _text.Version)
            {
                return;
            }

            _lastTextVersion = _text.Version;
            _gameOverNewGame.text = _text.Get(NewGameUiKeys.GameOverNewGame);
            if (!_hasGameInfo)
            {
                return;
            }

            string info = NewGameViewModel.FormatSeedInfo(
                _text.Get(NewGameUiKeys.SeedInfo),
                _gameInfo.Seed,
                _gameInfo.MapSize);
            _seedInfoLabel.text = info;
            _gameOverSeed.text = info;
        }
    }
}
