#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class RoadStationCargoCapDecayTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void TryAddCargo_RespectsPlaceholderCapacity()
        {
            var station = new RoadStation(0, new TileCoordinate(1, 1));
            int added = station.TryAddCargo("wood", RoadStation.PlaceholderCargoCapacity + 10);
            Assert.That(added, Is.EqualTo(RoadStation.PlaceholderCargoCapacity));
            Assert.That(station.Cargo.TotalAmount, Is.EqualTo(RoadStation.PlaceholderCargoCapacity));
            Assert.That(station.FreeCargoCapacity, Is.EqualTo(0));
            Assert.That(station.TryAddCargo("coal", 1), Is.EqualTo(0));
        }

        [Test]
        public void Constructor_ClampsOverCapacityCargo()
        {
            var stacks = new[]
            {
                new CargoAmount("wood", RoadStation.PlaceholderCargoCapacity + 5),
                new CargoAmount("coal", 3)
            };
            var station = new RoadStation(1, new TileCoordinate(2, 2), stacks);
            Assert.That(station.Cargo.TotalAmount, Is.EqualTo(RoadStation.PlaceholderCargoCapacity));
            // Trim drains ascending cargo-id order (coal before wood).
            Assert.That(station.Cargo.Stacks.Count, Is.EqualTo(1));
            Assert.That(station.Cargo.Stacks[0].CargoId, Is.EqualTo("wood"));
            Assert.That(station.Cargo.Stacks[0].Amount, Is.EqualTo(RoadStation.PlaceholderCargoCapacity));
        }

        [Test]
        public void Decay_RemovesAmountOnInterval_Deterministically()
        {
            GeneratedWorld world = CreateWorldWithIndustryStation(out RoadStation station);
            station.Cargo.Clear();
            station.TryAddCargo("coal", 2);
            station.TryAddCargo("wood", 3);

            RoadStationCargoDecay.AdvanceAll(world, new Tick(RoadStationCargoDecay.DecayIntervalTicks - 1));
            Assert.That(station.Cargo.TotalAmount, Is.EqualTo(5));

            RoadStationCargoDecay.AdvanceAll(world, new Tick(RoadStationCargoDecay.DecayIntervalTicks));
            Assert.That(station.Cargo.TotalAmount, Is.EqualTo(5 - RoadStationCargoDecay.DecayAmount));
            Assert.That(station.Cargo.Stacks[0].CargoId, Is.EqualTo("coal"));
            Assert.That(station.Cargo.Stacks[0].Amount, Is.EqualTo(1));

            GeneratedWorld world2 = CreateWorldWithIndustryStation(out RoadStation station2);
            station2.Cargo.Clear();
            station2.TryAddCargo("coal", 2);
            station2.TryAddCargo("wood", 3);
            RoadStationCargoDecay.AdvanceAll(world2, new Tick(RoadStationCargoDecay.DecayIntervalTicks));
            Assert.That(station2.Cargo.TotalAmount, Is.EqualTo(station.Cargo.TotalAmount));
            Assert.That(station2.Cargo.Stacks[0].Amount, Is.EqualTo(station.Cargo.Stacks[0].Amount));
        }

        [Test]
        public void Seeding_RespectsFreeCapacity()
        {
            GeneratedWorld world = CreateWorldWithIndustryStation(out RoadStation station);
            station.Cargo.Clear();
            station.TryAddCargo("coal", RoadStation.PlaceholderCargoCapacity - 5);
            RoadStationCargoSeeding.SeedAtIndustryAdjacentStations(
                world,
                RoadStationCargoSeeding.DemoCargoId,
                amount: 20);
            Assert.That(station.Cargo.TotalAmount, Is.EqualTo(RoadStation.PlaceholderCargoCapacity));
            Assert.That(station.FreeCargoCapacity, Is.EqualTo(0));
        }

        [Test]
        public void Unload_WhenStationFull_LeavesCargoOnVehicleAndCompletesUnload()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RoadStation a, out RoadStation b);
            a.Cargo.Clear();
            b.Cargo.Clear();
            while (b.FreeCargoCapacity > 0)
            {
                b.TryAddCargo("coal", Math.Min(5, b.FreeCargoCapacity));
            }

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

            RoadVehicle vehicle = world.RoadVehicles[0];
            vehicle.Cargo.Add("wood", 4);

            bool leftWithWood = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world);
                if (!vehicle.IsServicingStop &&
                    vehicle.Cargo.TotalAmount == 4 &&
                    vehicle.Cargo.Stacks[0].CargoId == "wood")
                {
                    leftWithWood = true;
                    break;
                }
            }

            Assert.That(leftWithWood, Is.True);
            Assert.That(b.Cargo.TotalAmount, Is.EqualTo(RoadStation.PlaceholderCargoCapacity));
            Assert.That(b.Cargo.Stacks[0].CargoId, Is.EqualTo("coal"));
        }

        private static GeneratedWorld CreateWorldWithIndustryStation(out RoadStation station)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate industry = world.Industries[0].Coordinate;
            TileCoordinate stationTile = FindOrthogonalEmptyNeighbor(world, industry);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(stationTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(stationTile, out RoadStation? found), Is.True);
            station = found!;
            return world;
        }

        private static GeneratedWorld CreateConnectedTwoStationWorld(out RoadStation a, out RoadStation b)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townA = world.TownAnchors[0];
            TileCoordinate townB = world.TownAnchors[1];
            TileCoordinate stationA = FindOrthogonalEmptyNeighbor(world, townA);
            TileCoordinate stationB = FindOrthogonalEmptyNeighbor(world, townB);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(stationA)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new BuildRoadStationCommand(stationB)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(stationA, out RoadStation? foundA), Is.True);
            Assert.That(world.TryGetRoadStationAt(stationB, out RoadStation? foundB), Is.True);
            a = foundA!;
            b = foundB!;
            ConnectStationsWithRoads(world, processor, stationA, stationB, startingTick: 3);
            return world;
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
            var path = new System.Collections.Generic.List<TileCoordinate>();
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

        private static GeneratedWorld CreateWorld()
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 16),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, 47001UL);
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

                if (world.Map.GetObjectId(candidate) == TileMap.NoObjectId &&
                    world.Map.GetRoadTypeId(candidate) == TileMap.NoRoadTypeId &&
                    world.Map.GetStationId(candidate) == TileMap.NoStationId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }
    }
}
