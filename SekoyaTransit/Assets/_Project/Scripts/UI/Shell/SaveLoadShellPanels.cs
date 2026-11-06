#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Hosts the save and load panel, its top bar button, and the Continue and Load buttons of the new game screen
    /// (Issue #183), plus the Debug download button and Load from file of WebGL players (Issue #320), so
    /// <see cref="MapPlayShell"/> only wires them in. Owns the localized-text cache the panel reads.
    /// Saving and loading themselves happen in Presentation: this host only raises slot ids.
    /// </summary>
    public sealed class SaveLoadShellPanels
    {
        private const string FileTransferUnavailableClass = "shell-file-transfer--unavailable";

        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly ShellTextRequests _textRequests;
        private readonly HashSet<string> _requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action _onBeforeOpen;
        private readonly Button _toggle;
        private readonly Button _continueButton;
        private readonly Button _loadButton;
        private readonly Button _debugButton;
        private readonly SaveLoadPanelController _panel;
        private readonly Action _toggleHandler;
        private readonly Action _continueHandler;
        private readonly Action _loadHandler;
        private readonly Action _debugHandler;
        private int _lastTextVersion = int.MinValue;
        private int _lastModelVersion = int.MinValue;

        /// <summary>
        /// Queries the elements from the UXML root. <paramref name="onBeforeOpen"/> runs before the panel opens so the
        /// shell can close its other panels.
        /// </summary>
        public SaveLoadShellPanels(
            VisualElement root,
            Func<CultureInfo> culture,
            Action onBeforeOpen,
            ShellTextRequests textRequests)
        {
            _textRequests = textRequests ?? throw new ArgumentNullException(nameof(textRequests));
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
            Button? loadFromFile = root.Q<Button>("save-load-file");
            Button? debugButton = root.Q<Button>("debug-download");
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
                || loadButton == null
                || loadFromFile == null
                || debugButton == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define the save and load panel, its toggle and Load from file button, the Debug button, and the new game Continue and Load buttons.");
            }

            _toggle = toggle;
            _continueButton = continueButton;
            _loadButton = loadButton;
            _debugButton = debugButton;
            panel.pickingMode = PickingMode.Position;
            _panel = new SaveLoadPanelController(
                panel,
                title,
                list,
                loadFromFile,
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
            _debugHandler = OnDebugPressed;
            _toggle.clicked += _toggleHandler;
            SetFileTransferAvailable(false);
            _continueButton.clicked += _continueHandler;
            _loadButton.clicked += _loadHandler;
            _debugButton.clicked += _debugHandler;
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
        /// Raised when Load from file is chosen (and confirmed while a game runs), so Presentation opens the file
        /// picker. The panel stays open until a file is read, so a cancelled picker leaves it as it was.
        /// </summary>
        public event Action? LoadFromFileRequested;

        /// <summary>
        /// Raised when the player presses the top bar Debug button, so Presentation downloads the running game.
        /// </summary>
        public event Action? DebugDownloadRequested;

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
        /// The top bar Debug button was pressed; ignored where file transfer does not exist.
        /// </summary>
        public void OnDebugPressed()
        {
            if (_panel.Model.FileTransferAvailable)
            {
                DebugDownloadRequested?.Invoke();
            }
        }

        /// <summary>
        /// Shows or hides the Debug button and Load from file (Issue #320: WebGL players only).
        /// </summary>
        public void SetFileTransferAvailable(bool available)
        {
            _panel.Model.SetFileTransferAvailable(available);
            _debugButton.EnableInClassList(FileTransferUnavailableClass, !available);
        }

        /// <summary>
        /// Closes the panel once the file chosen for Load from file has been read, before the load itself starts, the
        /// same moment a slot load closes it.
        /// </summary>
        public void CloseForFileLoad()
        {
            _panel.SetOpen(false);
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
                _textRequests.Request(captured, value => _text.Set(captured, value));
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
                ShellTooltip.SetIconButtonName(_toggle, _text.Get(SaveLoadUiKeys.Toggle));
                ShellTooltip.SetIconButtonName(_debugButton, _text.Get(SaveLoadUiKeys.DebugDownload));
                _continueButton.text = _text.Get(SaveLoadUiKeys.Continue);
                _loadButton.text = _text.Get(SaveLoadUiKeys.LoadGame);
            }

            if (_lastModelVersion != _panel.Model.Version)
            {
                _lastModelVersion = _panel.Model.Version;
                _continueButton.SetEnabled(_panel.Model.HasLoadableSave);
                _loadButton.SetEnabled(_panel.Model.HasSomethingToLoad);
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
            _debugButton.clicked -= _debugHandler;
            _panel.Dispose();
            SaveRequested = null;
            LoadRequested = null;
            ContinueRequested = null;
            SlotsRefreshRequested = null;
            LoadFromFileRequested = null;
            DebugDownloadRequested = null;
        }

        private void OnLoadChosen(string slotId)
        {
            if (string.Equals(slotId, SaveLoadViewModel.FileLoadId, StringComparison.Ordinal))
            {
                LoadFromFileRequested?.Invoke();
                return;
            }

            _panel.SetOpen(false);
            LoadRequested?.Invoke(slotId);
        }
    }
}
