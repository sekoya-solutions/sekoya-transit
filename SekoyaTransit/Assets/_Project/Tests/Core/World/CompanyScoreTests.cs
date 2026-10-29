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
    [TestFixture]
    public sealed class CompanyScoreTests
    {
        private const string _truckMk2Id = "truck_mk2";
        private const long _truckMk2UnlockCost = 50000L;
        private static readonly string[] _forestOutputCargoIds = { "wood" };
        private static readonly GameDate _january = new GameDate(1900, 1, 1);

        [Test]
        public void Compute_NoVehicles_EqualsCash()
        {
            GeneratedWorld world = CreateWorld();

            Assert.That(CompanyScore.ComputeVehicleResaleValue(world, CreateCatalog()), Is.Zero);
            Assert.That(CompanyScore.Compute(world, CreateCatalog()), Is.EqualTo(world.CompanyCash));
        }

        [Test]
        public void Compute_OneRoadVehicle_AddsHalfPurchasePrice()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRoadVehicle(world, processor, catalog, tick: 1);

            Assert.That(
                CompanyScore.Compute(world, catalog),
                Is.EqualTo(world.CompanyCash + EconomySettings.HalfRefund(catalog.StartingRoadVehicle.Purchase)));
        }

        [Test]
        public void Compute_RailVehicleWithWagons_AddsRailSellRefund()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRailVehicle(world, processor, catalog, wagonCount: 3, tick: 1);

            Assert.That(
                CompanyScore.Compute(world, catalog),
                Is.EqualTo(world.CompanyCash + catalog.SellRefundForRail(catalog.StartingRailLocomotive.Id, 3)));
        }

        [Test]
        public void Compute_MixedFleet_SumsEveryVehicle()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRoadVehicle(world, processor, catalog, tick: 1);
            BuyRoadVehicle(world, processor, catalog, tick: 2);
            BuyRailVehicle(world, processor, catalog, wagonCount: 2, tick: 3);
            long expectedResale =
                (2L * EconomySettings.HalfRefund(catalog.StartingRoadVehicle.Purchase)) +
                catalog.SellRefundForRail(catalog.StartingRailLocomotive.Id, 2);

            Assert.That(CompanyScore.ComputeVehicleResaleValue(world, catalog), Is.EqualTo(expectedResale));
            Assert.That(CompanyScore.Compute(world, catalog), Is.EqualTo(world.CompanyCash + expectedResale));
        }

        [Test]
        public void Compute_MovingRoadVehicle_StillCountsAtResaleValue()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateTownAndForestStations(out RoadStation townStation, out RoadStation forestStation);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(100), new BuyRoadVehicleCommand(townStation.Id, catalog)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(101),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(forestStation.Id, waitForFull: false),
                            new RoadVehicleOrder(townStation.Id, waitForFull: false)
                        })).IsAccepted,
                Is.True);
            RoadVehicle vehicle = world.RoadVehicles[0];
            for (int tick = 0; tick < 200 && !vehicle.IsOnEdge; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, calendarDate: _january);
            }

            Assert.That(vehicle.IsOnEdge, Is.True);
            Assert.That(
                CompanyScore.Compute(world, catalog),
                Is.EqualTo(world.CompanyCash + EconomySettings.HalfRefund(catalog.StartingRoadVehicle.Purchase)));
        }

        [Test]
        public void Compute_SellRoadVehicle_LeavesScoreUnchanged()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRoadVehicle(world, processor, catalog, tick: 1);
            long scoreBefore = CompanyScore.Compute(world, catalog);

            Assert.That(processor.Process(new Tick(10), new SellRoadVehicleCommand(0, catalog)).IsAccepted, Is.True);

            Assert.That(world.RoadVehicles, Is.Empty);
            Assert.That(CompanyScore.Compute(world, catalog), Is.EqualTo(scoreBefore));
        }

        [Test]
        public void Compute_SellRailWagon_LeavesScoreUnchanged()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRailVehicle(world, processor, catalog, wagonCount: 2, tick: 1);
            long scoreBefore = CompanyScore.Compute(world, catalog);

            Assert.That(processor.Process(new Tick(10), new SellRailWagonCommand(0, catalog)).IsAccepted, Is.True);

            Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(1));
            Assert.That(CompanyScore.Compute(world, catalog), Is.EqualTo(scoreBefore));
        }

        [Test]
        public void Compute_SellRailVehicle_LeavesScoreUnchanged()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRailVehicle(world, processor, catalog, wagonCount: 2, tick: 1);
            long scoreBefore = CompanyScore.Compute(world, catalog);

            Assert.That(processor.Process(new Tick(10), new SellRailVehicleCommand(0, catalog)).IsAccepted, Is.True);

            Assert.That(world.RailVehicles, Is.Empty);
            Assert.That(CompanyScore.Compute(world, catalog), Is.EqualTo(scoreBefore));
        }

        [Test]
        public void Compute_BuyOddPricedVehicles_LowersScoreByNonRefundablePart()
        {
            ContentCatalog catalog = CreateOddPriceCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            long scoreBefore = CompanyScore.Compute(world, catalog);

            BuyRoadVehicle(world, processor, catalog, tick: 1);
            long scoreAfterTruck = CompanyScore.Compute(world, catalog);
            BuyRailVehicle(world, processor, catalog, wagonCount: 1, tick: 2);

            Assert.That(scoreBefore - scoreAfterTruck, Is.EqualTo(8001L - 4000L));
            Assert.That(
                scoreAfterTruck - CompanyScore.Compute(world, catalog),
                Is.EqualTo((20001L - 10000L) + (4001L - 2000L)));
        }

        [Test]
        public void Compute_UnlockVehicleType_LowersScoreByUnlockCost()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            long scoreBefore = CompanyScore.Compute(world, catalog);

            Assert.That(
                processor.Process(new Tick(1), new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog)).IsAccepted,
                Is.True);

            Assert.That(CompanyScore.Compute(world, catalog), Is.EqualTo(scoreBefore - _truckMk2UnlockCost));
        }

        [Test]
        public void Compute_ScoreAboveLongMaxValue_ThrowsOverflow()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRoadVehicle(world, processor, catalog, tick: 1);
            world.CreditCompanyCash(long.MaxValue - world.CompanyCash, FinanceCategory.DeliveryIncome);

            Assert.Throws<OverflowException>(() => CompanyScore.Compute(world, catalog));
        }

        [Test]
        public void Compute_UnknownRoadVehicleType_Throws()
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            BuyRoadVehicle(world, processor, CreateCatalogWithMk2(), tick: 1, typeId: _truckMk2Id, calendarYear: 1910);

            Assert.Throws<InvalidOperationException>(() => CompanyScore.Compute(world, CreateCatalog()));
        }

        private static void BuyRoadVehicle(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            ContentCatalog catalog,
            int tick,
            string? typeId = null,
            int calendarYear = 1900)
        {
            if (typeId != null && !world.IsVehicleTypeUnlocked(typeId))
            {
                Assert.That(
                    processor.Process(new Tick(tick), new UnlockVehicleTypeCommand(typeId, calendarYear, catalog)).IsAccepted,
                    Is.True);
            }

            RoadStation station = BuildRoadStation(world, processor, tick);
            Assert.That(
                processor.Process(
                    new Tick(tick),
                    new BuyRoadVehicleCommand(station.Id, catalog, typeId, calendarYear)).IsAccepted,
                Is.True);
        }

        private static void BuyRailVehicle(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            ContentCatalog catalog,
            int wagonCount,
            int tick)
        {
            TileCoordinate tile = FindEmptyTile(world, world.TownAnchors[1]);
            Assert.That(processor.Process(new Tick(tick), new BuildRailStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(tile, out RailStation? station), Is.True);
            Assert.That(processor.Process(new Tick(tick), new BuyRailVehicleCommand(station!.Id, catalog)).IsAccepted, Is.True);
            int vehicleId = world.RailVehicles[world.RailVehicles.Count - 1].Id;
            for (int wagon = 0; wagon < wagonCount; wagon++)
            {
                Assert.That(
                    processor.Process(new Tick(tick), new AddRailWagonCommand(vehicleId, catalog)).IsAccepted,
                    Is.True);
            }
        }

        private static RoadStation BuildRoadStation(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            int tick)
        {
            if (world.RoadStations.Count > 0)
            {
                return world.RoadStations[0];
            }

            TileCoordinate tile = FindEmptyTile(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(tick), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? station), Is.True);
            return station!;
        }

        private static GeneratedWorld CreateTownAndForestStations(
            out RoadStation townStation,
            out RoadStation forestStation)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townStationCoord = FindEmptyTile(world, world.TownAnchors[0]);
            TileCoordinate forestStationCoord = FindEmptyTile(world, world.Industries[0].Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(townStationCoord)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new BuildRoadStationCommand(forestStationCoord)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(townStationCoord, out RoadStation? foundTown), Is.True);
            Assert.That(world.TryGetRoadStationAt(forestStationCoord, out RoadStation? foundForest), Is.True);
            townStation = foundTown!;
            forestStation = foundForest!;
            ConnectStationsWithRoads(world, processor, townStationCoord, forestStationCoord, startingTick: 3);
            return world;
        }

        private static void ConnectStationsWithRoads(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            TileCoordinate fromStation,
            TileCoordinate toStation,
            int startingTick)
        {
            var cameFrom = new Dictionary<int, int>();
            var queue = new Queue<TileCoordinate>();
            queue.Enqueue(fromStation);
            cameFrom[world.Map.GetTileId(fromStation)] = -1;
            bool found = false;
            while (queue.Count > 0)
            {
                TileCoordinate current = queue.Dequeue();
                if (current.Equals(toStation))
                {
                    found = true;
                    break;
                }

                TileCoordinate[] offsets = OrthogonalOffsets();
                for (int index = 0; index < offsets.Length; index++)
                {
                    var next = new TileCoordinate(current.X + offsets[index].X, current.Y + offsets[index].Y);
                    if (!world.Map.Contains(next))
                    {
                        continue;
                    }

                    int nextId = world.Map.GetTileId(next);
                    if (cameFrom.ContainsKey(nextId))
                    {
                        continue;
                    }

                    if (!next.Equals(toStation) &&
                        (world.Map.GetObjectId(next) != TileMap.NoObjectId ||
                         world.Map.GetStationId(next) != TileMap.NoStationId))
                    {
                        continue;
                    }

                    cameFrom[nextId] = world.Map.GetTileId(current);
                    queue.Enqueue(next);
                }
            }

            Assert.That(found, Is.True, "No empty path between stations.");
            var path = new List<TileCoordinate>();
            int cursor = world.Map.GetTileId(toStation);
            while (cursor >= 0)
            {
                path.Add(world.Map.GetCoordinate(cursor));
                cursor = cameFrom[cursor];
            }

            path.Reverse();
            int tick = startingTick;
            for (int index = 0; index < path.Count; index++)
            {
                TileCoordinate step = path[index];
                if (step.Equals(fromStation) ||
                    step.Equals(toStation) ||
                    world.Map.GetRoadTypeId(step) != TileMap.NoRoadTypeId)
                {
                    continue;
                }

                Assert.That(processor.Process(new Tick(tick++), new BuildRoadCommand(step)).IsAccepted, Is.True);
            }
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 46001UL);
        }

        private static TileCoordinate FindEmptyTile(GeneratedWorld world, TileCoordinate anchor)
        {
            TileCoordinate[] offsets = OrthogonalOffsets();
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

        private static TileCoordinate[] OrthogonalOffsets()
        {
            return new[]
            {
                new TileCoordinate(0, -1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(-1, 0)
            };
        }

        private static ContentCatalog CreateCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }

        private static ContentCatalog CreateOddPriceCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: new[]
                {
                    new VehicleDefinition("truck_mk1", "vehicle.truck_mk1", VehicleMode.Road, 8001L, 200L, 1900, 0L, true, cargoCapacity: 10, speedThousandths: 250),
                    new VehicleDefinition("loco_mk1", "vehicle.loco_mk1", VehicleMode.Rail, 20001L, 400L, 1900, 0L, true, maxWagons: 3, capacityPerWagon: 10, speedThousandths: 250),
                    new VehicleDefinition("rail_wagon", "vehicle.rail_wagon", VehicleMode.RailWagon, 4001L, 100L, 1900, 0L, true)
                });
        }

        private static ContentCatalog CreateCatalogWithMk2()
        {
            var vehicles = new List<VehicleDefinition>(ContentCatalog.CreatePlaceholderVehicles())
            {
                new VehicleDefinition(_truckMk2Id, "vehicle.truck_mk2", VehicleMode.Road, 12000L, 250L, 1910, _truckMk2UnlockCost, false, cargoCapacity: 12, speedThousandths: 300)
            };
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: vehicles);
        }
    }
}
