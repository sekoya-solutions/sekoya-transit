#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.Tests.World;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class VehicleEconomyCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuyVehicleForRoute_Road_DebitsPurchasePrice()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorldWithRoadStations(out RoadStation a, out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int routeId = RouteTestSupport.CreateRoute(processor, world, RouteMode.Road, new[] { a.Id, b.Id });
            long cashBefore = world.CompanyCash;

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyVehicleForRouteCommand(routeId, catalog, catalog.StartingRoadVehicle.Id, GameDate.FirstYear));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore - catalog.StartingRoadVehicle.Purchase));
            Assert.That(world.RoadVehicles, Has.Count.EqualTo(1));
        }

        [Test]
        public void BuyVehicleForRoute_UsesLastCash_BankruptsCompany()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorldWithRoadStations(out RoadStation a, out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int routeId = RouteTestSupport.CreateRoute(processor, world, RouteMode.Road, new[] { a.Id, b.Id });
            world.DebitCompanyCashClamped(world.CompanyCash - catalog.StartingRoadVehicle.Purchase, FinanceCategory.RunningCosts);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyVehicleForRouteCommand(routeId, catalog, catalog.StartingRoadVehicle.Id, GameDate.FirstYear));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.CompanyCash, Is.Zero);
            Assert.That(world.IsBankrupt, Is.True);
        }

        [Test]
        public void BuyVehicleForRoute_InsufficientCash_RejectsWithoutMutation()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorldWithRoadStations(out RoadStation a, out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int routeId = RouteTestSupport.CreateRoute(processor, world, RouteMode.Road, new[] { a.Id, b.Id });
            world.DebitCompanyCashClamped(world.CompanyCash - 100L, FinanceCategory.RunningCosts);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyVehicleForRouteCommand(routeId, catalog, catalog.StartingRoadVehicle.Id, GameDate.FirstYear));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Insufficient"));
            Assert.That(world.RoadVehicles, Is.Empty);
            Assert.That(world.CompanyCash, Is.EqualTo(100L));
        }

        [Test]
        public void SellRoadVehicle_CreditsHalfPurchase()
        {
            GeneratedWorld world = CreateWorldWithRoadStations(out RoadStation a, out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(processor, world, a.Id, b.Id);
            long cashAfterBuy = world.CompanyCash;

            Assert.That(processor.Process(new Tick(2), new SellRoadVehicleCommand(vehicleId)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterBuy + EconomySettings.HalfRefund(catalog.StartingRoadVehicle.Purchase)));
            Assert.That(world.RoadVehicles, Is.Empty);
        }

        [Test]
        public void BuyVehicleForRoute_Rail_DebitsLocomotiveAndOneWagon()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorldWithRailStations(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int routeId = RouteTestSupport.CreateRoute(processor, world, RouteMode.Rail, new[] { a.Id, b.Id });
            long cashBefore = world.CompanyCash;

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyVehicleForRouteCommand(routeId, catalog, catalog.StartingRailLocomotive.Id, GameDate.FirstYear));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(
                world.CompanyCash,
                Is.EqualTo(cashBefore - catalog.StartingRailLocomotive.Purchase - catalog.RailWagon.Purchase));
            Assert.That(
                BuyVehicleForRouteCommand.GetPrice(catalog, catalog.StartingRailLocomotive),
                Is.EqualTo(catalog.StartingRailLocomotive.Purchase + catalog.RailWagon.Purchase));
        }

        [Test]
        public void AddAndSellRailWagon_DebitsAndRefundsHalf()
        {
            GeneratedWorld world = CreateWorldWithRailStations(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int vehicleId = RouteTestSupport.PlaceRailVehicle(processor, world, a.Id, b.Id);
            long cashAfterTrain = world.CompanyCash;

            Assert.That(processor.Process(new Tick(2), new AddRailWagonCommand(vehicleId)).IsAccepted, Is.True);
            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterTrain - catalog.RailWagon.Purchase));

            Assert.That(processor.Process(new Tick(3), new SellRailWagonCommand(vehicleId)).IsAccepted, Is.True);
            Assert.That(
                world.CompanyCash,
                Is.EqualTo(cashAfterTrain - catalog.RailWagon.Purchase + EconomySettings.HalfRefund(catalog.RailWagon.Purchase)));
        }

        [Test]
        public void SellRailVehicle_CreditsHalfLocoAndWagons()
        {
            GeneratedWorld world = CreateWorldWithRailStations(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int vehicleId = RouteTestSupport.PlaceRailVehicle(processor, world, a.Id, b.Id);
            Assert.That(processor.Process(new Tick(2), new AddRailWagonCommand(vehicleId)).IsAccepted, Is.True);
            Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(2));
            long cashAfterBuy = world.CompanyCash;

            Assert.That(processor.Process(new Tick(4), new SellRailVehicleCommand(vehicleId)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterBuy + catalog.SellRefundForRail(catalog.StartingRailLocomotive.Id, 2)));
            Assert.That(world.RailVehicles, Is.Empty);
        }

        [Test]
        public void SellRailVehicle_Unassigned_CreditsHalfLocoAndWagon()
        {
            GeneratedWorld world = CreateWorldWithRailStations(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int vehicleId = RouteTestSupport.PlaceRailVehicle(processor, world, a.Id, b.Id);
            int routeId = world.RailVehicles[0].RouteId;
            RouteTestSupport.Accept(processor, new UnassignVehicleCommand(routeId, vehicleId));
            long cashAfterUnassign = world.CompanyCash;

            Assert.That(processor.Process(new Tick(4), new SellRailVehicleCommand(vehicleId)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterUnassign + catalog.SellRefundForRail(catalog.StartingRailLocomotive.Id, 1)));
            Assert.That(world.UnassignedRailVehicles, Is.Empty);
        }

        [Test]
        public void AddRailWagon_InsufficientCash_Rejects()
        {
            GeneratedWorld world = CreateWorldWithRailStations(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int vehicleId = RouteTestSupport.PlaceRailVehicle(processor, world, a.Id, b.Id);
            world.DebitCompanyCashClamped(world.CompanyCash, FinanceCategory.RunningCosts);
            long cash = world.CompanyCash;

            CommandProcessingResult result = processor.Process(new Tick(2), new AddRailWagonCommand(vehicleId));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Insufficient"));
            Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(1));
            Assert.That(world.CompanyCash, Is.EqualTo(cash));
        }

        private static GeneratedWorld CreateWorldWithRoadStations(out RoadStation first, out RoadStation second)
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate firstTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(firstTile)).IsAccepted, Is.True);
            TileCoordinate secondTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(secondTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(firstTile, out RoadStation? foundFirst), Is.True);
            Assert.That(world.TryGetRoadStationAt(secondTile, out RoadStation? foundSecond), Is.True);
            first = foundFirst!;
            second = foundSecond!;
            return world;
        }

        private static GeneratedWorld CreateWorldWithRailStations(out RailStation first, out RailStation second)
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate firstTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(firstTile)).IsAccepted, Is.True);
            TileCoordinate secondTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(secondTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(firstTile, out RailStation? foundFirst), Is.True);
            Assert.That(world.TryGetRailStationAt(secondTile, out RailStation? foundSecond), Is.True);
            first = foundFirst!;
            second = foundSecond!;
            return world;
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 61001UL);
        }

        private static TileCoordinate FindOrthogonalEmptyNeighbor(GeneratedWorld world, TileCoordinate anchor)
        {
            var offsets = new[]
            {
                new TileCoordinate(0, -1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(-1, 0)
            };
            for (int index = 0; index < offsets.Length; index++)
            {
                var candidate = new TileCoordinate(anchor.X + offsets[index].X, anchor.Y + offsets[index].Y);
                if (!world.Map.Contains(candidate))
                {
                    continue;
                }

                if (world.Map.GetObjectId(candidate) == TileMap.NoObjectId
                    && world.Map.GetRoadTypeId(candidate) == TileMap.NoRoadTypeId
                    && world.Map.GetRailTypeId(candidate) == TileMap.NoRailTypeId
                    && world.Map.GetStationId(candidate) == TileMap.NoStationId
                    && world.Map.GetRailStationId(candidate) == TileMap.NoRailStationId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }
        private static ContentCatalog CreateCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }
    }
}
