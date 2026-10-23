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
    public sealed class DeliveryIncomeTests
    {
        [Test]
        public void DeliveryAcceptance_ConsumerIndustry_AcceptsInputCargoOnly()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillStationWorld(out RoadStation station);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "wood", catalog), Is.True);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "planks", catalog), Is.False);
        }

        [Test]
        public void DeliveryAcceptance_TownAdjacentStation_AcceptsAnyCargo()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateTownStationWorld(out RoadStation station);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "planks", catalog), Is.True);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "wood", catalog), Is.True);
        }

        [Test]
        public void DeliveryAcceptance_ProducerOnlyStation_RejectsOutputCargo()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestStationWorld(out RoadStation station);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "wood", catalog), Is.False);
        }

        [Test]
        public void Unload_AtConsumerIndustryStation_CreditsCash()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedPickupAndConsumerWorld(
                out RoadStation pickup,
                out RoadStation consumer);
            long cashBefore = world.CompanyCash;
            pickup.TryAddCargo("wood", 4);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(consumer.Id, waitForFull: false),
                            new RoadVehicleOrder(pickup.Id, waitForFull: false)
                        })).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            bool delivered = false;
            for (int tick = 0; tick < 4000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog);
                if (world.CompanyCash - cashBefore >= 8L * 4)
                {
                    delivered = true;
                    break;
                }
            }

            Assert.That(delivered, Is.True);
            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(8L * 4));
            Assert.That(vehicle.Id, Is.EqualTo(0));
        }

        [Test]
        public void Unload_AtTownStation_ConsumesFreightWithoutStocking()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedPickupAndTownWorld(
                out RoadStation pickup,
                out RoadStation townStation);
            long cashBefore = world.CompanyCash;
            pickup.TryAddCargo("wood", 4);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(townStation.Id, waitForFull: false),
                            new RoadVehicleOrder(pickup.Id, waitForFull: false)
                        })).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            int townNode = world.Map.GetTileId(townStation.Coordinate);
            bool leftTownEmpty = false;
            for (int tick = 0; tick < 4000; tick++)
            {
                bool wasServicingTown = vehicle.IsServicingStop && vehicle.EdgeFromNodeId == townNode;
                RoadVehicleMovement.AdvanceAll(world, catalog);
                Assert.That(townStation.Cargo.IsEmpty, Is.True);
                if (wasServicingTown &&
                    !vehicle.IsServicingStop &&
                    vehicle.Cargo.IsEmpty &&
                    world.CompanyCash > cashBefore)
                {
                    leftTownEmpty = true;
                    break;
                }
            }

            Assert.That(leftTownEmpty, Is.True);
            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(8L * 4));
        }

        [Test]
        public void Unload_AtProducerOnlyStation_DoesNotCreditCash()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedTwoForestStations(
                out RoadStation a,
                out RoadStation b);
            long cashBefore = world.CompanyCash;
            a.TryAddCargo("wood", 4);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(b.Id, waitForFull: false),
                            new RoadVehicleOrder(a.Id, waitForFull: false)
                        })).IsAccepted,
                Is.True);

            bool unloaded = false;
            for (int tick = 0; tick < 4000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog);
                if (b.Cargo.GetAmount("wood") > 0)
                {
                    unloaded = true;
                    break;
                }
            }

            Assert.That(unloaded, Is.True);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
        }

        private static ContentCatalog CreateWoodChainCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14)
            };
            var industries = new[]
            {
                new IndustryDefinition(
                    "forest",
                    "industry.forest",
                    1,
                    Array.Empty<CargoAmountDefinition>(),
                    new[] { new CargoAmountDefinition("wood", 4) }),
                new IndustryDefinition(
                    "sawmill",
                    "industry.sawmill",
                    2,
                    new[] { new CargoAmountDefinition("wood", 2) },
                    new[] { new CargoAmountDefinition("planks", 1) })
            };
            return new ContentCatalog(cargo, industries);
        }

        private static GeneratedWorld CreateForestStationWorld(out RoadStation station)
        {
            GeneratedWorld world = GenerateForestWorld(48011UL);
            station = BuildAdjacentStation(world, world.Industries[0].Coordinate);
            return world;
        }

        private static GeneratedWorld CreateSawmillStationWorld(out RoadStation station)
        {
            GeneratedWorld world = GenerateForestAndSawmillWorld(48012UL);
            station = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            return world;
        }

        private static GeneratedWorld CreateTownStationWorld(out RoadStation station)
        {
            GeneratedWorld world = GenerateForestWorld(48013UL);
            station = BuildAdjacentStation(world, world.TownAnchors[0]);
            return world;
        }

        private static GeneratedWorld CreateConnectedPickupAndConsumerWorld(
            out RoadStation pickup,
            out RoadStation consumer)
        {
            GeneratedWorld world = GenerateForestAndSawmillWorld(48014UL);
            pickup = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate);
            consumer = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, pickup.Coordinate, consumer.Coordinate, 10);
            return world;
        }

        private static GeneratedWorld CreateConnectedPickupAndTownWorld(
            out RoadStation pickup,
            out RoadStation townStation)
        {
            GeneratedWorld world = GenerateForestWorld(48016UL);
            pickup = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate);
            townStation = BuildAdjacentStation(world, world.TownAnchors[0]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, pickup.Coordinate, townStation.Coordinate, 10);
            return world;
        }

        private static GeneratedWorld CreateConnectedTwoForestStations(
            out RoadStation a,
            out RoadStation b)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 48015UL);
            Assert.That(world.Industries.Count, Is.GreaterThanOrEqualTo(2));
            a = BuildAdjacentStation(world, world.Industries[0].Coordinate);
            b = BuildAdjacentStation(world, world.Industries[1].Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, a.Coordinate, b.Coordinate, 10);
            return world;
        }

        private static GeneratedWorld GenerateForestWorld(ulong seed)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, seed);
        }

        private static GeneratedWorld GenerateForestAndSawmillWorld(ulong seed)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0),
                new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 80)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, seed);
        }

        private static PlacedIndustry FindIndustry(GeneratedWorld world, string industryId)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (string.Equals(world.Industries[index].IndustryId, industryId, StringComparison.Ordinal))
                {
                    return world.Industries[index];
                }
            }

            throw new InvalidOperationException($"Industry '{industryId}' was not placed.");
        }

        private static int _stationBuildTick = 1;

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
            var cameFrom = new System.Collections.Generic.Dictionary<int, int>();
            var queue = new System.Collections.Generic.Queue<TileCoordinate>();
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
            var path = new System.Collections.Generic.List<TileCoordinate>();
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
