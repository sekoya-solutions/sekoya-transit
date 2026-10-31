#nullable enable

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// How well a town need was supplied over a month (Issue #242).
    /// </summary>
    public enum TownNeedStatus
    {
        /// <summary>
        /// Nothing was delivered.
        /// </summary>
        Unmet = 0,

        /// <summary>
        /// At least one unit but less than the monthly demand was delivered.
        /// </summary>
        PartlyMet = 1,

        /// <summary>
        /// The monthly demand or more was delivered.
        /// </summary>
        FullyMet = 2,
    }
}
