#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Display status for one vehicle type on the unlocks panel (Issue #63).
    /// </summary>
    public enum VehicleUnlockRowStatus
    {
        /// <summary>
        /// Calendar year has not reached the type's available year.
        /// </summary>
        Future = 0,

        /// <summary>
        /// Year reached; type can be permanently unlocked with money.
        /// </summary>
        Unlockable = 1,

        /// <summary>
        /// Starting type or already permanently unlocked.
        /// </summary>
        Unlocked = 2,
    }
}
