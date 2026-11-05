#nullable enable

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// How well a town need was supplied over a month (Issue #242).
    /// </summary>
    public enum TownNeedStatus
    {
        /// <summary>
        /// Less than the partly met share of the monthly demand was delivered (Issue #310).
        /// </summary>
        Unmet = 0,

        /// <summary>
        /// At least the partly met share of the monthly demand, but less than all of it, was delivered (Issue #310).
        /// </summary>
        PartlyMet = 1,

        /// <summary>
        /// The monthly demand or more was delivered.
        /// </summary>
        FullyMet = 2,
    }
}
