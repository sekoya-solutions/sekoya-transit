#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class VehicleEconomyCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuyRoadVehicle_DebitsPurchasePrice()
        {
            GeneratedWorld world = CreateWorldWithRoadStation(out RoadStation station);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(station.Id)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore - catalog.StartingRoadVehicle.Purchase));
            Assert.That(world.RoadVehicles, Has.Count.EqualTo(1));
        }

        [Test]
        public void BuyRoadVehicle_UsesLastCash_BankruptsCompany()
        {
            GeneratedWorld world = CreateWorldWithRoadStation(out RoadStation station);
            ContentCatalog catalog = CreateCatalog();
            world.DebitCompanyCashClamped(world.CompanyCash - catalog.StartingRoadVehicle.Purchase, FinanceCategory.RunningCosts);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyRoadVehicleCommand(station.Id, catalog));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.CompanyCash, Is.Zero);
            Assert.That(world.IsBankrupt, Is.True);
        }

        [Test]
        public void BuyRoadVehicle_InsufficientCash_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorldWithRoadStation(out RoadStation station);
            world.DebitCompanyCashClamped(world.CompanyCash - 100L, FinanceCategory.RunningCosts);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuyRoadVehicleCommand(station.Id));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Insufficient"));
            Assert.That(world.RoadVehicles, Is.Empty);
            Assert.That(world.CompanyCash, Is.EqualTo(100L));
        }

        [Test]
        public void SellRoadVehicle_CreditsHalfPurchase()
        {
            GeneratedWorld world = CreateWorldWithRoadStation(out RoadStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(station.Id)).IsAccepted, Is.True);
            long cashAfterBuy = world.CompanyCash;

            Assert.That(processor.Process(new Tick(2), new SellRoadVehicleCommand(0)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterBuy + EconomySettings.HalfRefund(catalog.StartingRoadVehicle.Purchase)));
            Assert.That(world.RoadVehicles, Is.Empty);
        }

        [Test]
        public void BuyRailVehicle_DebitsLocomotivePrice()
        {
            GeneratedWorld world = CreateWorldWithRailStation(out RailStation station);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore - catalog.StartingRailLocomotive.Purchase));
        }

        [Test]
        public void AddAndSellRailWagon_DebitsAndRefundsHalf()
        {
            GeneratedWorld world = CreateWorldWithRailStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);
            long cashAfterLoco = world.CompanyCash;

            Assert.That(processor.Process(new Tick(2), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterLoco - catalog.RailWagon.Purchase));

            Assert.That(processor.Process(new Tick(3), new SellRailWagonCommand(0)).IsAccepted, Is.True);
            Assert.That(
                world.CompanyCash,
                Is.EqualTo(cashAfterLoco - catalog.RailWagon.Purchase + EconomySettings.HalfRefund(catalog.RailWagon.Purchase)));
        }

        [Test]
        public void SellRailVehicle_CreditsHalfLocoAndWagons()
        {
            GeneratedWorld world = CreateWorldWithRailStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(3), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            long cashAfterBuy = world.CompanyCash;

            Assert.That(processor.Process(new Tick(4), new SellRailVehicleCommand(0)).IsAccepted, Is.True);

            ContentCatalog catalog = CreateCatalog();
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterBuy + catalog.SellRefundForRail(catalog.StartingRailLocomotive.Id, 2)));
            Assert.That(world.RailVehicles, Is.Empty);
        }

        [Test]
        public void AddRailWagon_InsufficientCash_Rejects()
        {
            GeneratedWorld world = CreateWorldWithRailStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);
            world.DebitCompanyCashClamped(world.CompanyCash, FinanceCategory.RunningCosts);
            long cash = world.CompanyCash;

            CommandProcessingResult result = processor.Process(new Tick(2), new AddRailWagonCommand(0));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Insufficient"));
            Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(0));
            Assert.That(world.CompanyCash, Is.EqualTo(cash));
        }

        private static GeneratedWorld CreateWorldWithRoadStation(out RoadStation station)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? found), Is.True);
            station = found!;
            return world;
        }

        private static GeneratedWorld CreateWorldWithRailStation(out RailStation station)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(tile, out RailStation? found), Is.True);
            station = found!;
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
