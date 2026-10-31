#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// How well a town need was supplied last month; picks the need line color (Issue #242).
    /// </summary>
    public enum TownNeedLineStatus
    {
        /// <summary>
        /// Nothing was delivered (default text color).
        /// </summary>
        Unmet = 0,

        /// <summary>
        /// Some but less than the demand was delivered (blue).
        /// </summary>
        PartlyMet = 1,

        /// <summary>
        /// The demand was delivered (green).
        /// </summary>
        FullyMet = 2,
    }
}
