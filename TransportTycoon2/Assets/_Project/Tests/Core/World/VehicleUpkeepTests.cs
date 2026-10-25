#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class VehicleUpkeepTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void AdvanceAll_FirstMonthObservation_RecordsWithoutCharging()
        {
            GeneratedWorld world = CreateWorldWithTruck();
            EconomySettings economy = EconomySettings.CreatePlaceholder();
            long cashBefore = world.CompanyCash;

            long charged = VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 1, 15));

            Assert.That(charged, Is.EqualTo(0L));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.LastUpkeepYear, Is.EqualTo(1900));
            Assert.That(world.LastUpkeepMonth, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceAll_SameMonth_DoesNotChargeAgain()
        {
            GeneratedWorld world = CreateWorldWithTruck();
            EconomySettings economy = EconomySettings.CreatePlaceholder();
            VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 1, 1));
            long cashAfterFirst = world.CompanyCash;

            long charged = VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 1, 20));

            Assert.That(charged, Is.EqualTo(0L));
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterFirst));
        }

        [Test]
        public void AdvanceAll_MonthChange_DebitsAllVehicles()
        {
            GeneratedWorld world = CreateWorldWithTruckAndTrain(wagonCount: 2);
            EconomySettings economy = EconomySettings.CreatePlaceholder();
            VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 1, 1));
            long cashAfterObserve = world.CompanyCash;
            long expected = economy.RoadVehicleUpkeepPerMonth + economy.MonthlyUpkeepForRail(2);

            long charged = VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 2, 1));

            Assert.That(charged, Is.EqualTo(expected));
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterObserve - expected));
            Assert.That(world.LastUpkeepMonth, Is.EqualTo(2));
        }

        [Test]
        public void AdvanceAll_InsufficientCash_ClampsToZero()
        {
            GeneratedWorld world = CreateWorldWithTruck();
            EconomySettings economy = EconomySettings.CreatePlaceholder();
            VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 1, 1));
            world.DebitCompanyCashClamped(world.CompanyCash - 50L);

            long charged = VehicleUpkeep.AdvanceAll(world, economy, new GameDate(1900, 2, 1));

            Assert.That(charged, Is.EqualTo(50L));
            Assert.That(world.CompanyCash, Is.EqualTo(0L));
            Assert.That(world.IsBankrupt, Is.True);
        }

        private static GeneratedWorld CreateWorldWithTruck()
        {
            GeneratedWorld world = CreateWorld();
            PlaceRoadStationAndBuy(world);
            return world;
        }

        private static GeneratedWorld CreateWorldWithTruckAndTrain(int wagonCount)
        {
            GeneratedWorld world = CreateWorld();
            PlaceRoadStationAndBuy(world);
            TileCoordinate railTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[1]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(10), new BuildRailStationCommand(railTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(railTile, out RailStation? station), Is.True);
            Assert.That(processor.Process(new Tick(11), new BuyRailVehicleCommand(station!.Id)).IsAccepted, Is.True);
            for (int wagon = 0; wagon < wagonCount; wagon++)
            {
                Assert.That(
                    processor.Process(new Tick(12 + wagon), new AddRailWagonCommand(0)).IsAccepted,
                    Is.True);
            }

            return world;
        }

        private static void PlaceRoadStationAndBuy(GeneratedWorld world)
        {
            TileCoordinate tile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? station), Is.True);
            Assert.That(processor.Process(new Tick(2), new BuyRoadVehicleCommand(station!.Id)).IsAccepted, Is.True);
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 61002UL);
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
    }
}
