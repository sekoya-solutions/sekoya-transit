#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Display status for a row in the MapPlay vehicles panel (Issue #51).
    /// </summary>
    public enum VehicleListStatusKind
    {
        Parked = 0,
        Moving = 1,
        Loading = 2,
        Stuck = 3,

        /// <summary>
        /// Owned but not on a route, so off the map (Issue #229).
        /// </summary>
        Unassigned = 4,
    }
}
