#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// MapPlay construction tools exposed by the build toolbar (Issue #50, #181).
    /// </summary>
    public enum BuildToolId
    {
        Select = 0,
        Road = 1,
        Station = 2,
        Rail = 3,
        RailStation = 4,

        /// <summary>
        /// Removes the station on a tile, otherwise the road and the rail on it.
        /// </summary>
        Demolish = 5,
    }
}
