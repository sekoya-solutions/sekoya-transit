#nullable enable

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Classifies a vehicle catalog entry.
    /// </summary>
    public enum VehicleMode
    {
        /// <summary>
        /// Road truck.
        /// </summary>
        Road = 0,

        /// <summary>
        /// Rail locomotive (wagons attached separately).
        /// </summary>
        Rail = 1,

        /// <summary>
        /// Shared rail wagon entry (purchase/upkeep only).
        /// </summary>
        RailWagon = 2
    }
}
