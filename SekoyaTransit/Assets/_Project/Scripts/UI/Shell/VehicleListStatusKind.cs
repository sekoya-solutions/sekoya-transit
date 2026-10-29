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
    }
}
