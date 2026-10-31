#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class VehicleUpkeepTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [TestCase(0L)]
        [TestCase(1L)]
        [TestCase(7L)]
        [TestCase(1000L)]
        [TestCase(12345L)]
        public void GetDailyShare_EveryMonth_SumsToMonthlyUpkeep(long monthlyUpkeep)
        {
            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = new GameDate(1900, month, 1).DaysInMonth;
                long total = 0L;
                for (int day = 1; day <= daysInMonth; day++)
                {
                    long share = VehicleUpkeep.GetDailyShare(monthlyUpkeep, new GameDate(1900, month, day));
                    Assert.That(share, Is.GreaterThanOrEqualTo(0L));
                    total += share;
                }

                Assert.That(total, Is.EqualTo(monthlyUpkeep), $"Month {month}");
            }
        }

        [Test]
        public void AdvanceAll_FirstDay_ChargesDailyShareToRoute()
        {
            GeneratedWorld world = CreateWorldWithTruck(out int routeId);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            long cashBefore = world.CompanyCash;
            var date = new GameDate(1900, 1, 1);
            long expected = VehicleUpkeep.GetDailyShare(catalog.StartingRoadVehicle.UpkeepPerMonth, date);

            long charged = VehicleUpkeep.AdvanceAll(world, catalog, date);

            Assert.That(charged, Is.EqualTo(expected));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore - expected));
            Assert.That(GetRoute(world, routeId).CurrentMonthRunningCost, Is.EqualTo(expected));
            Assert.That(world.LastUpkeepDay, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceAll_SameDay_DoesNotChargeAgain()
        {
            GeneratedWorld world = CreateWorldWithTruck(out _);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, 3));
            long cashAfterFirst = world.CompanyCash;

            long charged = VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, 3));

            Assert.That(charged, Is.EqualTo(0L));
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterFirst));
        }

        [Test]
        public void AdvanceAll_FullMonth_ChargesExactMonthlyUpkeepOfTruckAndTrain()
        {
            GeneratedWorld world = CreateWorldWithTruckAndTrain(extraWagons: 1);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            long cashBefore = world.CompanyCash;
            long expected = catalog.StartingRoadVehicle.UpkeepPerMonth +
                catalog.MonthlyUpkeepForRail(catalog.StartingRailLocomotive.Id, 2);

            long charged = 0L;
            for (int day = 1; day <= 31; day++)
            {
                charged += VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, day));
            }

            Assert.That(charged, Is.EqualTo(expected));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore - expected));
        }

        [Test]
        public void AdvanceAll_MonthChange_MovesRouteTotalsToLastMonth()
        {
            GeneratedWorld world = CreateWorldWithTruck(out int routeId);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            long monthly = catalog.StartingRoadVehicle.UpkeepPerMonth;
            for (int day = 1; day <= 31; day++)
            {
                VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, day));
            }

            Route route = GetRoute(world, routeId);
            Assert.That(route.HasLastMonth, Is.False);

            var firstOfFebruary = new GameDate(1900, 2, 1);
            VehicleUpkeep.AdvanceAll(world, catalog, firstOfFebruary);

            Assert.That(route.HasLastMonth, Is.True);
            Assert.That(route.LastMonthRunningCost, Is.EqualTo(monthly));
            Assert.That(route.CurrentMonthRunningCost, Is.EqualTo(VehicleUpkeep.GetDailyShare(monthly, firstOfFebruary)));
        }

        [Test]
        public void AdvanceAll_UnassignedVehicle_CostsNothing()
        {
            GeneratedWorld world = CreateWorldWithTruck(out int routeId);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.Accept(processor, new UnassignVehicleCommand(routeId, world.RoadVehicles[0].Id));
            long cashBefore = world.CompanyCash;

            long charged = VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));

            Assert.That(charged, Is.EqualTo(0L));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.UnassignedRoadVehicles.Count, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceAll_InsufficientCash_ClampsToZero()
        {
            GeneratedWorld world = CreateWorldWithTruck(out _);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            world.DebitCompanyCashClamped(world.CompanyCash - 1L, FinanceCategory.RunningCosts);

            long charged = VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));

            Assert.That(charged, Is.EqualTo(1L));
            Assert.That(world.CompanyCash, Is.EqualTo(0L));
            Assert.That(world.IsBankrupt, Is.True);
        }

        private static Route GetRoute(GeneratedWorld world, int routeId)
        {
            Assert.That(world.TryGetRoute(routeId, out Route? route), Is.True);
            return route!;
        }

        private static GeneratedWorld CreateWorldWithTruck(out int routeId)
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int first = BuildRoadStation(world, processor, world.TownAnchors[0]);
            int second = BuildRoadStation(world, processor, world.TownAnchors[1]);
            RouteTestSupport.PlaceRoadVehicle(processor, world, first, second);
            routeId = world.RoadVehicles[0].RouteId;
            return world;
        }

        private static GeneratedWorld CreateWorldWithTruckAndTrain(int extraWagons)
        {
            GeneratedWorld world = CreateWorldWithTruck(out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int first = BuildRailStation(world, processor, world.TownAnchors[0]);
            int second = BuildRailStation(world, processor, world.TownAnchors[1]);
            int trainId = RouteTestSupport.PlaceRailVehicle(processor, world, first, second);
            for (int wagon = 0; wagon < extraWagons; wagon++)
            {
                RouteTestSupport.Accept(processor, new AddRailWagonCommand(trainId));
            }

            return world;
        }

        private static int BuildRoadStation(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            TileCoordinate anchor)
        {
            TileCoordinate tile = FindEmptyNeighbor(world, anchor);
            RouteTestSupport.Accept(processor, new BuildRoadStationCommand(tile));
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? station), Is.True);
            return station!.Id;
        }

        private static int BuildRailStation(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            TileCoordinate anchor)
        {
            TileCoordinate tile = FindEmptyNeighbor(world, anchor);
            RouteTestSupport.Accept(processor, new BuildRailStationCommand(tile));
            Assert.That(world.TryGetRailStationAt(tile, out RailStation? station), Is.True);
            return station!.Id;
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 61002UL);
        }

        // Searches the tiles beside the edges of the 2x2 site (not its corners, which are not orthogonally adjacent),
        // so road and rail stations of the same town fit.
        private static TileCoordinate FindEmptyNeighbor(GeneratedWorld world, TileCoordinate anchor)
        {
            for (int dy = -1; dy <= 2; dy++)
            {
                for (int dx = -1; dx <= 2; dx++)
                {
                    bool corner = (dx == -1 || dx == 2) && (dy == -1 || dy == 2);
                    bool inside = dx >= 0 && dx <= 1 && dy >= 0 && dy <= 1;
                    var candidate = new TileCoordinate(anchor.X + dx, anchor.Y + dy);
                    if (!corner && !inside && world.Map.Contains(candidate)
                        && world.Map.GetObjectId(candidate) == TileMap.NoObjectId
                        && world.Map.GetRoadTypeId(candidate) == TileMap.NoRoadTypeId
                        && world.Map.GetRailTypeId(candidate) == TileMap.NoRailTypeId
                        && world.Map.GetStationId(candidate) == TileMap.NoStationId
                        && world.Map.GetRailStationId(candidate) == TileMap.NoRailStationId)
                    {
                        return candidate;
                    }
                }
            }

            throw new InvalidOperationException("No empty tile next to the site.");
        }
    }
}
