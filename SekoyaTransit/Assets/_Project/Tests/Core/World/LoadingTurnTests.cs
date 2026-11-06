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
    /// <summary>
    /// A vehicle that cannot load at a site must neither hold the site's loading turn nor wait there forever for a
    /// full load (Issue #322).
    /// </summary>
    [TestFixture]
    public sealed class LoadingTurnTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly bool[] _waitAtSecondStop = { false, true };
        private static readonly GameDate _january = new GameDate(1900, 1, 1);
        private const int _setupTicks = 8000;

        [TestCase(RouteMode.Road)]
        [TestCase(RouteMode.Rail)]
        public void DistributeIndustry_LoaderWithoutDestinationHasTurn_NextLoaderLoads(RouteMode mode)
        {
            ContentCatalog catalog = CreateIndustryCatalog();
            GeneratedWorld world = CreateForestWorld(catalog, mode, out Stop forestA, out Stop forestB, out Stop sawmill);
            PlacedIndustry forest = FindIndustry(world, "forest");
            forest.Cargo.Clear();

            // Bought first, so it has the lower id and the first turn: no other stop of its route accepts wood.
            int blocker = BuyVehicle(world, catalog, mode, forestA.Id, forestB.Id);
            int loader = BuyVehicle(world, catalog, mode, sawmill.Id, forestA.Id);

            // Without a catalog the leave rule cannot tell destinations apart, so both keep waiting for a full load.
            DriveUntil(
                world,
                mode,
                catalog: null,
                () => IsWaitingAt(world, mode, blocker, forestB) && IsWaitingAt(world, mode, loader, forestA));

            Assert.That(forest.TryAddCargo("wood", 5), Is.EqualTo(5));
            for (int tick = 0; tick < 3; tick++)
            {
                IndustryCargoLoading.Distribute(world, catalog, _january);
            }

            Assert.That(CargoOf(world, mode, blocker).TotalAmount, Is.EqualTo(0));
            Assert.That(CargoOf(world, mode, loader).GetAmount("wood"), Is.EqualTo(3));
            Assert.That(forest.Cargo.GetAmount("wood"), Is.EqualTo(2));
            Assert.That(forest.MonthTransported, Is.EqualTo(3L));
        }

        [Test]
        public void DistributeTown_LoaderWithoutDestinationHasTurn_NextLoaderLoads()
        {
            ContentCatalog catalog = CreateTownCatalog();
            GeneratedWorld world = CreateTwoTownWorld(catalog, out Stop townA1, out Stop townA2, out Stop townB);
            Town town = world.Towns[0];

            // Both stops of the blocker's route serve the same town, so it may not load passengers there.
            int blocker = BuyVehicle(world, catalog, RouteMode.Road, townA1.Id, townA2.Id);
            int loader = BuyVehicle(world, catalog, RouteMode.Road, townB.Id, townA1.Id);
            DriveUntil(
                world,
                RouteMode.Road,
                catalog: null,
                () => IsWaitingAt(world, RouteMode.Road, blocker, townA2) &&
                      IsWaitingAt(world, RouteMode.Road, loader, townA1));

            town.WaitingCargo.Clear();
            Assert.That(
                town.TryAddWaitingCargo("passengers", 5, catalog.TownNeeds.PassengerWaitingCapacity),
                Is.EqualTo(5));
            for (int tick = 0; tick < 3; tick++)
            {
                TownCargoLoading.Distribute(world, catalog, _january);
            }

            Assert.That(CargoOf(world, RouteMode.Road, blocker).TotalAmount, Is.EqualTo(0));
            Assert.That(CargoOf(world, RouteMode.Road, loader).GetAmount("passengers"), Is.EqualTo(3));
            Assert.That(town.WaitingCargo.GetAmount("passengers"), Is.EqualTo(2));
        }

        [TestCase(RouteMode.Road)]
        [TestCase(RouteMode.Rail)]
        public void TryFinishService_WaitForFullWithoutDestination_LeavesAtOnce(RouteMode mode)
        {
            ContentCatalog catalog = CreateIndustryCatalog();
            GeneratedWorld world = CreateForestWorld(catalog, mode, out Stop forestA, out Stop forestB, out _);
            PlacedIndustry forest = FindIndustry(world, "forest");
            forest.Cargo.Clear();
            Assert.That(forest.TryAddCargo("wood", 40), Is.EqualTo(40));
            int vehicle = BuyVehicle(world, catalog, mode, forestA.Id, forestB.Id);

            int nodeB = world.Map.GetTileId(forestB.Coordinate);
            bool leftForestB = false;
            for (int tick = 0; tick < _setupTicks && !leftForestB; tick++)
            {
                Advance(world, mode, catalog);
                Assert.That(
                    IsWaitingAt(world, mode, vehicle, forestB),
                    Is.False,
                    "The vehicle stayed at a wait-for-full stop it can never load at.");
                leftForestB = !IsServicing(world, mode, vehicle) && EdgeFromNode(world, mode, vehicle) == nodeB;
            }

            Assert.That(leftForestB, Is.True);
            Assert.That(CargoOf(world, mode, vehicle).IsEmpty, Is.True);
            Assert.That(forest.Cargo.GetAmount("wood"), Is.EqualTo(40));
        }

        [TestCase(RouteMode.Road)]
        [TestCase(RouteMode.Rail)]
        public void TryFinishService_WaitForFullWithDestinationAtEmptyIndustry_KeepsWaitingThenFills(RouteMode mode)
        {
            ContentCatalog catalog = CreateIndustryCatalog();
            GeneratedWorld world = CreateForestWorld(catalog, mode, out Stop forestA, out _, out Stop sawmill);
            PlacedIndustry forest = FindIndustry(world, "forest");
            forest.Cargo.Clear();
            int vehicle = BuyVehicle(world, catalog, mode, sawmill.Id, forestA.Id);

            DriveUntil(world, mode, catalog, () => IsWaitingAt(world, mode, vehicle, forestA));
            for (int tick = 0; tick < 500; tick++)
            {
                Advance(world, mode, catalog);
            }

            Assert.That(IsWaitingAt(world, mode, vehicle, forestA), Is.True);

            int capacity = FreeCapacityOf(world, mode, vehicle);
            Assert.That(forest.TryAddCargo("wood", 40), Is.EqualTo(40));
            DriveUntil(world, mode, catalog, () => !IsServicing(world, mode, vehicle));
            Assert.That(CargoOf(world, mode, vehicle).GetAmount("wood"), Is.EqualTo(capacity));
        }

        private readonly struct Stop
        {
            public Stop(int id, TileCoordinate coordinate)
            {
                Id = id;
                Coordinate = coordinate;
            }

            public int Id { get; }

            public TileCoordinate Coordinate { get; }
        }

        private static ContentCatalog CreateIndustryCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2)
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
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                new TownNeedLevelAmounts(10, 10, 10),
                allNeedsMetGrowthMultiplier: 1);
            return new ContentCatalog(cargo, industries, townNeeds);
        }

        private static ContentCatalog CreateTownCatalog()
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
                demandPerNeedPerMonth: new TownNeedLevelAmounts(10, 10, 10),
                allNeedsMetGrowthMultiplier: 1,
                passengersPerPopulationUnit: 100,
                mailPerPopulationUnit: 250,
                passengerWaitingCapacity: 50);
            return new ContentCatalog(cargo, Array.Empty<IndustryDefinition>(), townNeeds);
        }

        /// <summary>
        /// Two stations next to one forest (A and B) and one next to a sawmill, linked sawmill to A and A to B.
        /// </summary>
        private static GeneratedWorld CreateForestWorld(
            ContentCatalog catalog,
            RouteMode mode,
            out Stop forestA,
            out Stop forestB,
            out Stop sawmill)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                5,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 58100UL, catalog.TownNeeds, catalog);
            TileCoordinate forestAnchor = FindIndustry(world, "forest").Coordinate;
            forestA = BuildStation(world, mode, forestAnchor);
            forestB = BuildStation(world, mode, forestAnchor);
            sawmill = BuildStation(world, mode, FindIndustry(world, "sawmill").Coordinate);
            Connect(world, mode, sawmill.Coordinate, forestA.Coordinate);
            Connect(world, mode, forestA.Coordinate, forestB.Coordinate);
            return world;
        }

        /// <summary>
        /// Two road stations next to town 0 (A1 and A2) and one next to town 1 (B), linked B to A1 and A1 to A2.
        /// </summary>
        private static GeneratedWorld CreateTwoTownWorld(
            ContentCatalog catalog,
            out Stop townA1,
            out Stop townA2,
            out Stop townB)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                5,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 59100UL, catalog.TownNeeds, catalog);
            Assert.That(world.Towns.Count, Is.GreaterThanOrEqualTo(2));
            townA1 = BuildStation(world, RouteMode.Road, world.TownAnchors[0]);
            townA2 = BuildStation(world, RouteMode.Road, world.TownAnchors[0]);
            townB = BuildStation(world, RouteMode.Road, world.TownAnchors[1]);
            Connect(world, RouteMode.Road, townB.Coordinate, townA1.Coordinate);
            Connect(world, RouteMode.Road, townA1.Coordinate, townA2.Coordinate);
            return world;
        }

        private static int BuyVehicle(
            GeneratedWorld world,
            ContentCatalog catalog,
            RouteMode mode,
            int firstStationId,
            int waitStationId)
        {
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int routeId = RouteTestSupport.CreateRoute(
                processor,
                world,
                mode,
                new[] { firstStationId, waitStationId },
                _waitAtSecondStop);
            string typeId = mode == RouteMode.Road ? catalog.StartingRoadVehicle.Id : catalog.StartingRailLocomotive.Id;
            return RouteTestSupport.BuyOnRoute(processor, world, routeId, catalog, typeId);
        }

        private static void Advance(GeneratedWorld world, RouteMode mode, ContentCatalog? catalog)
        {
            if (mode == RouteMode.Road)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, _january);
            }
            else
            {
                RailVehicleMovement.AdvanceAll(world, catalog, _january);
            }
        }

        private static void DriveUntil(GeneratedWorld world, RouteMode mode, ContentCatalog? catalog, Func<bool> done)
        {
            for (int tick = 0; tick < _setupTicks; tick++)
            {
                if (done())
                {
                    return;
                }

                Advance(world, mode, catalog);
            }

            Assert.That(done(), Is.True, "The vehicles did not reach the expected state.");
        }

        private static bool IsWaitingAt(GeneratedWorld world, RouteMode mode, int vehicleId, Stop stop)
        {
            int node = world.Map.GetTileId(stop.Coordinate);
            if (mode == RouteMode.Road)
            {
                RoadVehicle road = FindRoad(world, vehicleId);
                return road.IsServicingStop && road.IsUnloadComplete && road.EdgeFromNodeId == node;
            }

            RailVehicle rail = FindRail(world, vehicleId);
            return rail.IsServicingStop && rail.IsUnloadComplete && rail.EdgeFromNodeId == node;
        }

        private static bool IsServicing(GeneratedWorld world, RouteMode mode, int vehicleId)
        {
            return mode == RouteMode.Road
                ? FindRoad(world, vehicleId).IsServicingStop
                : FindRail(world, vehicleId).IsServicingStop;
        }

        private static int EdgeFromNode(GeneratedWorld world, RouteMode mode, int vehicleId)
        {
            return mode == RouteMode.Road
                ? FindRoad(world, vehicleId).EdgeFromNodeId
                : FindRail(world, vehicleId).EdgeFromNodeId;
        }

        private static CargoInventory CargoOf(GeneratedWorld world, RouteMode mode, int vehicleId)
        {
            return mode == RouteMode.Road ? FindRoad(world, vehicleId).Cargo : FindRail(world, vehicleId).Cargo;
        }

        private static int FreeCapacityOf(GeneratedWorld world, RouteMode mode, int vehicleId)
        {
            return mode == RouteMode.Road
                ? FindRoad(world, vehicleId).FreeCargoCapacity
                : FindRail(world, vehicleId).FreeCargoCapacity;
        }

        private static RoadVehicle FindRoad(GeneratedWorld world, int vehicleId)
        {
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                if (world.RoadVehicles[index].Id == vehicleId)
                {
                    return world.RoadVehicles[index];
                }
            }

            throw new InvalidOperationException("Road vehicle not found: " + vehicleId);
        }

        private static RailVehicle FindRail(GeneratedWorld world, int vehicleId)
        {
            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                if (world.RailVehicles[index].Id == vehicleId)
                {
                    return world.RailVehicles[index];
                }
            }

            throw new InvalidOperationException("Rail vehicle not found: " + vehicleId);
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

        private static bool IsFree(GeneratedWorld world, TileCoordinate tile)
        {
            return world.Map.GetObjectId(tile) == TileMap.NoObjectId &&
                   world.Map.GetRoadTypeId(tile) == TileMap.NoRoadTypeId &&
                   world.Map.GetRailTypeId(tile) == TileMap.NoRailTypeId &&
                   world.Map.GetStationId(tile) == TileMap.NoStationId &&
                   world.Map.GetRailStationId(tile) == TileMap.NoRailStationId;
        }

        private static bool HasStation(GeneratedWorld world, TileCoordinate tile)
        {
            return world.Map.GetStationId(tile) != TileMap.NoStationId ||
                   world.Map.GetRailStationId(tile) != TileMap.NoRailStationId;
        }

        private static readonly TileCoordinate[] _offsets =
        {
            new TileCoordinate(0, -1),
            new TileCoordinate(1, 0),
            new TileCoordinate(0, 1),
            new TileCoordinate(-1, 0)
        };

        private static Stop BuildStation(GeneratedWorld world, RouteMode mode, TileCoordinate anchor)
        {
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            for (int index = 0; index < _offsets.Length; index++)
            {
                var tile = new TileCoordinate(anchor.X + _offsets[index].X, anchor.Y + _offsets[index].Y);
                if (!world.Map.Contains(tile) || !IsFree(world, tile))
                {
                    continue;
                }

                if (mode == RouteMode.Road)
                {
                    RouteTestSupport.Accept(processor, new BuildRoadStationCommand(tile));
                    Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? road), Is.True);
                    return new Stop(road!.Id, tile);
                }

                RouteTestSupport.Accept(processor, new BuildRailStationCommand(tile));
                Assert.That(world.TryGetRailStationAt(tile, out RailStation? rail), Is.True);
                return new Stop(rail!.Id, tile);
            }

            throw new InvalidOperationException("No free orthogonal neighbor for a station.");
        }

        /// <summary>
        /// Lays road or rail along the shortest free path between two stations (breadth-first search).
        /// </summary>
        private static void Connect(GeneratedWorld world, RouteMode mode, TileCoordinate from, TileCoordinate to)
        {
            var cameFrom = new Dictionary<int, int> { [world.Map.GetTileId(from)] = -1 };
            var queue = new Queue<TileCoordinate>();
            queue.Enqueue(from);
            bool found = false;
            while (queue.Count > 0 && !found)
            {
                TileCoordinate current = queue.Dequeue();
                for (int index = 0; index < _offsets.Length; index++)
                {
                    var next = new TileCoordinate(current.X + _offsets[index].X, current.Y + _offsets[index].Y);
                    if (!world.Map.Contains(next) || cameFrom.ContainsKey(world.Map.GetTileId(next)))
                    {
                        continue;
                    }

                    bool isTarget = next.Equals(to);
                    if (!isTarget && (world.Map.GetObjectId(next) != TileMap.NoObjectId || HasStation(world, next)))
                    {
                        continue;
                    }

                    cameFrom[world.Map.GetTileId(next)] = world.Map.GetTileId(current);
                    if (isTarget)
                    {
                        found = true;
                        break;
                    }

                    queue.Enqueue(next);
                }
            }

            Assert.That(found, Is.True, "No free path between stations.");
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            int cursor = cameFrom[world.Map.GetTileId(to)];
            while (cursor != world.Map.GetTileId(from))
            {
                TileCoordinate step = world.Map.GetCoordinate(cursor);
                if (mode == RouteMode.Road && world.Map.GetRoadTypeId(step) == TileMap.NoRoadTypeId)
                {
                    RouteTestSupport.Accept(processor, new BuildRoadCommand(step));
                }
                else if (mode == RouteMode.Rail && world.Map.GetRailTypeId(step) == TileMap.NoRailTypeId)
                {
                    RouteTestSupport.Accept(processor, new BuildRailCommand(step));
                }

                cursor = cameFrom[cursor];
            }
        }
    }
}
