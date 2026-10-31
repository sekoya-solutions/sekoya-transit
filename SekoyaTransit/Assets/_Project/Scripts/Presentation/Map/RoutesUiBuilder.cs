#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Builds the routes window data from the Core world and turns its actions into Core commands (Issue #229).
    /// </summary>
    public static class RoutesUiBuilder
    {
        /// <summary>
        /// Creates the bindings the routes windows use. <paramref name="calendarYear"/> gives the current year for
        /// vehicle availability.
        /// </summary>
        public static RoutesBindings CreateBindings(
            GeneratedWorld world,
            ContentCatalog catalog,
            SimulationCommandProcessor<GeneratedWorld> processor,
            Func<int> calendarYear)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            if (calendarYear == null)
            {
                throw new ArgumentNullException(nameof(calendarYear));
            }
#pragma warning restore CA1510

            return new RoutesBindings(
                () => BuildRouteItems(world),
                routeId => BuildDetail(world, catalog, routeId, calendarYear()),
                name => CreateRoute(world, processor, name),
                (routeId, name) => RenameRoute(world, processor, routeId, name),
                routeId => Process(processor, new DeleteRouteCommand(routeId)),
                (routeId, stopIndex) => Process(processor, new RemoveRouteStopCommand(routeId, stopIndex)),
                (routeId, stopIndex, up) => Process(processor, new MoveRouteStopCommand(routeId, stopIndex, up)),
                (routeId, stopIndex, wait) => Process(processor, new SetRouteStopWaitForFullCommand(routeId, stopIndex, wait)),
                (routeId, vehicleId) => Process(processor, new AssignVehicleToRouteCommand(routeId, vehicleId)),
                (routeId, vehicleId) => Process(processor, new UnassignVehicleCommand(routeId, vehicleId)),
                (routeId, typeId) => Process(
                    processor,
                    new BuyVehicleForRouteCommand(routeId, catalog, typeId, calendarYear())));
        }

        /// <summary>
        /// Appends a clicked station to a route and reports why it was refused, if it was.
        /// </summary>
        public static RouteStopAddResult AppendStation(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            int routeId,
            VehicleListKind stationKind,
            int stationId)
        {
            RouteMode mode = stationKind == VehicleListKind.Rail ? RouteMode.Rail : RouteMode.Road;
            RouteEditRefusal refusal = RouteRules.CheckAppendStop(world, routeId, mode, stationId);
            switch (refusal)
            {
                case RouteEditRefusal.None:
                    return processor.Process(Tick.Zero, new AddRouteStopCommand(routeId, mode, stationId)).IsAccepted
                        ? RouteStopAddResult.Added
                        : RouteStopAddResult.Refused;
                case RouteEditRefusal.ModeMismatch:
                    return RouteStopAddResult.ModeMismatch;
                case RouteEditRefusal.RepeatedStop:
                    return RouteStopAddResult.Repeated;
                default:
                    return RouteStopAddResult.Refused;
            }
        }

        /// <summary>
        /// Returns the ids of the routes that stop at a road or rail station.
        /// </summary>
        public static IReadOnlyCollection<int> FindRoutesStoppingAt(GeneratedWorld world, VehicleListKind stationKind, int stationId)
        {
            RouteMode mode = stationKind == VehicleListKind.Rail ? RouteMode.Rail : RouteMode.Road;
            var routeIds = new List<int>();
            for (int index = 0; index < world.Routes.Count; index++)
            {
                Route route = world.Routes[index];
                if (route.Mode == mode && StopsAt(route, stationId))
                {
                    routeIds.Add(route.Id);
                }
            }

            return routeIds;
        }

        /// <summary>
        /// Returns the name of a route, or null for <see cref="RoadVehicle.NoRouteId"/> or a missing route.
        /// </summary>
        public static string? FindRouteName(GeneratedWorld world, int routeId)
        {
            return world.TryGetRoute(routeId, out Route? route) && route != null ? route.Name : null;
        }

        /// <summary>
        /// Returns whether <paramref name="route"/> has a stop at <paramref name="stationId"/>.
        /// </summary>
        public static bool StopsAt(Route route, int stationId)
        {
            for (int stop = 0; stop < route.Stops.Count; stop++)
            {
                if (route.Stops[stop].StationId == stationId)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<RouteListItem> BuildRouteItems(GeneratedWorld world)
        {
            var items = new RouteListItem[world.Routes.Count];
            for (int index = 0; index < items.Length; index++)
            {
                Route route = world.Routes[index];
                items[index] = new RouteListItem(
                    route.Id,
                    route.Name,
                    route.IsValid,
                    route.HasLastMonth,
                    route.LastMonthRunningCost,
                    route.LastMonthRevenue,
                    route.Stops.Count,
                    RouteRules.CountVehicles(world, route.Id));
            }

            return items;
        }

        private static RouteDetail? BuildDetail(GeneratedWorld world, ContentCatalog catalog, int routeId, int calendarYear)
        {
            if (!world.TryGetRoute(routeId, out Route? route) || route == null)
            {
                return null;
            }

            var stops = new RouteStopItem[route.Stops.Count];
            for (int index = 0; index < stops.Length; index++)
            {
                stops[index] = new RouteStopItem(
                    route.Stops[index].StationId,
                    route.Stops[index].WaitForFull,
                    RouteRules.CheckMoveStop(world, routeId, index, up: true) == RouteEditRefusal.None,
                    RouteRules.CheckMoveStop(world, routeId, index, up: false) == RouteEditRefusal.None,
                    RouteRules.RemovingStopUnassignsVehicles(world, routeId, index));
            }

            var vehicles = new List<RouteVehicleItem>();
            var unassigned = new List<RouteVehicleItem>();
            IReadOnlyList<VehiclePurchaseOption> buyOptions = Array.Empty<VehiclePurchaseOption>();
            if (route.Mode == RouteMode.Road)
            {
                AddRoadVehicles(world.RoadVehicles, routeId, catalog, vehicles);
                AddRoadVehicles(world.UnassignedRoadVehicles, RoadVehicle.NoRouteId, catalog, unassigned);
                buyOptions = BuildBuyOptions(world, catalog, VehicleMode.Road, calendarYear);
            }
            else if (route.Mode == RouteMode.Rail)
            {
                AddRailVehicles(world.RailVehicles, routeId, catalog, vehicles);
                AddRailVehicles(world.UnassignedRailVehicles, RoadVehicle.NoRouteId, catalog, unassigned);
                buyOptions = BuildBuyOptions(world, catalog, VehicleMode.Rail, calendarYear);
            }

            return new RouteDetail(
                route.Id,
                route.Name,
                route.Mode == RouteMode.Rail ? RouteUiMode.Rail : route.Mode == RouteMode.Road ? RouteUiMode.Road : RouteUiMode.None,
                route.IsValid,
                stops,
                vehicles,
                unassigned,
                buyOptions);
        }

        private static void AddRoadVehicles(
            IReadOnlyList<RoadVehicle> source,
            int routeId,
            ContentCatalog catalog,
            List<RouteVehicleItem> target)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].RouteId == routeId)
                {
                    target.Add(new RouteVehicleItem(source[index].Id, NameKey(catalog, source[index].TypeId)));
                }
            }
        }

        private static void AddRailVehicles(
            IReadOnlyList<RailVehicle> source,
            int routeId,
            ContentCatalog catalog,
            List<RouteVehicleItem> target)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].RouteId == routeId)
                {
                    target.Add(new RouteVehicleItem(source[index].Id, NameKey(catalog, source[index].TypeId)));
                }
            }
        }

        private static string NameKey(ContentCatalog catalog, string typeId)
        {
            return catalog.TryGetVehicle(typeId, out VehicleDefinition? definition) && definition != null
                ? definition.NameKey
                : typeId;
        }

        // Unlocked types of the route's mode available this year, oldest first, priced as bought for a route (a
        // locomotive comes with one wagon).
        private static IReadOnlyList<VehiclePurchaseOption> BuildBuyOptions(
            GeneratedWorld world,
            ContentCatalog catalog,
            VehicleMode mode,
            int calendarYear)
        {
            var matches = new List<VehicleDefinition>();
            for (int index = 0; index < catalog.Vehicles.Count; index++)
            {
                VehicleDefinition definition = catalog.Vehicles[index];
                if (definition.Mode == mode && VehicleAvailability.CanPurchase(definition, world, calendarYear))
                {
                    matches.Add(definition);
                }
            }

            matches.Sort((left, right) =>
            {
                int year = left.AvailableYear.CompareTo(right.AvailableYear);
                return year != 0 ? year : string.CompareOrdinal(left.Id, right.Id);
            });
            var options = new VehiclePurchaseOption[matches.Count];
            for (int index = 0; index < options.Length; index++)
            {
                VehicleDefinition definition = matches[index];
                long price = BuyVehicleForRouteCommand.GetPrice(catalog, definition);
                options[index] = new VehiclePurchaseOption(
                    definition.Id,
                    definition.NameKey,
                    price,
                    world.CompanyCash >= price);
            }

            return options;
        }

        private static RouteNameError CreateRoute(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            string name)
        {
            RouteNameError error = ToNameError(RouteRules.CheckName(world, name, RoadVehicle.NoRouteId, out _));
            if (error == RouteNameError.None)
            {
                processor.Process(Tick.Zero, new CreateRouteCommand(name));
            }

            return error;
        }

        private static RouteNameError RenameRoute(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            int routeId,
            string name)
        {
            RouteNameError error = ToNameError(RouteRules.CheckName(world, name, routeId, out _));
            if (error == RouteNameError.None)
            {
                processor.Process(Tick.Zero, new RenameRouteCommand(routeId, name));
            }

            return error;
        }

        private static RouteNameError ToNameError(RouteEditRefusal refusal)
        {
            switch (refusal)
            {
                case RouteEditRefusal.EmptyName:
                    return RouteNameError.Empty;
                case RouteEditRefusal.NameTooLong:
                    return RouteNameError.TooLong;
                case RouteEditRefusal.NameTaken:
                    return RouteNameError.Taken;
                default:
                    return RouteNameError.None;
            }
        }

        // The windows run every action as a Core command; a rejected command (a stale click) changes nothing.
        private static void Process(SimulationCommandProcessor<GeneratedWorld> processor, ISimulationCommand<GeneratedWorld> command)
        {
            processor.Process(Tick.Zero, command);
        }
    }
}
