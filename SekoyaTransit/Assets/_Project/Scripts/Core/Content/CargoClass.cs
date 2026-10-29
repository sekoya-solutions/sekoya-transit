#nullable enable

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// The gameplay class of a cargo definition.
    /// </summary>
    public enum CargoClass
    {
        /// <summary>
        /// Cargo representing people transported by the player.
        /// </summary>
        Passengers,

        /// <summary>
        /// Non-passenger cargo transported by the player.
        /// </summary>
        Cargo
    }
}
