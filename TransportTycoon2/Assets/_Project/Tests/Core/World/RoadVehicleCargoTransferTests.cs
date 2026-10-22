#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class RoadVehicleCargoTransferTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Advance_OnArrival_UnloadsThenLoadsAtTransferRate()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RoadStation a, out RoadStation b);
            a.Cargo.Clear();
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
            b.Cargo.Clear();
            b.Cargo.Add("wood", 8);

            int nodeB = world.Map.GetTileId(b.Coordinate);
            bool leftBWithCargo = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                bool wasServicingB = vehicle.IsServicingStop && vehicle.EdgeFromNodeId == nodeB;
                RoadVehicleMovement.AdvanceAll(world);
                if (wasServicingB && !vehicle.IsServicingStop && vehicle.Cargo.TotalAmount > 0)
                {
                    leftBWithCargo = true;
                    break;
                }
            }

            Assert.That(leftBWithCargo, Is.True);
            // Unload 4 wood into B (B becomes 12), then load up to capacity 10.
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(RoadVehicle.PlaceholderCargoCapacity));
            Assert.That(vehicle.Cargo.Stacks[0].CargoId, Is.EqualTo("wood"));
            Assert.That(b.Cargo.TotalAmount, Is.EqualTo(2));
        }

        [Test]
        public void Advance_WaitForFull_StaysUntilFullWhenCargoArrives()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RoadStation a, out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(b.Id, waitForFull: true),
                            new RoadVehicleOrder(a.Id, waitForFull: false)
                        })).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            b.Cargo.Add("wood", 4);

            bool servicingAtB = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world);
                if (vehicle.IsServicingStop && vehicle.EdgeFromNodeId == world.Map.GetTileId(b.Coordinate))
                {
                    servicingAtB = true;
                    if (vehicle.IsUnloadComplete && vehicle.Cargo.TotalAmount == 4)
                    {
                        break;
                    }
                }
            }

            Assert.That(servicingAtB, Is.True);
            Assert.That(vehicle.IsServicingStop, Is.True);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(4));

            b.Cargo.Add("wood", 10);
            bool left = false;
            for (int tick = 0; tick < 40; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world);
                if (!vehicle.IsServicingStop && vehicle.Cargo.TotalAmount == RoadVehicle.PlaceholderCargoCapacity)
                {
                    left = true;
                    break;
                }
            }

            Assert.That(left, Is.True);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(RoadVehicle.PlaceholderCargoCapacity));
        }

        [Test]
        public void Advance_WithoutWaitForFull_LeavesPartiallyLoaded()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RoadStation a, out RoadStation b);
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
            b.Cargo.Add("wood", 3);

            bool leftWithPartial = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world);
                if (!vehicle.IsServicingStop &&
                    vehicle.Cargo.TotalAmount == 3 &&
                    vehicle.EdgeFromNodeId == world.Map.GetTileId(b.Coordinate))
                {
                    leftWithPartial = true;
                    break;
                }

                // After leaving B it may already be on an edge toward A.
                if (!vehicle.IsServicingStop && vehicle.Cargo.TotalAmount == 3 && vehicle.IsOnEdge)
                {
                    leftWithPartial = true;
                    break;
                }
            }

            Assert.That(leftWithPartial, Is.True);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(3));
        }

        [Test]
        public void Capacity_RejectsCargoAboveLimit_ViaTransfer()
        {
            var inventory = new CargoInventory();
            inventory.Add("wood", RoadVehicle.PlaceholderCargoCapacity);
            var destination = new CargoInventory();
            int moved = inventory.TransferTo(destination, 5, destinationFreeCapacity: 0);
            Assert.That(moved, Is.EqualTo(0));
            Assert.That(inventory.TotalAmount, Is.EqualTo(RoadVehicle.PlaceholderCargoCapacity));
        }

        [Test]
        public void Transfer_IdenticalInputs_YieldIdenticalInventories()
        {
            var source1 = new CargoInventory();
            var source2 = new CargoInventory();
            source1.Add("coal", 3);
            source1.Add("wood", 5);
            source2.Add("coal", 3);
            source2.Add("wood", 5);
            var dest1 = new CargoInventory();
            var dest2 = new CargoInventory();

            source1.TransferTo(dest1, RoadVehicleCargoTransfer.UnitsPerTick, 10);
            source2.TransferTo(dest2, RoadVehicleCargoTransfer.UnitsPerTick, 10);

            Assert.That(dest1.TotalAmount, Is.EqualTo(dest2.TotalAmount));
            Assert.That(dest1.Stacks[0].CargoId, Is.EqualTo(dest2.Stacks[0].CargoId));
            Assert.That(dest1.Stacks[0].Amount, Is.EqualTo(dest2.Stacks[0].Amount));
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
            return WorldGenerator.Generate(settings, 46001UL);
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
