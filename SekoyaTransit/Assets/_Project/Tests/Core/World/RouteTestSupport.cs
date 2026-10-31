#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    /// <summary>
    /// Puts vehicles on routes through the production commands (Issue #229), for tests that need a running vehicle.
    /// </summary>
    internal static class RouteTestSupport
    {
        /// <summary>
        /// Creates a catalog with only the placeholder road and rail vehicles.
        /// </summary>
        public static ContentCatalog CreatePlaceholderCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }

        /// <summary>
        /// Creates a route with the given stops and returns its id.
        /// </summary>
        public static int CreateRoute(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            RouteMode mode,
            IReadOnlyList<int> stationIds,
            IReadOnlyList<bool>? waitForFull = null)
        {
            int routeId = world.NextRouteId;
            Accept(processor, new CreateRouteCommand("Route " + routeId));
            for (int index = 0; index < stationIds.Count; index++)
            {
                Accept(processor, new AddRouteStopCommand(routeId, mode, stationIds[index]));
                if (waitForFull != null && waitForFull[index])
                {
                    Accept(processor, new SetRouteStopWaitForFullCommand(routeId, index, true));
                }
            }

            return routeId;
        }

        /// <summary>
        /// Creates a road route with <paramref name="orders"/> and buys one truck on it; returns the truck id.
        /// The truck starts empty at the first stop.
        /// </summary>
        public static int PlaceRoadVehicle(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            IReadOnlyList<RoadVehicleOrder> orders,
            ContentCatalog? catalog = null,
            string? typeId = null,
            int calendarYear = GameDate.FirstYear)
        {
            ContentCatalog resolved = catalog ?? CreatePlaceholderCatalog();
            var stationIds = new int[orders.Count];
            var waits = new bool[orders.Count];
            for (int index = 0; index < orders.Count; index++)
            {
                stationIds[index] = orders[index].StationId;
                waits[index] = orders[index].WaitForFull;
            }

            int routeId = CreateRoute(processor, world, RouteMode.Road, stationIds, waits);
            return BuyOnRoute(processor, world, routeId, resolved, typeId ?? resolved.StartingRoadVehicle.Id, calendarYear);
        }

        /// <summary>
        /// Creates a road route through <paramref name="stationIds"/> and buys one truck on it; returns the truck id.
        /// </summary>
        public static int PlaceRoadVehicle(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            params int[] stationIds)
        {
            var orders = new RoadVehicleOrder[stationIds.Length];
            for (int index = 0; index < stationIds.Length; index++)
            {
                orders[index] = new RoadVehicleOrder(stationIds[index]);
            }

            return PlaceRoadVehicle(processor, world, orders);
        }

        /// <summary>
        /// Creates a rail route with <paramref name="orders"/> and buys one train (locomotive and one wagon) on it;
        /// returns the train id. The train starts empty at the first stop.
        /// </summary>
        public static int PlaceRailVehicle(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            IReadOnlyList<RailVehicleOrder> orders,
            ContentCatalog? catalog = null,
            string? typeId = null,
            int calendarYear = GameDate.FirstYear)
        {
            ContentCatalog resolved = catalog ?? CreatePlaceholderCatalog();
            var stationIds = new int[orders.Count];
            var waits = new bool[orders.Count];
            for (int index = 0; index < orders.Count; index++)
            {
                stationIds[index] = orders[index].StationId;
                waits[index] = orders[index].WaitForFull;
            }

            int routeId = CreateRoute(processor, world, RouteMode.Rail, stationIds, waits);
            return BuyOnRoute(processor, world, routeId, resolved, typeId ?? resolved.StartingRailLocomotive.Id, calendarYear);
        }

        /// <summary>
        /// Creates a rail route through <paramref name="stationIds"/> and buys one train on it; returns the train id.
        /// </summary>
        public static int PlaceRailVehicle(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            params int[] stationIds)
        {
            var orders = new RailVehicleOrder[stationIds.Length];
            for (int index = 0; index < stationIds.Length; index++)
            {
                orders[index] = new RailVehicleOrder(stationIds[index]);
            }

            return PlaceRailVehicle(processor, world, orders);
        }

        /// <summary>
        /// Creates a road route through <paramref name="stationIds"/> and buys one truck from <paramref name="catalog"/>.
        /// </summary>
        public static int PlaceRoadVehicle(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            ContentCatalog catalog,
            params int[] stationIds)
        {
            var orders = new RoadVehicleOrder[stationIds.Length];
            for (int index = 0; index < stationIds.Length; index++)
            {
                orders[index] = new RoadVehicleOrder(stationIds[index]);
            }

            return PlaceRoadVehicle(processor, world, orders, catalog);
        }

        /// <summary>
        /// Creates a rail route through <paramref name="stationIds"/> and buys one train from <paramref name="catalog"/>.
        /// </summary>
        public static int PlaceRailVehicle(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            ContentCatalog catalog,
            params int[] stationIds)
        {
            var orders = new RailVehicleOrder[stationIds.Length];
            for (int index = 0; index < stationIds.Length; index++)
            {
                orders[index] = new RailVehicleOrder(stationIds[index]);
            }

            return PlaceRailVehicle(processor, world, orders, catalog);
        }

        /// <summary>
        /// Buys a vehicle on an existing route and returns its id.
        /// </summary>
        public static int BuyOnRoute(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            int routeId,
            ContentCatalog catalog,
            string typeId,
            int calendarYear = GameDate.FirstYear)
        {
            Assert.That(world.TryGetRoute(routeId, out Route? route), Is.True);
            int before = route!.Mode == RouteMode.Road ? world.RoadVehicles.Count : world.RailVehicles.Count;
            Accept(processor, new BuyVehicleForRouteCommand(routeId, catalog, typeId, calendarYear));
            if (route.Mode == RouteMode.Road)
            {
                Assert.That(world.RoadVehicles.Count, Is.EqualTo(before + 1));
                return FindNewest(world.RoadVehicles);
            }

            Assert.That(world.RailVehicles.Count, Is.EqualTo(before + 1));
            return FindNewest(world.RailVehicles);
        }

        /// <summary>
        /// Processes a command and asserts it was accepted.
        /// </summary>
        public static void Accept(SimulationCommandProcessor<GeneratedWorld> processor, ISimulationCommand<GeneratedWorld> command)
        {
            CommandProcessingResult result = processor.Process(new Tick(0), command);
            Assert.That(result.IsAccepted, Is.True, result.RejectionReason);
        }

        private static int FindNewest(IReadOnlyList<RoadVehicle> vehicles)
        {
            int newest = -1;
            for (int index = 0; index < vehicles.Count; index++)
            {
                newest = Math.Max(newest, vehicles[index].Id);
            }

            return newest;
        }

        private static int FindNewest(IReadOnlyList<RailVehicle> vehicles)
        {
            int newest = -1;
            for (int index = 0; index < vehicles.Count; index++)
            {
                newest = Math.Max(newest, vehicles[index].Id);
            }

            return newest;
        }
    }
}
