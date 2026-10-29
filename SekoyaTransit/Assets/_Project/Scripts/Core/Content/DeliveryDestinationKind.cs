#nullable enable

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// What a delivery was unloaded at. <see cref="Any"/> is only valid as an achievement filter,
    /// never as a recorded counter.
    /// </summary>
    public enum DeliveryDestinationKind
    {
        /// <summary>
        /// A town-adjacent station.
        /// </summary>
        Town = 0,

        /// <summary>
        /// An industry-adjacent station.
        /// </summary>
        Industry = 1,

        /// <summary>
        /// Either a town or an industry (achievement filter only).
        /// </summary>
        Any = 2
    }
}
