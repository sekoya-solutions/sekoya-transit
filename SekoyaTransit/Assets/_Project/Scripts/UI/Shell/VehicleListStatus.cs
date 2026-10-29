#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Pure helpers that map vehicle runtime flags to list-row status (Issue #51).
    /// </summary>
    public static class VehicleListStatus
    {
        /// <summary>
        /// Resolves status from Core-equivalent flags. Stuck wins over servicing and movement.
        /// </summary>
        public static VehicleListStatusKind Resolve(bool isStuck, bool isOnEdge, bool isServicingStop)
        {
            if (isStuck)
            {
                return VehicleListStatusKind.Stuck;
            }

            if (isServicingStop)
            {
                return VehicleListStatusKind.Loading;
            }

            if (isOnEdge)
            {
                return VehicleListStatusKind.Moving;
            }

            return VehicleListStatusKind.Parked;
        }
    }
}
