#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Why a route name was refused (Issue #229).
    /// </summary>
    public enum RouteNameError
    {
        None = 0,
        Empty = 1,
        TooLong = 2,
        Taken = 3
    }

    /// <summary>
    /// The outcome of clicking a station on the map while a route's station window is open (Issue #229).
    /// </summary>
    public enum RouteStopAddResult
    {
        Added = 0,
        ModeMismatch = 1,
        Repeated = 2,
        Refused = 3
    }

    /// <summary>
    /// Data providers and actions the routes windows use; Presentation turns the actions into Core commands
    /// (Issue #229).
    /// </summary>
    public sealed class RoutesBindings
    {
        public RoutesBindings(
            Func<IReadOnlyList<RouteListItem>> routes,
            Func<int, RouteDetail?> detail,
            Func<string, RouteNameError> createRoute,
            Func<int, string, RouteNameError> renameRoute,
            Action<int> deleteRoute,
            Action<int, int> removeStop,
            Action<int, int, bool> moveStop,
            Action<int, int, bool> setWaitForFull,
            Action<int, int> assignVehicle,
            Action<int, int> unassignVehicle,
            Action<int, string> buyVehicle)
        {
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
            CreateRoute = createRoute ?? throw new ArgumentNullException(nameof(createRoute));
            RenameRoute = renameRoute ?? throw new ArgumentNullException(nameof(renameRoute));
            DeleteRoute = deleteRoute ?? throw new ArgumentNullException(nameof(deleteRoute));
            RemoveStop = removeStop ?? throw new ArgumentNullException(nameof(removeStop));
            MoveStop = moveStop ?? throw new ArgumentNullException(nameof(moveStop));
            SetWaitForFull = setWaitForFull ?? throw new ArgumentNullException(nameof(setWaitForFull));
            AssignVehicle = assignVehicle ?? throw new ArgumentNullException(nameof(assignVehicle));
            UnassignVehicle = unassignVehicle ?? throw new ArgumentNullException(nameof(unassignVehicle));
            BuyVehicle = buyVehicle ?? throw new ArgumentNullException(nameof(buyVehicle));
        }

        /// <summary>
        /// Gets the routes in id order.
        /// </summary>
        public Func<IReadOnlyList<RouteListItem>> Routes { get; }

        /// <summary>
        /// Gets the detail of a route, or null when it no longer exists.
        /// </summary>
        public Func<int, RouteDetail?> Detail { get; }

        /// <summary>
        /// Gets the create action (typed name); returns why the name was refused, or <see cref="RouteNameError.None"/>.
        /// </summary>
        public Func<string, RouteNameError> CreateRoute { get; }

        /// <summary>
        /// Gets the rename action (route id, typed name).
        /// </summary>
        public Func<int, string, RouteNameError> RenameRoute { get; }

        public Action<int> DeleteRoute { get; }

        /// <summary>
        /// Gets the remove-stop action (route id, stop index).
        /// </summary>
        public Action<int, int> RemoveStop { get; }

        /// <summary>
        /// Gets the move-stop action (route id, stop index, up).
        /// </summary>
        public Action<int, int, bool> MoveStop { get; }

        /// <summary>
        /// Gets the wait-for-full action (route id, stop index, wait).
        /// </summary>
        public Action<int, int, bool> SetWaitForFull { get; }

        /// <summary>
        /// Gets the add action for an unassigned vehicle (route id, vehicle id).
        /// </summary>
        public Action<int, int> AssignVehicle { get; }

        /// <summary>
        /// Gets the remove action for a vehicle of the route (route id, vehicle id).
        /// </summary>
        public Action<int, int> UnassignVehicle { get; }

        /// <summary>
        /// Gets the buy action (route id, vehicle type id).
        /// </summary>
        public Action<int, string> BuyVehicle { get; }
    }
}
