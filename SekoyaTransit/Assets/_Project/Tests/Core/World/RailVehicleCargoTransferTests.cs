#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class RailVehicleCargoTransferTests
    {
        private static readonly bool[] _waitAtSecondStop = { false, true };
        private static readonly string[] _forestOutputCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly GameDate _january = new GameDate(1900, 1, 1);

        [Test]
        public void Advance_OnArrival_UnloadsThenLoadsFromIndustryAtTransferRate()
        {
            GeneratedWorld world = CreateTownAndForestStations(
                out RailStation townStation,
                out RailStation forestStation,
                out PlacedIndustry forest);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            // The train starts at the route's first stop, so the town stop comes first.
            int vehicleId = RouteTestSupport.PlaceRailVehicle(
                processor,
                world,
                new[]
                {
                    new RailVehicleOrder(townStation.Id, waitForFull: false),
                    new RailVehicleOrder(forestStation.Id, waitForFull: false)
                });
            AttachFullConsist(processor, vehicleId, startingTick: 2);

            RailVehicle vehicle = world.RailVehicles[0];
            forest.Cargo.Clear();
            forest.TryAddCargo("wood", 8);

            int nodeForest = world.Map.GetTileId(forestStation.Coordinate);
            bool leftForestWithCargo = false;
            for (int tick = 0; tick < 4000; tick++)
            {
                bool wasServicingForest = vehicle.IsServicingStop && vehicle.EdgeFromNodeId == nodeForest;
                RailVehicleMovement.AdvanceAll(world, calendarDate: _january);
                if (wasServicingForest && !vehicle.IsServicingStop && vehicle.Cargo.TotalAmount > 0)
                {
                    leftForestWithCargo = true;
                    break;
                }
            }

            Assert.That(leftForestWithCargo, Is.True);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(8));
            Assert.That(vehicle.Cargo.Stacks[0].CargoId, Is.EqualTo("wood"));
            Assert.That(forest.Cargo.TotalAmount, Is.EqualTo(0));
            Assert.That(forest.MonthTransported, Is.EqualTo(8L));
        }

        [Test]
        public void Advance_WaitForFull_StaysUntilFullWhenCargoArrives()
        {
            GeneratedWorld world = CreateTownAndForestStations(
                out RailStation townStation,
                out RailStation forestStation,
                out PlacedIndustry forest);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            // The train starts at the route's first stop, so the town stop comes first.
            int vehicleId = RouteTestSupport.PlaceRailVehicle(
                processor,
                world,
                new[]
                {
                    new RailVehicleOrder(townStation.Id, waitForFull: false),
                    new RailVehicleOrder(forestStation.Id, waitForFull: true)
                });
            AttachFullConsist(processor, vehicleId, startingTick: 2);

            RailVehicle vehicle = world.RailVehicles[0];
            forest.TryAddCargo("wood", 4);

            bool servicingAtForest = false;
            for (int tick = 0; tick < 4000; tick++)
            {
                RailVehicleMovement.AdvanceAll(world, calendarDate: _january);
                if (vehicle.IsServicingStop &&
                    vehicle.EdgeFromNodeId == world.Map.GetTileId(forestStation.Coordinate))
                {
                    servicingAtForest = true;
                    if (vehicle.IsUnloadComplete && vehicle.Cargo.TotalAmount == 4)
                    {
                        break;
                    }
                }
            }

            Assert.That(servicingAtForest, Is.True);
            Assert.That(vehicle.IsServicingStop, Is.True);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(4));

            forest.TryAddCargo("wood", RailVehicle.PlaceholderCargoCapacity);
            bool left = false;
            for (int tick = 0; tick < 200; tick++)
            {
                RailVehicleMovement.AdvanceAll(world, calendarDate: _january);
                if (!vehicle.IsServicingStop && vehicle.Cargo.TotalAmount == RailVehicle.PlaceholderCargoCapacity)
                {
                    left = true;
                    break;
                }
            }

            Assert.That(left, Is.True);
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(RailVehicle.PlaceholderCargoCapacity));
        }

        [Test]
        public void Advance_WithoutWaitForFull_LeavesPartiallyLoaded()
        {
            GeneratedWorld world = CreateTownAndForestStations(
                out RailStation townStation,
                out RailStation forestStation,
                out PlacedIndustry forest);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            // The train starts at the route's first stop, so the town stop comes first.
            int vehicleId = RouteTestSupport.PlaceRailVehicle(
                processor,
                world,
                new[]
                {
                    new RailVehicleOrder(townStation.Id, waitForFull: false),
                    new RailVehicleOrder(forestStation.Id, waitForFull: false)
                });
            AttachFullConsist(processor, vehicleId, startingTick: 2);

            RailVehicle vehicle = world.RailVehicles[0];
            forest.TryAddCargo("wood", 3);

            bool leftWithPartial = false;
            for (int tick = 0; tick < 4000; tick++)
            {
                RailVehicleMovement.AdvanceAll(world, calendarDate: _january);
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
        public void DistributeLoads_RoundRobin_AlternatesOneUnitPerTickBetweenVehicles()
        {
            GeneratedWorld world = CreateTownAndForestStations(
                out RailStation townStation,
                out RailStation forestStation,
                out PlacedIndustry forest);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            int routeId = RouteTestSupport.CreateRoute(
                processor,
                world,
                RouteMode.Rail,
                new[] { townStation.Id, forestStation.Id },
                _waitAtSecondStop);
            int firstId = RouteTestSupport.BuyOnRoute(processor, world, routeId, catalog, catalog.StartingRailLocomotive.Id);
            int secondId = RouteTestSupport.BuyOnRoute(processor, world, routeId, catalog, catalog.StartingRailLocomotive.Id);
            AttachFullConsist(processor, firstId, startingTick: 3);
            AttachFullConsist(processor, secondId, startingTick: 6);

            RailVehicle vehicleA = world.RailVehicles[0];
            RailVehicle vehicleB = world.RailVehicles[1];
            Assert.That(vehicleA.Id, Is.LessThan(vehicleB.Id));

            forest.Cargo.Clear();
            // Keep industry empty until both vehicles are waiting to load.
            int forestNode = world.Map.GetTileId(forestStation.Coordinate);
            bool bothWaiting = false;
            for (int tick = 0; tick < 8000; tick++)
            {
                RailVehicleMovement.AdvanceAll(world, calendarDate: _january);
                if (vehicleA.IsServicingStop &&
                    vehicleB.IsServicingStop &&
                    vehicleA.IsUnloadComplete &&
                    vehicleB.IsUnloadComplete &&
                    vehicleA.EdgeFromNodeId == forestNode &&
                    vehicleB.EdgeFromNodeId == forestNode)
                {
                    bothWaiting = true;
                    break;
                }
            }

            Assert.That(bothWaiting, Is.True);
            forest.TryAddCargo("wood", 5);

            RailVehicleCargoTransfer.DistributeIndustryLoads(world, catalog: null, _january);
            Assert.That(vehicleA.Cargo.TotalAmount, Is.EqualTo(1));
            Assert.That(vehicleB.Cargo.TotalAmount, Is.EqualTo(0));

            RailVehicleCargoTransfer.DistributeIndustryLoads(world, catalog: null, _january);
            Assert.That(vehicleA.Cargo.TotalAmount, Is.EqualTo(1));
            Assert.That(vehicleB.Cargo.TotalAmount, Is.EqualTo(1));

            RailVehicleCargoTransfer.DistributeIndustryLoads(world, catalog: null, _january);
            Assert.That(vehicleA.Cargo.TotalAmount, Is.EqualTo(2));
            Assert.That(vehicleB.Cargo.TotalAmount, Is.EqualTo(1));
            Assert.That(forest.MonthTransported, Is.EqualTo(3L));
        }

        [Test]
        public void Capacity_RejectsCargoAboveLimit_ViaTransfer()
        {
            var inventory = new CargoInventory();
            inventory.Add("wood", RailVehicle.PlaceholderCargoCapacity);
            var destination = new CargoInventory();
            int moved = inventory.TransferTo(destination, 5, destinationFreeCapacity: 0);
            Assert.That(moved, Is.EqualTo(0));
            Assert.That(inventory.TotalAmount, Is.EqualTo(RailVehicle.PlaceholderCargoCapacity));
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

            source1.TransferTo(dest1, RailVehicleCargoTransfer.UnitsPerTick, 10);
            source2.TransferTo(dest2, RailVehicleCargoTransfer.UnitsPerTick, 10);

            Assert.That(dest1.TotalAmount, Is.EqualTo(dest2.TotalAmount));
            Assert.That(dest1.Stacks[0].CargoId, Is.EqualTo(dest2.Stacks[0].CargoId));
            Assert.That(dest1.Stacks[0].Amount, Is.EqualTo(dest2.Stacks[0].Amount));
        }

        private static void AttachFullConsist(
            SimulationCommandProcessor<GeneratedWorld> processor,
            int vehicleId,
            int startingTick)
        {
            int tick = startingTick;
            // A train bought for a route already has one wagon.
            for (int wagon = 1; wagon < RailVehicle.LegacyMaxWagons; wagon++)
            {
                Assert.That(
                    processor.Process(new Tick(tick++), new AddRailWagonCommand(vehicleId)).IsAccepted,
                    Is.True);
            }
        }

        [Test]
        public void Delivery_ToConsumerIndustryStation_CountsAgainstIndustryDestination()
        {
            var cargo = new[] { new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1) };
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
            var catalog = new ContentCatalog(cargo, industries);
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _forestOutputCargoIds, _planksCargoIds, 1, 80)
            };
            GeneratedWorld world = WorldGenerator.Generate(
                new WorldGenerationSettings(new TownPlacementSettings(MapSize.Small, 1, 8), 4, rules),
                48020UL);
            PlacedIndustry forest = FindIndustryById(world, "forest");
            PlacedIndustry sawmill = FindIndustryById(world, "sawmill");
            TileCoordinate pickupTile = FindOrthogonalEmptyNeighbor(world, forest.Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(pickupTile)).IsAccepted, Is.True);
            TileCoordinate consumerTile = FindOrthogonalEmptyNeighbor(world, sawmill.Coordinate);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(consumerTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(pickupTile, out RailStation? pickup), Is.True);
            Assert.That(world.TryGetRailStationAt(consumerTile, out RailStation? consumer), Is.True);
            ConnectStationsWithRoads(world, processor, pickupTile, consumerTile, startingTick: 3);
            RouteTestSupport.PlaceRailVehicle(processor, world, catalog, pickup!.Id, consumer!.Id);
            forest.Cargo.Clear();
            forest.TryAddCargo("wood", 4);
            Contract woodToTown = world.OfferContract("wood", 0, 1000, _january);
            Contracts.Accept(world, catalog, woodToTown.Id, _january);

            for (int tick = 0; tick < 8000 && world.GetCargoDelivered("wood", DeliveryDestinationKind.Any) < 4L; tick++)
            {
                RailVehicleMovement.AdvanceAll(world, catalog, _january);
            }

            Assert.That(woodToTown.Delivered, Is.Zero);
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Industry), Is.EqualTo(4L));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Town), Is.Zero);
        }

        [Test]
        public void Delivery_WithDistancePay_PaysByTilesFromThePickupStation()
        {
            var cargo = new[] { new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1) };
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
            var pay = new DeliveryPaySettings(30, 4);
            // A locomotive bigger than the legacy 3 x 10 consist, so the save checks below see its real size.
            var vehicles = new System.Collections.Generic.List<VehicleDefinition>();
            foreach (VehicleDefinition vehicle in ContentCatalog.CreatePlaceholderVehicles())
            {
                vehicles.Add(
                    vehicle.Mode == VehicleMode.Rail
                        ? new VehicleDefinition(
                            vehicle.Id,
                            vehicle.NameKey,
                            VehicleMode.Rail,
                            vehicle.Purchase,
                            vehicle.UpkeepPerMonth,
                            vehicle.AvailableYear,
                            0L,
                            true,
                            maxWagons: 5,
                            capacityPerWagon: 14,
                            speedThousandths: 250)
                        : vehicle);
            }

            var catalog = new ContentCatalog(cargo, industries, null, new EconomySettings(100000L, pay), vehicles);
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _forestOutputCargoIds, _planksCargoIds, 1, 80)
            };
            GeneratedWorld world = WorldGenerator.Generate(
                new WorldGenerationSettings(new TownPlacementSettings(MapSize.Small, 1, 8), 4, rules),
                48020UL);
            PlacedIndustry forest = FindIndustryById(world, "forest");
            TileCoordinate pickupTile = FindOrthogonalEmptyNeighbor(world, forest.Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(pickupTile)).IsAccepted, Is.True);
            TileCoordinate consumerTile = FindOrthogonalEmptyNeighbor(world, FindIndustryById(world, "sawmill").Coordinate);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(consumerTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(pickupTile, out RailStation? pickup), Is.True);
            Assert.That(world.TryGetRailStationAt(consumerTile, out RailStation? consumer), Is.True);
            ConnectStationsWithRoads(world, processor, pickupTile, consumerTile, startingTick: 3);
            RouteTestSupport.PlaceRailVehicle(processor, world, catalog, pickup!.Id, consumer!.Id);
            forest.Cargo.Clear();
            forest.TryAddCargo("wood", 4);
            long cashBefore = world.CompanyCash;
            int distance = DeliveryPay.ResolveDistance(pickupTile, consumerTile, pay);

            for (int tick = 0; tick < 8000 && world.GetCargoDelivered("wood", DeliveryDestinationKind.Any) < 4L; tick++)
            {
                RailVehicleMovement.AdvanceAll(world, catalog, _january);
            }

            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(8L * 4L * distance / 30L));
            Assert.That(world.RailVehicles[0].CargoOriginTile, Is.EqualTo(pickupTile));
            Assert.That(world.RailVehicles[0].DeliveryPayRemainder, Is.EqualTo((int)(8L * 4L * distance % 30L)));

            // Save keeps the load origin, the pay fraction and the consist size (the snapshot used to drop the size).
            RailVehicle saved = SaveSnapshot.Capture(world.MapSeed, new Tick(9000), world).RailVehicles[0];
            Assert.That(saved.CargoOriginTile, Is.EqualTo(pickupTile));
            Assert.That(saved.DeliveryPayRemainder, Is.EqualTo(world.RailVehicles[0].DeliveryPayRemainder));
            Assert.That(saved.MaxWagons, Is.EqualTo(5));
            Assert.That(saved.CapacityPerWagon, Is.EqualTo(14));
            Assert.That(saved.Copy().MaxWagons, Is.EqualTo(5));
            Assert.That(saved.Copy().CapacityPerWagon, Is.EqualTo(14));
        }

        private static PlacedIndustry FindIndustryById(GeneratedWorld world, string industryId)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (world.Industries[index].IndustryId == industryId)
                {
                    return world.Industries[index];
                }
            }

            throw new InvalidOperationException("Industry not found: " + industryId);
        }

        private static GeneratedWorld CreateTownAndForestStations(
            out RailStation townStation,
            out RailStation forestStation,
            out PlacedIndustry forest)
        {
            GeneratedWorld world = CreateWorld();
            forest = world.Industries[0];
            TileCoordinate townAnchor = world.TownAnchors[0];
            TileCoordinate townStationCoord = FindOrthogonalEmptyNeighbor(world, townAnchor);
            TileCoordinate forestStationCoord = FindOrthogonalEmptyNeighbor(world, forest.Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(new Tick(1), new BuildRailStationCommand(townStationCoord)).IsAccepted,
                Is.True);
            Assert.That(
                processor.Process(new Tick(2), new BuildRailStationCommand(forestStationCoord)).IsAccepted,
                Is.True);
            Assert.That(world.TryGetRailStationAt(townStationCoord, out RailStation? foundTown), Is.True);
            Assert.That(world.TryGetRailStationAt(forestStationCoord, out RailStation? foundForest), Is.True);
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
                         world.Map.GetRailStationId(next) != TileMap.NoRailStationId))
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
