#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The section of the achievements window a track is shown in (Issue #223).
    /// </summary>
    public enum AchievementSection
    {
        /// <summary>
        /// The number of achievements unlocked.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Company goals: money, contracts, fleet, distance, towns.
        /// </summary>
        Company = 1,

        /// <summary>
        /// One cargo-sold track per resource.
        /// </summary>
        Resources = 2
    }
}
