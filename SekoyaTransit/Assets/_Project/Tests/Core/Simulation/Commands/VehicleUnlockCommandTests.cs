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
    public sealed class VehicleUnlockCommandTests
    {
        private const string _truckMk1Id = "truck_mk1";
        private const string _truckMk2Id = "truck_mk2";
        private const long _truckMk2UnlockCost = 50000L;
        private const long _truckMk2Purchase = 12000L;
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void UnlockVehicleType_DebitsCostAndRecordsPermanentUnlock()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out _);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore - _truckMk2UnlockCost));
            Assert.That(world.IsVehicleTypeUnlocked(_truckMk2Id), Is.True);
            CollectionAssert.AreEqual(new[] { _truckMk2Id }, world.UnlockedVehicleTypeIds);
        }

        [Test]
        public void UnlockVehicleType_BeforeAvailableYear_Rejects()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out _);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1909, catalog));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("not available"));
            Assert.That(world.IsVehicleTypeUnlocked(_truckMk2Id), Is.False);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
        }

        [Test]
        public void UnlockVehicleType_AlreadyUnlocked_Rejects()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(
                    new Tick(1),
                    new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog)).IsAccepted,
                Is.True);
            long cashAfterUnlock = world.CompanyCash;

            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("already unlocked"));
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterUnlock));
        }

        [Test]
        public void UnlockVehicleType_InsufficientCash_RejectsWithoutMutation()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out _);
            world.DebitCompanyCashClamped(world.CompanyCash - (_truckMk2UnlockCost - 1L), FinanceCategory.RunningCosts);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Insufficient"));
            Assert.That(world.IsVehicleTypeUnlocked(_truckMk2Id), Is.False);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
        }

        [Test]
        public void BuyVehicleForRoute_NonStartingAfterUnlock_Succeeds()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out int routeId);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(
                    new Tick(1),
                    new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog)).IsAccepted,
                Is.True);
            long cashAfterUnlock = world.CompanyCash;

            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new BuyVehicleForRouteCommand(routeId, catalog, _truckMk2Id, calendarYear: 1910));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.RoadVehicles, Has.Count.EqualTo(1));
            Assert.That(world.RoadVehicles[0].TypeId, Is.EqualTo(_truckMk2Id));
            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterUnlock - _truckMk2Purchase));
        }

        [Test]
        public void BuyVehicleForRoute_LockedType_Rejects()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out int routeId);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyVehicleForRouteCommand(routeId, catalog, _truckMk2Id, calendarYear: 1910));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("not unlocked"));
            Assert.That(world.RoadVehicles, Is.Empty);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
        }

        [Test]
        public void BuyVehicleForRoute_FutureYear_Rejects()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithRoadStation(out int routeId);
            long cashBefore = world.CompanyCash;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuyVehicleForRouteCommand(routeId, catalog, _truckMk2Id, calendarYear: 1909));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("not available"));
            Assert.That(world.RoadVehicles, Is.Empty);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
        }

        private static ContentCatalog CreateCatalogWithMk2()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: new[]
                {
                    new VehicleDefinition(
                        _truckMk1Id,
                        "vehicle.truck_mk1",
                        VehicleMode.Road,
                        8000L,
                        200L,
                        1900,
                        0L,
                        true,
                        cargoCapacity: 10,
                        speedThousandths: 250),
                    new VehicleDefinition(
                        _truckMk2Id,
                        "vehicle.truck_mk2",
                        VehicleMode.Road,
                        _truckMk2Purchase,
                        250L,
                        1910,
                        _truckMk2UnlockCost,
                        false,
                        cargoCapacity: 12,
                        speedThousandths: 300),
                    new VehicleDefinition(
                        "loco_mk1",
                        "vehicle.loco_mk1",
                        VehicleMode.Rail,
                        20000L,
                        400L,
                        1900,
                        0L,
                        true,
                        maxWagons: 3,
                        capacityPerWagon: 10,
                        speedThousandths: 250),
                    new VehicleDefinition(
                        "rail_wagon",
                        "vehicle.rail_wagon",
                        VehicleMode.RailWagon,
                        4000L,
                        100L,
                        1900,
                        0L,
                        true)
                });
        }

        private static GeneratedWorld CreateWorldWithRoadStation(out int routeId)
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            var stationIds = new int[2];
            for (int index = 0; index < stationIds.Length; index++)
            {
                TileCoordinate tile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
                Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
                Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? found), Is.True);
                stationIds[index] = found!.Id;
            }

            routeId = RouteTestSupport.CreateRoute(processor, world, RouteMode.Road, stationIds);
            return world;
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 63001UL);
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
