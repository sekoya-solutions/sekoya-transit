#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The outcome of the last save or load, shown at the bottom of the save and load panel (Issue #183).
    /// </summary>
    public enum SaveLoadStatus
    {
        None,
        Saved,
        SaveFailed,
        LoadFailed,

        /// <summary>
        /// The save is corrupt or was written by a newer version of the game.
        /// </summary>
        NotSupported,

        /// <summary>
        /// The save was written by an older version of the game that is no longer loaded (Issue #229).
        /// </summary>
        TooOld,
        AutosaveFailed
    }
}
