#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Hosts the save and load panel, its top bar button, and the Continue and Load buttons of the new game screen
    /// (Issue #183), so <see cref="MapPlayShell"/> only wires them in. Owns the localized-text cache the panel reads.
    /// Saving and loading themselves happen in Presentation: this host only raises slot ids.
    /// </summary>
    public sealed class SaveLoadShellPanels
    {
        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly HashSet<string> _requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action _onBeforeOpen;
        private readonly Button _toggle;
        private readonly Button _continueButton;
        private readonly Button _loadButton;
        private readonly SaveLoadPanelController _panel;
        private readonly Action _toggleHandler;
        private readonly Action _continueHandler;
        private readonly Action _loadHandler;
        private int _lastTextVersion = int.MinValue;
        private int _lastModelVersion = int.MinValue;

        /// <summary>
        /// Queries the elements from the UXML root. <paramref name="onBeforeOpen"/> runs before the panel opens so the
        /// shell can close its other panels.
        /// </summary>
        public SaveLoadShellPanels(VisualElement root, Func<CultureInfo> culture, Action onBeforeOpen)
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
            VisualElement? panel = root.Q<VisualElement>("save-load-panel");
            Label? title = root.Q<Label>("save-load-title");
            ScrollView? list = root.Q<ScrollView>("save-load-list");
            VisualElement? confirm = root.Q<VisualElement>("save-load-confirm");
            Label? confirmText = root.Q<Label>("save-load-confirm-text");
            Button? confirmYes = root.Q<Button>("save-load-confirm-yes");
            Button? confirmNo = root.Q<Button>("save-load-confirm-no");
            Label? status = root.Q<Label>("save-load-status");
            Button? close = root.Q<Button>("save-load-close");
            Button? toggle = root.Q<Button>("save-load-toggle");
            Button? continueButton = root.Q<Button>("new-game-continue");
            Button? loadButton = root.Q<Button>("new-game-load");
            if (panel == null
                || title == null
                || list == null
                || confirm == null
                || confirmText == null
                || confirmYes == null
                || confirmNo == null
                || status == null
                || close == null
                || toggle == null
                || continueButton == null
                || loadButton == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define the save and load panel, its toggle, and the new game Continue and Load buttons.");
            }

            _toggle = toggle;
            _continueButton = continueButton;
            _loadButton = loadButton;
            panel.pickingMode = PickingMode.Position;
            _panel = new SaveLoadPanelController(
                panel,
                title,
                list,
                confirm,
                confirmText,
                confirmYes,
                confirmNo,
                status,
                close,
                new SaveLoadViewModel(),
                slotId => SaveRequested?.Invoke(slotId),
                OnLoadChosen,
                () => SetOpen(false),
                _text,
                culture);
            _toggleHandler = Toggle;
            _continueHandler = OnContinuePressed;
            _loadHandler = OnLoadGamePressed;
            _toggle.clicked += _toggleHandler;
            _continueButton.clicked += _continueHandler;
            _loadButton.clicked += _loadHandler;
        }

        /// <summary>
        /// Raised when the player presses Save on a manual slot; the argument is the slot number.
        /// </summary>
        public event Action<string>? SaveRequested;

        /// <summary>
        /// Raised when a load is chosen (and confirmed while a game runs); the argument is the slot id.
        /// </summary>
        public event Action<string>? LoadRequested;

        /// <summary>
        /// Raised when the player presses Continue on the new game screen.
        /// </summary>
        public event Action? ContinueRequested;

        /// <summary>
        /// Raised when the panel opens, so Presentation refreshes the slot list.
        /// </summary>
        public event Action? SlotsRefreshRequested;

        /// <summary>
        /// Gets whether the panel is open.
        /// </summary>
        public bool IsOpen => _panel.IsOpen;

        /// <summary>
        /// Gets the panel state.
        /// </summary>
        public SaveLoadViewModel Model => _panel.Model;

        /// <summary>
        /// Gets the panel controller (tests).
        /// </summary>
        public SaveLoadPanelController Panel => _panel;

        /// <summary>
        /// The Continue button of the new game screen was pressed.
        /// </summary>
        public void OnContinuePressed()
        {
            ContinueRequested?.Invoke();
        }

        /// <summary>
        /// The Load game button of the new game screen was pressed: opens the slot list.
        /// </summary>
        public void OnLoadGamePressed()
        {
            SetOpen(true);
        }

        /// <summary>
        /// Opens or closes the panel (top bar button).
        /// </summary>
        public void Toggle()
        {
            SetOpen(!_panel.IsOpen);
        }

        /// <summary>
        /// Opens or closes the panel; opening asks Presentation for a fresh slot list.
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
                _panel.SetOpen(true);
                SlotsRefreshRequested?.Invoke();
            }
            else
            {
                _panel.Model.CancelLoad();
                if (_panel.Model.Status == SaveLoadStatus.Saved)
                {
                    _panel.Model.SetStatus(SaveLoadStatus.None);
                }

                _panel.SetOpen(false);
            }
        }

        /// <summary>
        /// Replaces the slot list shown by the panel and enables the Continue and Load buttons accordingly.
        /// </summary>
        public void SetSlots(IReadOnlyList<SaveSlotEntry> slots)
        {
            _panel.Model.SetSlots(SaveLoadViewModel.CompleteSlots(slots));
        }

        /// <summary>
        /// Records whether a game is running: saving needs one and loading asks for confirmation.
        /// </summary>
        public void SetGameRunning(bool running)
        {
            _panel.Model.SetGameRunning(running);
        }

        /// <summary>
        /// Shows the outcome of the last save or load in the panel; <paramref name="openPanel"/> also opens it so a
        /// failed load is not missed.
        /// </summary>
        public void SetStatus(SaveLoadStatus status, bool openPanel = false)
        {
            _panel.Model.SetStatus(status);
            if (openPanel && !_panel.IsOpen)
            {
                SetOpen(true);
            }
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
            for (int index = 0; index < SaveLoadUiKeys.StaticKeys.Length; index++)
            {
                string key = SaveLoadUiKeys.StaticKeys[index];
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
        /// Per-frame update: button texts follow localization and the model, and the open panel repaints.
        /// </summary>
        public void Update()
        {
            if (_lastTextVersion != _text.Version)
            {
                _lastTextVersion = _text.Version;
                _toggle.text = _text.Get(SaveLoadUiKeys.Toggle);
                _continueButton.text = _text.Get(SaveLoadUiKeys.Continue);
                _loadButton.text = _text.Get(SaveLoadUiKeys.LoadGame);
            }

            if (_lastModelVersion != _panel.Model.Version)
            {
                _lastModelVersion = _panel.Model.Version;
                bool hasSave = _panel.Model.HasLoadableSave;
                _continueButton.SetEnabled(hasSave);
                _loadButton.SetEnabled(hasSave);
            }

            _panel.Refresh(force: false);
        }

        /// <summary>
        /// Releases callbacks.
        /// </summary>
        public void Dispose()
        {
            _toggle.clicked -= _toggleHandler;
            _continueButton.clicked -= _continueHandler;
            _loadButton.clicked -= _loadHandler;
            _panel.Dispose();
            SaveRequested = null;
            LoadRequested = null;
            ContinueRequested = null;
            SlotsRefreshRequested = null;
        }

        private void OnLoadChosen(string slotId)
        {
            _panel.SetOpen(false);
            LoadRequested?.Invoke(slotId);
        }
    }
}
