#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// How well a town need was supplied last month; picks the need line color (Issue #242).
    /// </summary>
    public enum TownNeedLineStatus
    {
        /// <summary>
        /// Too little was delivered to count as partly met (default text color).
        /// </summary>
        Unmet = 0,

        /// <summary>
        /// Enough to count as partly met, but less than the demand, was delivered (blue).
        /// </summary>
        PartlyMet = 1,

        /// <summary>
        /// The demand was delivered (green).
        /// </summary>
        FullyMet = 2,
    }
}
