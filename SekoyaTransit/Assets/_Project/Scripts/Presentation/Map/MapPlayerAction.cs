#nullable enable

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// A map click the player made that had an effect (Issue #68); Presentation plays feedback such as a sound for it.
    /// </summary>
    public enum MapPlayerAction
    {
        /// <summary>
        /// A tile was clicked with the Select tool (selected, deselected, or added to a route being edited).
        /// </summary>
        Selected = 0,

        /// <summary>
        /// A road, rail, road station or rail station build command was accepted.
        /// </summary>
        Built = 1,

        /// <summary>
        /// A Delete tool click removed at least one station, road or rail piece.
        /// </summary>
        Demolished = 2,
    }
}
