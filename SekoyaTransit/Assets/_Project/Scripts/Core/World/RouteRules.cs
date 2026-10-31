#nullable enable

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Why a route edit is refused; the commands and the routes UI share these rules (Issue #229).
    /// </summary>
    public enum RouteEditRefusal
    {
        /// <summary>
        /// The edit is allowed.
        /// </summary>
        None = 0,

        /// <summary>
        /// The route does not exist.
        /// </summary>
        MissingRoute,

        /// <summary>
        /// The name is empty after trimming.
        /// </summary>
        EmptyName,

        /// <summary>
        /// The name is longer than <see cref="Route.MaxNameLength"/> after trimming.
        /// </summary>
        NameTooLong,

        /// <summary>
        /// Another route already uses the name, ignoring case.
        /// </summary>
        NameTaken,

        /// <summary>
        /// The station does not exist.
        /// </summary>
        MissingStation,

        /// <summary>
        /// The station's mode differs from the route's mode (road and rail stations cannot be mixed).
        /// </summary>
        ModeMismatch,

        /// <summary>
        /// The edit would put the same station twice in a row.
        /// </summary>
        RepeatedStop,

        /// <summary>
        /// The stop index is outside the stop list, or the stop cannot move further in that direction.
        /// </summary>
        InvalidStop
    }

    /// <summary>
    /// Validation rules for route names and stop edits (Issue #229).
    /// </summary>
    public static class RouteRules
    {
        /// <summary>
        /// Trims a player-typed name and checks it is non-empty, short enough and not used by another route.
        /// </summary>
        public static RouteEditRefusal CheckName(
            GeneratedWorld world,
            string? input,
            int exceptRouteId,
            out string name)
        {
            name = (input ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return RouteEditRefusal.EmptyName;
            }

            if (name.Length > Route.MaxNameLength)
            {
                return RouteEditRefusal.NameTooLong;
            }

            return world.IsRouteNameTaken(name, exceptRouteId) ? RouteEditRefusal.NameTaken : RouteEditRefusal.None;
        }

        /// <summary>
        /// Checks that a station of <paramref name="mode"/> can be appended to the route.
        /// </summary>
        public static RouteEditRefusal CheckAppendStop(GeneratedWorld world, int routeId, RouteMode mode, int stationId)
        {
            if (!world.TryGetRoute(routeId, out Route? route) || route == null)
            {
                return RouteEditRefusal.MissingRoute;
            }

            bool stationExists = mode == RouteMode.Road
                ? world.TryGetRoadStation(stationId, out _)
                : mode == RouteMode.Rail && world.TryGetRailStation(stationId, out _);
            if (!stationExists)
            {
                return RouteEditRefusal.MissingStation;
            }

            if (route.Mode != RouteMode.None && route.Mode != mode)
            {
                return RouteEditRefusal.ModeMismatch;
            }

            if (route.Stops.Count > 0 && route.Stops[route.Stops.Count - 1].StationId == stationId)
            {
                return RouteEditRefusal.RepeatedStop;
            }

            return RouteEditRefusal.None;
        }

        /// <summary>
        /// Checks that the stop at <paramref name="stopIndex"/> can move one place up (towards the first stop) or down.
        /// </summary>
        public static RouteEditRefusal CheckMoveStop(GeneratedWorld world, int routeId, int stopIndex, bool up)
        {
            if (!world.TryGetRoute(routeId, out Route? route) || route == null)
            {
                return RouteEditRefusal.MissingRoute;
            }

            int other = up ? stopIndex - 1 : stopIndex + 1;
            if (stopIndex < 0 || stopIndex >= route.Stops.Count || other < 0 || other >= route.Stops.Count)
            {
                return RouteEditRefusal.InvalidStop;
            }

            int low = up ? other : stopIndex;
            int high = low + 1;

            // After swapping low and high, the stop now at low meets low - 1 and the stop now at high meets high + 1.
            if ((low > 0 && route.Stops[low - 1].StationId == route.Stops[high].StationId) ||
                (high < route.Stops.Count - 1 && route.Stops[high + 1].StationId == route.Stops[low].StationId))
            {
                return RouteEditRefusal.RepeatedStop;
            }

            return RouteEditRefusal.None;
        }

        /// <summary>
        /// Returns whether removing the stop at <paramref name="stopIndex"/> would leave the route invalid while
        /// vehicles run on it (the UI asks for confirmation first).
        /// </summary>
        public static bool RemovingStopUnassignsVehicles(GeneratedWorld world, int routeId, int stopIndex)
        {
            if (!world.TryGetRoute(routeId, out Route? route) || route == null || !route.IsValid ||
                stopIndex < 0 || stopIndex >= route.Stops.Count)
            {
                return false;
            }

            int remaining = route.Stops.Count - 1;
            if (stopIndex > 0 && stopIndex < route.Stops.Count - 1 &&
                route.Stops[stopIndex - 1].StationId == route.Stops[stopIndex + 1].StationId)
            {
                remaining--;
            }

            return remaining < Route.MinValidStops && CountVehicles(world, routeId) > 0;
        }

        /// <summary>
        /// Counts the vehicles running on a route.
        /// </summary>
        public static int CountVehicles(GeneratedWorld world, int routeId)
        {
            int count = 0;
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                if (world.RoadVehicles[index].RouteId == routeId)
                {
                    count++;
                }
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                if (world.RailVehicles[index].RouteId == routeId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
