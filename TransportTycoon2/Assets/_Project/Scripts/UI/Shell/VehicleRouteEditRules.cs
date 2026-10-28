#nullable enable
using System.Collections.Generic;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Pure rule for which vehicles the selected station lets the player route (Issue #181).
    /// </summary>
    public static class VehicleRouteEditRules
    {
        /// <summary>
        /// Minimum stops of a functional route; Core parks a vehicle with fewer.
        /// </summary>
        public const int MinFunctionalStops = 2;

        /// <summary>
        /// Returns whether the vehicle's route may be edited while the given station is selected: the station kind must
        /// match the vehicle kind, and the vehicle must be parked (no functional route) or have a route that uses
        /// the station.
        /// </summary>
        public static bool CanEditRoute(
            VehicleListKind vehicleKind,
            IReadOnlyList<int> routeStationIds,
            VehiclePurchaseSelection selectedStationKind,
            int? selectedStationId)
        {
            if (!selectedStationId.HasValue || !MatchesKind(vehicleKind, selectedStationKind))
            {
                return false;
            }

            if (routeStationIds.Count < MinFunctionalStops)
            {
                return true;
            }

            for (int index = 0; index < routeStationIds.Count; index++)
            {
                if (routeStationIds[index] == selectedStationId.Value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether a vehicle row is listed: every vehicle without a selected station, and only the routable
        /// ones (<paramref name="canEditRoute"/>) while a station is selected (Issue #193 preview feedback).
        /// </summary>
        public static bool IsListed(VehiclePurchaseSelection selectedStationKind, bool canEditRoute)
        {
            return selectedStationKind == VehiclePurchaseSelection.None || canEditRoute;
        }

        private static bool MatchesKind(VehicleListKind vehicleKind, VehiclePurchaseSelection stationKind)
        {
            return (vehicleKind == VehicleListKind.Road && stationKind == VehiclePurchaseSelection.Road)
                || (vehicleKind == VehicleListKind.Rail && stationKind == VehiclePurchaseSelection.Rail);
        }
    }
}
