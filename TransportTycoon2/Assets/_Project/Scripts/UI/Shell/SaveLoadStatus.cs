#nullable enable

namespace TransportTycoon.UI.Shell
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
        AutosaveFailed
    }
}
