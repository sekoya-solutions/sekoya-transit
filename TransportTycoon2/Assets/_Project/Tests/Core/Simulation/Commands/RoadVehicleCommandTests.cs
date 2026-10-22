#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RoadVehicleCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuyRoadVehicle_AtStation_AppliesAndRecordsForReplay()
        {
            GeneratedWorld world = CreateWorldWithStation(out RoadStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuyRoadVehicleCommand(station.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.RoadVehicles, Has.Count.EqualTo(1));
            Assert.That(world.RoadVehicles[0].Id, Is.EqualTo(0));
            Assert.That(world.RoadVehicles[0].TypeId, Is.EqualTo(RoadVehicle.PlaceholderTypeId));
            Assert.That(world.RoadVehicles[0].IsOnEdge, Is.False);
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));

            GeneratedWorld replayed = CreateWorldWithStation(out RoadStation replayStation);
            processor.Log.Replay(replayed);
            Assert.That(replayed.RoadVehicles, Has.Count.EqualTo(1));
            Assert.That(replayed.RoadVehicles[0].EdgeFromNodeId, Is.EqualTo(replayed.Map.GetTileId(replayStation.Coordinate)));
        }

        [Test]
        public void BuyRoadVehicle_MissingStation_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorldWithStation(out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuyRoadVehicleCommand(99));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("does not exist"));
            Assert.That(world.RoadVehicles, Is.Empty);
            Assert.That(processor.Log.Entries, Is.Empty);
        }

        [Test]
        public void SetRoadVehicleOrders_ValidStations_AppliesAndReplays()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RoadStation a, out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);

            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new SetRoadVehicleOrdersCommand(0, new[] { a.Id, b.Id }));

            Assert.That(result.IsAccepted, Is.True);
            CollectionAssert.AreEqual(new[] { a.Id, b.Id }, world.RoadVehicles[0].OrderStationIds);

            GeneratedWorld replayed = CreateConnectedTwoStationWorld(out RoadStation ra, out RoadStation rb);
            processor.Log.Replay(replayed);
            Assert.That(replayed.RoadVehicles, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(new[] { ra.Id, rb.Id }, replayed.RoadVehicles[0].OrderStationIds);
        }

        [Test]
        public void SetRoadVehicleOrders_MissingStation_Rejects()
        {
            GeneratedWorld world = CreateWorldWithStation(out RoadStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(station.Id)).IsAccepted, Is.True);

            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new SetRoadVehicleOrdersCommand(0, new[] { station.Id, 99 }));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("missing"));
            Assert.That(world.RoadVehicles[0].OrderStationIds, Is.Empty);
        }

        [Test]
        public void SellRoadVehicle_Existing_RemovesAndReplays()
        {
            GeneratedWorld world = CreateWorldWithStation(out RoadStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(station.Id)).IsAccepted, Is.True);

            CommandProcessingResult result = processor.Process(new Tick(2), new SellRoadVehicleCommand(0));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.RoadVehicles, Is.Empty);

            GeneratedWorld replayed = CreateWorldWithStation(out _);
            processor.Log.Replay(replayed);
            Assert.That(replayed.RoadVehicles, Is.Empty);
        }

        private static GeneratedWorld CreateWorldWithStation(out RoadStation station)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindStationCandidate(world);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? found), Is.True);
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
            ConnectStationsWithRoads(world, processor, stationA, stationB, startingTick: 3);

            Assert.That(world.TryGetRoadStationAt(stationA, out RoadStation? foundA), Is.True);
            Assert.That(world.TryGetRoadStationAt(stationB, out RoadStation? foundB), Is.True);
            a = foundA!;
            b = foundB!;
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
            TileCoordinate? found = null;
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
                    found = current;
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

                    bool isDestination = next.Equals(toStation);
                    bool canTraverse = isDestination
                        || (world.Map.GetObjectId(next) == TileMap.NoObjectId
                            && world.Map.GetStationId(next) == TileMap.NoStationId);
                    if (!canTraverse)
                    {
                        continue;
                    }

                    cameFrom[nextId] = world.Map.GetTileId(current);
                    queue.Enqueue(next);
                }
            }

            Assert.That(found.HasValue, Is.True, "No empty path between stations.");
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
            return WorldGenerator.Generate(settings, 45001UL);
        }

        private static TileCoordinate FindStationCandidate(GeneratedWorld world)
        {
            return FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
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
