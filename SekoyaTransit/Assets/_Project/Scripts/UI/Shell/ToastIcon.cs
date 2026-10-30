#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The icon shown before a toast's text (Issue #223): none, or the trophy of an unlocked achievement milestone.
    /// </summary>
    public enum ToastIcon
    {
        /// <summary>
        /// Text only.
        /// </summary>
        None = 0,

        /// <summary>
        /// Bronze trophy.
        /// </summary>
        TrophyBronze = 1,

        /// <summary>
        /// Silver trophy.
        /// </summary>
        TrophySilver = 2,

        /// <summary>
        /// Gold trophy.
        /// </summary>
        TrophyGold = 3
    }
}
