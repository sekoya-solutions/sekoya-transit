#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class RailVehicleMovementTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Advance_TwoStationLoop_ReachesOtherStationAndReturns()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(new Tick(2), new SetRailVehicleOrdersCommand(0, new[] { a.Id, b.Id })).IsAccepted,
                Is.True);

            RailVehicle vehicle = world.RailVehicles[0];
            int nodeA = world.Map.GetTileId(a.Coordinate);
            int nodeB = world.Map.GetTileId(b.Coordinate);
            Assert.That(vehicle.EdgeFromNodeId, Is.EqualTo(nodeA));

            bool reachedB = false;
            bool returnedToA = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                RailVehicleMovement.AdvanceAll(world);
                if (!vehicle.IsOnEdge && vehicle.EdgeFromNodeId == nodeB)
                {
                    reachedB = true;
                }

                if (reachedB && !vehicle.IsOnEdge && vehicle.EdgeFromNodeId == nodeA)
                {
                    returnedToA = true;
                    break;
                }
            }

            Assert.That(reachedB, Is.True, "Vehicle should reach station B.");
            Assert.That(returnedToA, Is.True, "Vehicle should loop back to station A.");
            Assert.That(vehicle.IsStuck, Is.False);
        }

        [Test]
        public void Advance_IdenticalInputs_YieldIdenticalPositions()
        {
            GeneratedWorld world1 = CreateConnectedTwoStationWorld(out RailStation a1, out RailStation b1);
            GeneratedWorld world2 = CreateConnectedTwoStationWorld(out RailStation a2, out RailStation b2);
            BuyAndOrder(world1, a1, b1);
            BuyAndOrder(world2, a2, b2);

            for (int tick = 0; tick < 40; tick++)
            {
                RailVehicleMovement.AdvanceAll(world1);
                RailVehicleMovement.AdvanceAll(world2);
            }

            RailVehicle v1 = world1.RailVehicles[0];
            RailVehicle v2 = world2.RailVehicles[0];
            Assert.That(v1.IsOnEdge, Is.EqualTo(v2.IsOnEdge));
            Assert.That(v1.EdgeFromNodeId, Is.EqualTo(v2.EdgeFromNodeId));
            Assert.That(v1.EdgeToNodeId, Is.EqualTo(v2.EdgeToNodeId));
            Assert.That(v1.ProgressAlongEdge, Is.EqualTo(v2.ProgressAlongEdge));
            Assert.That(v1.OrderIndex, Is.EqualTo(v2.OrderIndex));
        }

        [Test]
        public void Advance_UnreachableDestination_SetsStuckThenClearsWhenConnected()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate stationA = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            TileCoordinate stationB = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[1]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(stationA)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(stationB)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(stationA, out RailStation? foundA), Is.True);
            Assert.That(world.TryGetRailStationAt(stationB, out RailStation? foundB), Is.True);
            RailStation a = foundA!;
            RailStation b = foundB!;
            Assert.That(processor.Process(new Tick(3), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(new Tick(4), new SetRailVehicleOrdersCommand(0, new[] { a.Id, b.Id })).IsAccepted,
                Is.True);

            // First stop is the spawn station: one tick starts service, the next leaves and pathfinds to B.
            RailVehicleMovement.AdvanceAll(world);
            RailVehicleMovement.AdvanceAll(world);
            Assert.That(world.RailVehicles[0].IsStuck, Is.True);

            ConnectStationsWithRoads(world, processor, stationA, stationB, startingTick: 5);

            bool cleared = false;
            for (int tick = 0; tick < 8; tick++)
            {
                RailVehicleMovement.AdvanceAll(world);
                if (!world.RailVehicles[0].IsStuck)
                {
                    cleared = true;
                    break;
                }
            }

            Assert.That(cleared, Is.True);
        }

        [Test]
        public void Advance_FewerThanTwoOrders_RemainsIdle()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RailStation a, out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);
            int nodeA = world.Map.GetTileId(a.Coordinate);

            for (int tick = 0; tick < 10; tick++)
            {
                RailVehicleMovement.AdvanceAll(world);
            }

            RailVehicle vehicle = world.RailVehicles[0];
            Assert.That(vehicle.IsOnEdge, Is.False);
            Assert.That(vehicle.EdgeFromNodeId, Is.EqualTo(nodeA));
            Assert.That(vehicle.ProgressAlongEdge, Is.EqualTo(FixedPoint.Zero));
        }

        [Test]
        public void SetOrders_FewerThanTwoWhileOnEdge_SnapsOffRoadAndIsParked()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RailStation a, out RailStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(new Tick(2), new SetRailVehicleOrdersCommand(0, new[] { a.Id, b.Id })).IsAccepted,
                Is.True);

            RailVehicle vehicle = world.RailVehicles[0];
            bool onEdge = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                RailVehicleMovement.AdvanceAll(world);
                if (vehicle.IsOnEdge)
                {
                    onEdge = true;
                    break;
                }
            }

            Assert.That(onEdge, Is.True, "Vehicle should be mid-edge before clearing orders.");
            int fromNode = vehicle.EdgeFromNodeId;

            Assert.That(
                processor.Process(new Tick(3), new SetRailVehicleOrdersCommand(0, Array.Empty<int>())).IsAccepted,
                Is.True);

            Assert.That(vehicle.IsOnEdge, Is.False);
            Assert.That(vehicle.EdgeFromNodeId, Is.EqualTo(fromNode));
            Assert.That(vehicle.ProgressAlongEdge, Is.EqualTo(FixedPoint.Zero));
            Assert.That(vehicle.Orders, Is.Empty);
        }

        [Test]
        public void BuildAutoOrders_ConnectedStations_IncludesStartAndReachablePeers()
        {
            GeneratedWorld world = CreateConnectedTwoStationWorld(out RailStation a, out RailStation b);
            int[] orders = RailVehicleMovement.BuildAutoOrders(world, a.Id);

            Assert.That(orders.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(orders[0], Is.EqualTo(a.Id));
            Assert.That(orders, Does.Contain(b.Id));
        }

        private static void BuyAndOrder(GeneratedWorld world, RailStation a, RailStation b)
        {
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRailVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(new Tick(2), new SetRailVehicleOrdersCommand(0, new[] { a.Id, b.Id })).IsAccepted,
                Is.True);
        }

        private static GeneratedWorld CreateConnectedTwoStationWorld(out RailStation a, out RailStation b)
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate stationA = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            TileCoordinate stationB = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[1]);
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
            return WorldGenerator.Generate(settings, 45002UL);
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
