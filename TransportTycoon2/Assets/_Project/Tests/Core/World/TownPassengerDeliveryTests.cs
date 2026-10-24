#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class TownPassengerDeliveryTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static int _stationBuildTick = 1;

        [Test]
        public void RouteFilter_TwoTownStops_AllowsPassengerCargo()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateTwoTownWorld(catalog, out RoadStation a, out RoadStation b);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(0, new[] { a.Id, b.Id }, catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            Assert.That(vehicle.AllowsLoadCargo("passengers"), Is.True);
            Assert.That(vehicle.AllowsLoadCargo("mail"), Is.True);
        }

        [Test]
        public void RouteFilter_SingleTownStop_DoesNotAllowPassengerCargo()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateTwoTownWorld(catalog, out RoadStation a, out RoadStation b);
            _ = b;

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(0, new[] { a.Id, a.Id }, catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            Assert.That(vehicle.AllowsLoadCargo("passengers"), Is.False);
            Assert.That(vehicle.AllowsLoadCargo("mail"), Is.False);
        }

        [Test]
        public void LoadAndDeliver_BetweenTowns_PaysWithoutCreditingNeeds()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateTwoTownWorld(catalog, out RoadStation a, out RoadStation b);
            Town origin = world.Towns[0];
            Assert.That(origin.TryAddWaitingCargo("passengers", 3, catalog.TownNeeds.PassengerWaitingCapacity), Is.EqualTo(3));

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(a.Id, waitForFull: false),
                            new RoadVehicleOrder(b.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            long cashBefore = world.CompanyCash;
            var date = new GameDate(1900, 1, 1);
            for (int tick = 0; tick < 8000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, date);
                if (world.CompanyCash - cashBefore >= 10L * 3)
                {
                    break;
                }
            }

            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(10L * 3));
            Assert.That(vehicle.LifetimeDeliveryIncome, Is.EqualTo(10L * 3));
            Assert.That(vehicle.PassengerCargoOriginTownId, Is.EqualTo(PassengerCargo.NoOriginTownId));
            Assert.That(origin.Needs[0].MonthSupply, Is.EqualTo(0));
            Assert.That(origin.MonthPassengersTransported, Is.EqualTo(3L));
        }

        [Test]
        public void ConsumesPassengerOnUnload_AtOriginTown_ReturnsFalse()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateTwoTownWorld(catalog, out RoadStation a, out RoadStation b);
            Town origin = world.Towns[0];
            Town destination = world.Towns[1];

            Assert.That(
                DeliveryAcceptance.ConsumesPassengerOnUnload(
                    world,
                    a,
                    "passengers",
                    catalog,
                    origin.Id),
                Is.False);
            Assert.That(
                DeliveryAcceptance.ConsumesPassengerOnUnload(
                    world,
                    b,
                    "passengers",
                    catalog,
                    origin.Id),
                Is.True);
            Assert.That(destination.Id, Is.Not.EqualTo(origin.Id));
        }

        private static ContentCatalog CreateCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 0),
                new CargoDefinition("mail", "cargo.mail", CargoClass.Passengers, 6, 0),
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1)
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                demandPerNeedPerMonth: 10,
                populationPerSatisfiedNeed: 20,
                passengersPerPopulationUnit: 100,
                mailPerPopulationUnit: 250,
                passengerWaitingCapacity: 50);
            return new ContentCatalog(cargo, Array.Empty<IndustryDefinition>(), townNeeds);
        }

        private static GeneratedWorld CreateTwoTownWorld(
            ContentCatalog catalog,
            out RoadStation stationA,
            out RoadStation stationB)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 59100UL, catalog.TownNeeds, catalog);
            Assert.That(world.Towns.Count, Is.EqualTo(2));

            stationA = BuildAdjacentStation(world, world.TownAnchors[0]);
            stationB = BuildAdjacentStation(world, world.TownAnchors[1]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, stationA.Coordinate, stationB.Coordinate, 10);
            return world;
        }

        private static RoadStation BuildAdjacentStation(GeneratedWorld world, TileCoordinate anchor)
        {
            TileCoordinate stationCoordinate = FindEmptyOrthogonalNeighbor(world, anchor);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(new Tick(_stationBuildTick++), new BuildRoadStationCommand(stationCoordinate))
                    .IsAccepted,
                Is.True);
            Assert.That(world.TryGetRoadStationAt(stationCoordinate, out RoadStation? built), Is.True);
            return built!;
        }

        private static TileCoordinate FindEmptyOrthogonalNeighbor(GeneratedWorld world, TileCoordinate coordinate)
        {
            var offsets = new[]
            {
                new TileCoordinate(0, 1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, -1),
                new TileCoordinate(-1, 0)
            };
            for (int index = 0; index < offsets.Length; index++)
            {
                var neighbor = new TileCoordinate(
                    coordinate.X + offsets[index].X,
                    coordinate.Y + offsets[index].Y);
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                if (world.Map.GetObjectId(neighbor) == TileMap.NoObjectId &&
                    world.Map.GetRoadTypeId(neighbor) == TileMap.NoRoadTypeId &&
                    world.Map.GetStationId(neighbor) == TileMap.NoStationId)
                {
                    return neighbor;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
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
            var offsets = new[]
            {
                new TileCoordinate(0, -1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(-1, 0)
            };
            while (queue.Count > 0)
            {
                TileCoordinate current = queue.Dequeue();
                if (current.Equals(toStation))
                {
                    found = true;
                    break;
                }

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

                    if (world.Map.GetObjectId(next) != TileMap.NoObjectId && !next.Equals(toStation))
                    {
                        continue;
                    }

                    if (world.Map.GetStationId(next) != TileMap.NoStationId && !next.Equals(toStation))
                    {
                        continue;
                    }

                    cameFrom[nextId] = world.Map.GetTileId(current);
                    queue.Enqueue(next);
                }
            }

            Assert.That(found, Is.True, "Could not path between stations to pave roads.");
            var path = new List<TileCoordinate>();
            int cursor = world.Map.GetTileId(toStation);
            while (cursor >= 0)
            {
                path.Add(world.Map.GetCoordinate(cursor));
                if (!cameFrom.TryGetValue(cursor, out int previous))
                {
                    break;
                }

                cursor = previous;
            }

            path.Reverse();
            int tick = startingTick;
            for (int index = 0; index < path.Count; index++)
            {
                TileCoordinate step = path[index];
                if (step.Equals(fromStation) || step.Equals(toStation))
                {
                    continue;
                }

                if (world.Map.GetRoadTypeId(step) != TileMap.NoRoadTypeId)
                {
                    continue;
                }

                Assert.That(processor.Process(new Tick(tick++), new BuildRoadCommand(step)).IsAccepted, Is.True);
            }
        }
    }
}
