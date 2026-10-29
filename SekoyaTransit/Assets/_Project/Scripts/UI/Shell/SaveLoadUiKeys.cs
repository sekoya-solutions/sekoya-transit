#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Localization keys (table <see cref="GameUiKeys.Table"/>) for the save and load panel and the Continue and Load
    /// buttons of the new game screen (Issue #183).
    /// </summary>
    public static class SaveLoadUiKeys
    {
        public const string Toggle = "save_load.toggle";

        public const string Title = "save_load.title";

        public const string Close = "save_load.close";

        public const string SlotAutosave = "save_load.slot.autosave";

        /// <summary>
        /// Template with the manual slot number as {0}.
        /// </summary>
        public const string SlotManual = "save_load.slot.manual";

        /// <summary>
        /// Template with the date as {0}, the seed as {1} and the tiles per side as {2}.
        /// </summary>
        public const string SlotInfo = "save_load.slot.info";

        public const string SlotEmpty = "save_load.slot.empty";

        public const string SlotUnreadable = "save_load.slot.unreadable";

        public const string Save = "save_load.save";

        public const string Load = "save_load.load";

        public const string ConfirmText = "save_load.confirm.text";

        public const string ConfirmYes = "save_load.confirm.yes";

        public const string ConfirmNo = "save_load.confirm.no";

        public const string StatusSaved = "save_load.status.saved";

        public const string StatusSaveFailed = "save_load.status.save_failed";

        public const string StatusLoadFailed = "save_load.status.load_failed";

        public const string StatusNotSupported = "save_load.status.not_supported";

        public const string StatusAutosaveFailed = "save_load.status.autosave_failed";

        public const string Continue = "new_game.continue";

        public const string LoadGame = "new_game.load";

        /// <summary>
        /// Keys loaded when the shell starts.
        /// </summary>
        public static readonly string[] StaticKeys =
        {
            Toggle,
            Title,
            Close,
            SlotAutosave,
            SlotManual,
            SlotInfo,
            SlotEmpty,
            SlotUnreadable,
            Save,
            Load,
            ConfirmText,
            ConfirmYes,
            ConfirmNo,
            StatusSaved,
            StatusSaveFailed,
            StatusLoadFailed,
            StatusNotSupported,
            StatusAutosaveFailed,
            Continue,
            LoadGame
        };

        /// <summary>
        /// Gets the message key of a status, or null for <see cref="SaveLoadStatus.None"/>.
        /// </summary>
        public static string? StatusKey(SaveLoadStatus status)
        {
            switch (status)
            {
                case SaveLoadStatus.Saved:
                    return StatusSaved;
                case SaveLoadStatus.SaveFailed:
                    return StatusSaveFailed;
                case SaveLoadStatus.LoadFailed:
                    return StatusLoadFailed;
                case SaveLoadStatus.NotSupported:
                    return StatusNotSupported;
                case SaveLoadStatus.AutosaveFailed:
                    return StatusAutosaveFailed;
                default:
                    return null;
            }
        }
    }
}
