#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Localization table and entry keys for the MapPlay shell (English-only in V1).
    /// </summary>
    public static class GameUiKeys
    {
        public const string Table = "GameUi";

        public const string Cash = "shell.cash";

        public const string CashTooltip = "shell.cash.tooltip";

        public const string Score = "shell.score";

        public const string CalendarDate = "calendar.date";

        public const string SpeedX1 = "calendar.speed.x1";

        public const string SpeedX2 = "calendar.speed.x2";

        public const string SpeedX3 = "calendar.speed.x3";

        public const string GameOverTitle = "game_over.title";

        public const string GameOverMessage = "game_over.message";

        public const string Hint = "shell.hint";

        public const string HintToolRoad = "shell.hint.tool.road";

        public const string HintToolRail = "shell.hint.tool.rail";

        public const string HintToolStation = "shell.hint.tool.station";

        public const string HintToolRailStation = "shell.hint.tool.rail_station";

        public const string HintToolDemolish = "shell.hint.tool.demolish";

        public const string ToolSelect = "toolbar.tool.select";

        public const string ToolRoad = "toolbar.tool.road";

        public const string ToolRail = "toolbar.tool.rail";

        public const string ToolStation = "toolbar.tool.station";

        public const string ToolRailStation = "toolbar.tool.rail_station";

        public const string ToolDemolish = "toolbar.tool.demolish";

        public const string VehiclesToggle = "vehicles.toggle";

        public const string UnlocksToggle = "unlocks.toggle";

        public const string VehiclesTitle = "vehicles.title";

        public const string UnlocksTitle = "unlocks.title";

        public const string UnlocksEmpty = "unlocks.empty";

        public const string VehiclesBuyHint = "vehicles.buy.hint";

        public const string VehiclesBuyOption = "vehicles.buy.option";

        public const string UnlocksRowFuture = "unlocks.row.future";

        public const string UnlocksRowUnlockable = "unlocks.row.unlockable";

        public const string UnlocksRowUnlocked = "unlocks.row.unlocked";

        public const string UnlocksAction = "unlocks.action";

        public const string UnlocksActionUnlocked = "unlocks.action.unlocked";

        public const string VehiclesSell = "vehicles.sell";

        public const string VehiclesAddWagon = "vehicles.add_wagon";

        public const string VehiclesSellWagon = "vehicles.sell_wagon";

        public const string VehiclesEmpty = "vehicles.empty";

        public const string VehiclesRow = "vehicles.row";

        public const string VehiclesRowRail = "vehicles.row.rail";

        public const string VehiclesKindRoad = "vehicles.kind.road";

        public const string VehiclesKindRail = "vehicles.kind.rail";

        public const string VehiclesStatusParked = "vehicles.status.parked";

        public const string VehiclesStatusMoving = "vehicles.status.moving";

        public const string VehiclesStatusLoading = "vehicles.status.loading";

        public const string VehiclesStatusStuck = "vehicles.status.stuck";

        public const string OrdersTitle = "vehicles.orders.title";

        public const string OrdersHint = "vehicles.orders.hint";

        public const string OrdersNoneSelected = "vehicles.orders.none_selected";

        public const string OrdersSelected = "vehicles.orders.selected";

        public const string OrdersEmpty = "vehicles.orders.empty";

        public const string OrdersStop = "vehicles.orders.stop";

        public const string OrdersUp = "vehicles.orders.up";

        public const string OrdersDown = "vehicles.orders.down";

        public const string OrdersRemove = "vehicles.orders.remove";

        public const string OrdersApply = "vehicles.orders.apply";

        public const string OrdersApplied = "vehicles.orders.applied";

        public const string OrdersRejected = "vehicles.orders.rejected";

        public const string IndustriesToggle = "industries.toggle";

        public const string IndustriesTitle = "industries.title";

        public const string IndustriesEmpty = "industries.empty";

        public const string IndustriesRow = "industries.row";

        public const string TownsToggle = "towns.toggle";

        public const string TownsTitle = "towns.title";

        public const string TownsEmpty = "towns.empty";

        public const string TownsSummary = "towns.summary";

        public const string TownsNeedLine = "towns.need_line";

        public const string TownsPassengerLine = "towns.passenger_line";

        public const string TownsMailLine = "towns.mail_line";

        public const string IndustryForest = "industry.forest";

        public const string IndustrySawmill = "industry.sawmill";

        public const string IndustryFurnitureFactory = "industry.furniture_factory";

        public const string IndustryWheatFarm = "industry.wheat_farm";

        public const string IndustryFlourMill = "industry.flour_mill";

        public const string IndustryBakery = "industry.bakery";

        public const string IndustryIronMine = "industry.iron_mine";

        public const string IndustryIronFoundry = "industry.iron_foundry";

        public const string IndustryMachineShop = "industry.machine_shop";

        public const string IndustryLivestockRanch = "industry.livestock_ranch";

        public const string IndustryButcher = "industry.butcher";

        public const string IndustryCannery = "industry.cannery";

        public const string IndustrySheepFarm = "industry.sheep_farm";

        public const string IndustryTextileMill = "industry.textile_mill";

        public const string IndustryClothingWorkshop = "industry.clothing_workshop";

        public const string VehicleTruckMk1 = "vehicle.truck_mk1";

        public const string VehicleTruckMk2 = "vehicle.truck_mk2";

        public const string VehicleTruckMk3 = "vehicle.truck_mk3";

        public const string VehicleTruckMk4 = "vehicle.truck_mk4";

        public const string VehicleTruckMk5 = "vehicle.truck_mk5";

        public const string VehicleLocoMk1 = "vehicle.loco_mk1";

        public const string VehicleLocoMk2 = "vehicle.loco_mk2";

        public const string VehicleLocoMk3 = "vehicle.loco_mk3";

        public const string VehicleLocoMk4 = "vehicle.loco_mk4";

        public const string VehicleRailWagon = "vehicle.rail_wagon";

        /// <summary>
        /// Content industry name keys shipped in the GameUi table (English V1).
        /// </summary>
        public static readonly string[] IndustryNameKeys =
        {
            IndustryForest,
            IndustrySawmill,
            IndustryFurnitureFactory,
            IndustryWheatFarm,
            IndustryFlourMill,
            IndustryBakery,
            IndustryIronMine,
            IndustryIronFoundry,
            IndustryMachineShop,
            IndustryLivestockRanch,
            IndustryButcher,
            IndustryCannery,
            IndustrySheepFarm,
            IndustryTextileMill,
            IndustryClothingWorkshop,
        };

        /// <summary>
        /// Content vehicle name keys shipped in the GameUi table (English V1).
        /// </summary>
        public static readonly string[] VehicleNameKeys =
        {
            VehicleTruckMk1,
            VehicleTruckMk2,
            VehicleTruckMk3,
            VehicleTruckMk4,
            VehicleTruckMk5,
            VehicleLocoMk1,
            VehicleLocoMk2,
            VehicleLocoMk3,
            VehicleLocoMk4,
            VehicleRailWagon,
        };

        /// <summary>
        /// Returns the localization key for a vehicle list status.
        /// </summary>
        public static string VehicleStatusKey(VehicleListStatusKind status)
        {
            switch (status)
            {
                case VehicleListStatusKind.Moving:
                    return VehiclesStatusMoving;
                case VehicleListStatusKind.Loading:
                    return VehiclesStatusLoading;
                case VehicleListStatusKind.Stuck:
                    return VehiclesStatusStuck;
                case VehicleListStatusKind.Parked:
                    return VehiclesStatusParked;
                default:
                    return VehiclesStatusParked;
            }
        }

        /// <summary>
        /// Returns the toolbar label key for a tool.
        /// </summary>
        public static string ToolLabelKey(BuildToolId tool)
        {
            switch (tool)
            {
                case BuildToolId.Select:
                    return ToolSelect;
                case BuildToolId.Road:
                    return ToolRoad;
                case BuildToolId.Rail:
                    return ToolRail;
                case BuildToolId.Station:
                    return ToolStation;
                case BuildToolId.RailStation:
                    return ToolRailStation;
                case BuildToolId.Demolish:
                    return ToolDemolish;
                default:
                    return ToolSelect;
            }
        }

        /// <summary>
        /// Returns the hint key for a tool. Select uses the general map hint.
        /// </summary>
        public static string HintKey(BuildToolId tool)
        {
            switch (tool)
            {
                case BuildToolId.Road:
                    return HintToolRoad;
                case BuildToolId.Rail:
                    return HintToolRail;
                case BuildToolId.Station:
                    return HintToolStation;
                case BuildToolId.RailStation:
                    return HintToolRailStation;
                case BuildToolId.Demolish:
                    return HintToolDemolish;
                case BuildToolId.Select:
                    return Hint;
                default:
                    return Hint;
            }
        }
    }
}
