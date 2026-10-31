#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Localization keys (table <see cref="GameUiKeys.Table"/>) for the routes window and its station and vehicle
    /// windows (Issue #229).
    /// </summary>
    public static class RoutesUiKeys
    {
        public const string Toggle = "routes.toggle";

        public const string Title = "routes.title";

        public const string Close = "routes.close";

        public const string ColumnName = "routes.column.name";

        public const string ColumnRunningCost = "routes.column.running_cost";

        public const string ColumnRevenue = "routes.column.revenue";

        public const string ColumnProfit = "routes.column.profit";

        public const string ColumnStations = "routes.column.stations";

        public const string ColumnVehicles = "routes.column.vehicles";

        public const string Empty = "routes.empty";

        public const string NoValue = "routes.no_value";

        public const string Rename = "routes.rename";

        public const string Delete = "routes.delete";

        public const string EditStations = "routes.edit_stations";

        public const string EditVehicles = "routes.edit_vehicles";

        public const string NeedsFix = "routes.needs_fix";

        public const string Create = "routes.create";

        public const string NameCreateTitle = "routes.name.create_title";

        public const string NameRenameTitle = "routes.name.rename_title";

        public const string NameDefault = "routes.name.default";

        public const string NameOk = "routes.name.ok";

        public const string NameCancel = "routes.name.cancel";

        public const string NameErrorEmpty = "routes.name.error.empty";

        public const string NameErrorTooLong = "routes.name.error.too_long";

        public const string NameErrorTaken = "routes.name.error.taken";

        public const string DeleteConfirm = "routes.delete.confirm";

        public const string DeleteYes = "routes.delete.yes";

        public const string ConfirmNo = "routes.confirm.no";

        public const string StationsHeader = "route_stations.header";

        public const string StationsStop = "route_stations.stop";

        public const string StationsUp = "route_stations.up";

        public const string StationsDown = "route_stations.down";

        public const string StationsRemove = "route_stations.remove";

        public const string StationsWaitForFull = "route_stations.wait_for_full";

        public const string StationsHint = "route_stations.hint";

        public const string StationsEmpty = "route_stations.empty";

        public const string StationsModeMismatch = "route_stations.mode_mismatch";

        public const string StationsRepeated = "route_stations.repeated";

        public const string StationsRemoveConfirm = "route_stations.remove_confirm";

        public const string StationsRemoveYes = "route_stations.remove_yes";

        public const string VehiclesHeader = "route_vehicles.header";

        public const string VehiclesRemove = "route_vehicles.remove";

        public const string VehiclesAddButton = "route_vehicles.add_button";

        public const string VehiclesAvailable = "route_vehicles.available";

        public const string VehiclesAdd = "route_vehicles.add";

        public const string VehiclesBuy = "route_vehicles.buy";

        public const string VehiclesEmpty = "route_vehicles.empty";

        public const string VehiclesNoneAvailable = "route_vehicles.none_available";

        public const string VehiclesInvalidRoute = "route_vehicles.invalid_route";

        public const string VehicleLabel = "route_vehicles.label";

        public const string VehiclesStationHint = "vehicles.station_hint";

        public const string VehiclesManageRoutes = "vehicles.manage_routes";

        public const string VehiclesUnassignedHeader = "vehicles.unassigned_header";

        public const string VehiclesRoute = "vehicles.route";

        public const string VehiclesStatusUnassigned = "vehicles.status.unassigned";

        /// <summary>
        /// Keys loaded when the shell starts.
        /// </summary>
        public static readonly string[] StaticKeys =
        {
            GameUiKeys.CashTooltip,
            Toggle,
            Title,
            Close,
            ColumnName,
            ColumnRunningCost,
            ColumnRevenue,
            ColumnProfit,
            ColumnStations,
            ColumnVehicles,
            Empty,
            NoValue,
            Rename,
            Delete,
            EditStations,
            EditVehicles,
            NeedsFix,
            Create,
            NameCreateTitle,
            NameRenameTitle,
            NameDefault,
            NameOk,
            NameCancel,
            NameErrorEmpty,
            NameErrorTooLong,
            NameErrorTaken,
            DeleteConfirm,
            DeleteYes,
            ConfirmNo,
            StationsHeader,
            StationsStop,
            StationsUp,
            StationsDown,
            StationsRemove,
            StationsWaitForFull,
            StationsHint,
            StationsEmpty,
            StationsModeMismatch,
            StationsRepeated,
            StationsRemoveConfirm,
            StationsRemoveYes,
            VehiclesHeader,
            VehiclesRemove,
            VehiclesAddButton,
            VehiclesAvailable,
            VehiclesAdd,
            VehiclesBuy,
            VehiclesEmpty,
            VehiclesNoneAvailable,
            VehiclesInvalidRoute,
            VehicleLabel,
            VehiclesStationHint,
            VehiclesManageRoutes,
            VehiclesUnassignedHeader,
            VehiclesRoute,
            VehiclesStatusUnassigned
        };
    }
}
