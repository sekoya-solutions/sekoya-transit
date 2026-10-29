#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RailVehicleCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuyRailVehicle_AtStation_AppliesAndRecordsForReplay()
        {
            GeneratedWorld world = CreateWorldWithStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.RailVehicles, Has.Count.EqualTo(1));
            Assert.That(world.RailVehicles[0].Id, Is.EqualTo(0));
            Assert.That(world.RailVehicles[0].TypeId, Is.EqualTo(RailVehicle.StarterTypeId));
            Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(0));
            Assert.That(world.RailVehicles[0].CargoCapacity, Is.EqualTo(0));
            Assert.That(world.RailVehicles[0].IsOnEdge, Is.False);
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));

            GeneratedWorld replayed = CreateWorldWithStation(out RailStation replayStation);
            processor.Log.Replay(replayed);
            Assert.That(replayed.RailVehicles, Has.Count.EqualTo(1));
            Assert.That(replayed.RailVehicles[0].EdgeFromNodeId, Is.EqualTo(replayed.Map.GetTileId(replayStation.Coordinate)));
        }

        [Test]
        public void BuyRailVehicle_MissingStation_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorldWithStation(out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuyRailVehicleCommand(99));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("does not exist"));
            Assert.That(world.RailVehicles, Is.Empty);
            Assert.That(processor.Log.Entries, Is.Empty);
        }

        [Test]
        public void SetRailVehicleOrders_ValidStations_AppliesAndReplays()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);

            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new SetRailVehicleOrdersCommand(0, new[] { a.Id, b.Id }));

            Assert.That(result.IsAccepted, Is.True);
            CollectionAssert.AreEqual(new[] { a.Id, b.Id }, world.RailVehicles[0].OrderStationIds);

            GeneratedWorld replayed = CreateConnectedTwoStationWorld(out RailStation ra, out RailStation rb);
            processor.Log.Replay(replayed);
            Assert.That(replayed.RailVehicles, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(new[] { ra.Id, rb.Id }, replayed.RailVehicles[0].OrderStationIds);
        }

        [Test]
        public void SetRailVehicleOrders_StandingAtLaterStop_StartsAtThatStop()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);

            Assert.That(
                processor.Process(new Tick(2), new SetRailVehicleOrdersCommand(0, new[] { b.Id, a.Id })).IsAccepted,
                Is.True);

            Assert.That(world.RailVehicles[0].OrderIndex, Is.EqualTo(1));
        }

        [Test]
        public void SetRailVehicleOrders_MissingStation_Rejects()
        {
            GeneratedWorld world = CreateWorldWithStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);

            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new SetRailVehicleOrdersCommand(0, new[] { station.Id, 99 }));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("missing"));
            Assert.That(world.RailVehicles[0].OrderStationIds, Is.Empty);
        }

        [Test]
        public void SellRailVehicle_Existing_RemovesAndReplays()
        {
            GeneratedWorld world = CreateWorldWithStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);

            CommandProcessingResult result = processor.Process(new Tick(2), new SellRailVehicleCommand(0));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.RailVehicles, Is.Empty);

            GeneratedWorld replayed = CreateWorldWithStation(out _);
            processor.Log.Replay(replayed);
            Assert.That(replayed.RailVehicles, Is.Empty);
        }

        [Test]
        public void SellRailVehicle_WhileParkedAtStationWithCargo_RemovesImmediately()
        {
            GeneratedWorld world = CreateWorldWithStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            RailVehicle vehicle = world.RailVehicles[0];
            vehicle.Cargo.Add("wood", 4);
            Assert.That(vehicle.IsOnEdge, Is.False);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(4));

            CommandProcessingResult result = processor.Process(new Tick(3), new SellRailVehicleCommand(vehicle.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.RailVehicles, Is.Empty);
        }

        [Test]
        public void AddRailWagon_UpToMax_IncreasesCapacityAndRejectsBeyond()
        {
            GeneratedWorld world = CreateWorldWithStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);

            for (int wagon = 1; wagon <= RailVehicle.LegacyMaxWagons; wagon++)
            {
                CommandProcessingResult add = processor.Process(new Tick(wagon + 1), new AddRailWagonCommand(0));
                Assert.That(add.IsAccepted, Is.True);
                Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(wagon));
                Assert.That(world.RailVehicles[0].CargoCapacity, Is.EqualTo(wagon * RailVehicle.LegacyCapacityPerWagon));
            }

            CommandProcessingResult overMax = processor.Process(
                new Tick(RailVehicle.LegacyMaxWagons + 2),
                new AddRailWagonCommand(0));
            Assert.That(overMax.IsAccepted, Is.False);
            Assert.That(overMax.RejectionReason, Does.Contain("maximum"));
            Assert.That(world.RailVehicles[0].WagonCount, Is.EqualTo(RailVehicle.LegacyMaxWagons));
        }

        [Test]
        public void SellRailWagon_DiscardsExcessCargo_AndRejectsWhenEmpty()
        {
            GeneratedWorld world = CreateWorldWithStation(out RailStation station);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(station.Id)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(3), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(4), new AddRailWagonCommand(0)).IsAccepted, Is.True);
            RailVehicle vehicle = world.RailVehicles[0];
            vehicle.Cargo.Add("wood", 25);

            CommandProcessingResult sell = processor.Process(new Tick(5), new SellRailWagonCommand(0));
            Assert.That(sell.IsAccepted, Is.True);
            Assert.That(vehicle.WagonCount, Is.EqualTo(2));
            Assert.That(vehicle.CargoCapacity, Is.EqualTo(20));
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(20));

            Assert.That(processor.Process(new Tick(6), new SellRailWagonCommand(0)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(7), new SellRailWagonCommand(0)).IsAccepted, Is.True);
            Assert.That(vehicle.WagonCount, Is.EqualTo(0));
            Assert.That(vehicle.CargoCapacity, Is.EqualTo(0));
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(0));

            CommandProcessingResult empty = processor.Process(new Tick(8), new SellRailWagonCommand(0));
            Assert.That(empty.IsAccepted, Is.False);
            Assert.That(empty.RejectionReason, Does.Contain("no wagons"));
        }

        private static GeneratedWorld CreateWorldWithStation(out RailStation station)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindStationCandidate(world);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(tile, out RailStation? found), Is.True);
            station = found!;
            return world;
        }

        private static GeneratedWorld CreateConnectedTwoStationWorld(out RailStation a, out RailStation b)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townA = world.TownAnchors[0];
            TileCoordinate townB = world.TownAnchors[1];
            TileCoordinate stationA = FindOrthogonalEmptyNeighbor(world, townA);
            TileCoordinate stationB = FindOrthogonalEmptyNeighbor(world, townB);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(stationA)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(stationB)).IsAccepted, Is.True);
            ConnectStationsWithRoads(world, processor, stationA, stationB, startingTick: 3);

            Assert.That(world.TryGetRailStationAt(stationA, out RailStation? foundA), Is.True);
            Assert.That(world.TryGetRailStationAt(stationB, out RailStation? foundB), Is.True);
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
                            && world.Map.GetRailStationId(next) == TileMap.NoRailStationId);
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

                if (world.Map.GetRailTypeId(step) != TileMap.NoRailTypeId)
                {
                    continue;
                }

                Assert.That(processor.Process(new Tick(tick++), new BuildRailCommand(step)).IsAccepted, Is.True);
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
                    world.Map.GetRailTypeId(candidate) == TileMap.NoRailTypeId &&
                    world.Map.GetRailStationId(candidate) == TileMap.NoRailStationId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }
    }
}
