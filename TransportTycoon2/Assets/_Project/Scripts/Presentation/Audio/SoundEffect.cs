#nullable enable

namespace TransportTycoon.Presentation.Audio
{
    /// <summary>
    /// The V1 sound effects (Issue #68); the clips are listed in CREDITS.md.
    /// </summary>
    public enum SoundEffect
    {
        /// <summary>
        /// A shell button press or a map selection.
        /// </summary>
        Click = 0,

        /// <summary>
        /// An accepted road, rail or station build.
        /// </summary>
        Build = 1,

        /// <summary>
        /// A Delete tool click that removed something.
        /// </summary>
        Demolish = 2,

        /// <summary>
        /// Delivery income was paid.
        /// </summary>
        CashIn = 3,
    }
}
