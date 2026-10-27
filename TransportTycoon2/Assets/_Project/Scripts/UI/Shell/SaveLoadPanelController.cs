#nullable enable
using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// The save and load panel (Issue #183): the autosave and five manual slots with Save and Load buttons, the
    /// confirmation asked before a load replaces a running game, and the outcome of the last save or load.
    /// It never touches Core; it raises slot ids and paints what the <see cref="SaveLoadViewModel"/> holds.
    /// </summary>
    public sealed class SaveLoadPanelController : ShellPanelController
    {
        private const string HiddenClass = "save-load-panel--hidden";
        private const string ConfirmHiddenClass = "save-load-confirm--hidden";

        private readonly ScrollView _list;
        private readonly VisualElement _confirm;
        private readonly Label _confirmText;
        private readonly Button _confirmYes;
        private readonly Button _confirmNo;
        private readonly Label _status;
        private readonly Button _close;
        private readonly SaveLoadViewModel _model;
        private readonly Action<string> _onSave;
        private readonly Action<string> _onLoad;
        private readonly Action _onClose;
        private readonly Action _confirmYesHandler;
        private readonly Action _confirmNoHandler;
        private readonly Action _closeHandler;
        private int _lastModelVersion = int.MinValue;

        /// <summary>
        /// Initializes the controller over the panel elements.
        /// </summary>
        public SaveLoadPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            VisualElement confirm,
            Label confirmText,
            Button confirmYes,
            Button confirmNo,
            Label status,
            Button close,
            SaveLoadViewModel model,
            Action<string> onSave,
            Action<string> onLoad,
            Action onClose,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, HiddenClass, title, SaveLoadUiKeys.Title, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            _confirmText = confirmText ?? throw new ArgumentNullException(nameof(confirmText));
            _confirmYes = confirmYes ?? throw new ArgumentNullException(nameof(confirmYes));
            _confirmNo = confirmNo ?? throw new ArgumentNullException(nameof(confirmNo));
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _close = close ?? throw new ArgumentNullException(nameof(close));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
            _onLoad = onLoad ?? throw new ArgumentNullException(nameof(onLoad));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
            _confirmYesHandler = OnConfirmYes;
            _confirmNoHandler = _model.CancelLoad;
            _closeHandler = () => _onClose();
            _confirmYes.clicked += _confirmYesHandler;
            _confirmNo.clicked += _confirmNoHandler;
            _close.clicked += _closeHandler;
        }

        /// <summary>
        /// Gets the panel state.
        /// </summary>
        public SaveLoadViewModel Model => _model;

        /// <summary>
        /// A slot's Load button was pressed: loads at once, or asks for confirmation while a game runs.
        /// </summary>
        public void OnLoadPressed(string slotId)
        {
            if (_model.TryRequestLoad(slotId))
            {
                _onLoad(slotId);
            }
        }

        /// <summary>
        /// A slot's Save button was pressed.
        /// </summary>
        public void OnSavePressed(string slotId)
        {
            SaveSlotEntry? slot = FindSlot(slotId);
            if (slot != null && _model.CanSave(slot))
            {
                _onSave(slotId);
            }
        }

        /// <summary>
        /// The Load anyway button of the confirmation was pressed.
        /// </summary>
        public void OnConfirmYes()
        {
            string? slotId = _model.ConfirmLoad();
            if (slotId != null)
            {
                _onLoad(slotId);
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _confirmYes.clicked -= _confirmYesHandler;
            _confirmNo.clicked -= _confirmNoHandler;
            _close.clicked -= _closeHandler;
            _list.Clear();
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            if (!force && _lastModelVersion == _model.Version)
            {
                return;
            }

            _lastModelVersion = _model.Version;
            _close.text = Text.Get(SaveLoadUiKeys.Close);
            _confirmText.text = Text.Get(SaveLoadUiKeys.ConfirmText);
            _confirmYes.text = Text.Get(SaveLoadUiKeys.ConfirmYes);
            _confirmNo.text = Text.Get(SaveLoadUiKeys.ConfirmNo);
            _confirm.EnableInClassList(ConfirmHiddenClass, _model.PendingLoadId == null);
            string? statusKey = SaveLoadUiKeys.StatusKey(_model.Status);
            _status.text = statusKey == null ? string.Empty : Text.Get(statusKey);

            _list.Clear();
            for (int index = 0; index < _model.Slots.Count; index++)
            {
                _list.Add(BuildRow(_model.Slots[index]));
            }
        }

        private VisualElement BuildRow(SaveSlotEntry slot)
        {
            var row = new VisualElement();
            row.AddToClassList("save-load-row");
            var texts = new VisualElement();
            texts.AddToClassList("save-load-row-texts");
            var name = new Label(SlotName(slot));
            name.AddToClassList("save-load-row-name");
            var info = new Label(SlotInfo(slot));
            info.AddToClassList("save-load-row-info");
            texts.Add(name);
            texts.Add(info);
            row.Add(texts);

            string slotId = slot.Id;
            if (_model.CanSave(slot))
            {
                var save = new Button(() => OnSavePressed(slotId)) { text = Text.Get(SaveLoadUiKeys.Save) };
                save.AddToClassList("save-load-button");
                row.Add(save);
            }

            var load = new Button(() => OnLoadPressed(slotId)) { text = Text.Get(SaveLoadUiKeys.Load) };
            load.AddToClassList("save-load-button");
            load.SetEnabled(slot.CanLoad);
            row.Add(load);
            return row;
        }

        private string SlotName(SaveSlotEntry slot)
        {
            if (slot.IsAutosave)
            {
                return Text.Get(SaveLoadUiKeys.SlotAutosave);
            }

            return string.Format(CultureInfo.InvariantCulture, Text.Get(SaveLoadUiKeys.SlotManual), slot.Id);
        }

        private string SlotInfo(SaveSlotEntry slot)
        {
            if (slot.IsEmpty)
            {
                return Text.Get(SaveLoadUiKeys.SlotEmpty);
            }

            if (slot.IsUnreadable)
            {
                return Text.Get(SaveLoadUiKeys.SlotUnreadable);
            }

            return SaveLoadViewModel.FormatSlotInfo(Text.Get(SaveLoadUiKeys.SlotInfo), slot);
        }

        private SaveSlotEntry? FindSlot(string slotId)
        {
            for (int index = 0; index < _model.Slots.Count; index++)
            {
                if (string.Equals(_model.Slots[index].Id, slotId, StringComparison.Ordinal))
                {
                    return _model.Slots[index];
                }
            }

            return null;
        }
    }
}
