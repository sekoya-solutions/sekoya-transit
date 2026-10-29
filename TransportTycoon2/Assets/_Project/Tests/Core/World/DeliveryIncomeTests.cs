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
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };

        [Test]
        public void DeliveryAcceptance_ConsumerIndustry_AcceptsInputCargoOnly()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillStationWorld(out RoadStation station);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "wood", catalog), Is.True);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "planks", catalog), Is.False);
        }

        [Test]
        public void DeliveryAcceptance_TownAdjacentStation_AcceptsOnlyNeededCargo()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateTownStationWorld(catalog, out RoadStation station);
            Assert.That(world.Towns[0].HasNeed("wood"), Is.True);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "wood", catalog), Is.True);
            Assert.That(DeliveryAcceptance.Accepts(world, station, "planks", catalog), Is.False);
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
            PlacedIndustry forest = FindIndustry(world, "forest");
            forest.TryAddCargo("wood", 4);
            int sawmillIndex = IndexOfIndustry(world, FindIndustry(world, "sawmill"));
            Contract woodToSawmill = OfferContract(world, "wood", DeliveryDestinationKind.Industry, sawmillIndex);
            Contract planksToSawmill = OfferContract(world, "planks", DeliveryDestinationKind.Industry, sawmillIndex);
            Contract woodToTown = OfferContract(world, "wood", DeliveryDestinationKind.Town, 0);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            long cashBefore = world.CompanyCash;
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(consumer.Id, waitForFull: false),
                            new RoadVehicleOrder(pickup.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            bool delivered = false;
            for (int tick = 0; tick < 8000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
                if (world.CompanyCash - cashBefore >= 8L * 4)
                {
                    delivered = true;
                    break;
                }
            }

            Assert.That(delivered, Is.True);
            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(8L * 4));
            Assert.That(world.Finance.Current.Get(FinanceCategory.DeliveryIncome), Is.EqualTo(8L * 4));
            Assert.That(vehicle.LifetimeDeliveryIncome, Is.EqualTo(8L * 4));
            Assert.That(vehicle.Id, Is.EqualTo(0));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Industry), Is.EqualTo(4L));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Town), Is.Zero);
            Assert.That(woodToSawmill.Delivered, Is.EqualTo(4L));
            Assert.That(planksToSawmill.Delivered, Is.Zero);
            Assert.That(woodToTown.Delivered, Is.Zero);
        }

        [Test]
        public void Unload_WithDistancePay_PaysByTilesFromThePickupStation()
        {
            var pay = new DeliveryPaySettings(30, 4);
            ContentCatalog catalog = CreateWoodChainCatalog(new EconomySettings(100000L, pay));
            GeneratedWorld world = CreateConnectedPickupAndConsumerWorld(
                out RoadStation pickup,
                out RoadStation consumer);
            FindIndustry(world, "forest").TryAddCargo("wood", 4);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[] { new RoadVehicleOrder(consumer.Id), new RoadVehicleOrder(pickup.Id) },
                        catalog)).IsAccepted,
                Is.True);
            long cashBefore = world.CompanyCash;
            int distance = DeliveryPay.ResolveDistance(pickup.Coordinate, consumer.Coordinate, pay);
            long expected = 8L * 4L * distance / 30L;

            for (int tick = 0; tick < 8000 && world.GetCargoDelivered("wood", DeliveryDestinationKind.Industry) < 4L; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
            }

            RoadVehicle vehicle = world.RoadVehicles[0];
            Assert.That(distance, Is.GreaterThan(4));
            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(expected));
            Assert.That(vehicle.LifetimeDeliveryIncome, Is.EqualTo(expected));
            Assert.That(vehicle.DeliveryPayRemainder, Is.EqualTo((int)(8L * 4L * distance % 30L)));
            Assert.That(vehicle.CargoOriginTile, Is.EqualTo(pickup.Coordinate));
        }

        [Test]
        public void Unload_AtConsumerIndustryWithFullStock_AcceptsPaysAndLosesOutput()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedPickupAndConsumerWorld(
                out RoadStation pickup,
                out RoadStation consumer);
            FindIndustry(world, "forest").TryAddCargo("wood", 4);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill");
            sawmill.TryAddCargo("planks", PlacedIndustry.PlaceholderCargoCapacity);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            long cashBefore = world.CompanyCash;
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(consumer.Id, waitForFull: false),
                            new RoadVehicleOrder(pickup.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            for (int tick = 0; tick < 8000 && world.CompanyCash - cashBefore < 8L * 4; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
            }

            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(8L * 4));
            Assert.That(vehicle.Cargo.GetAmount("wood"), Is.Zero);
            Assert.That(sawmill.Cargo.GetAmount("wood"), Is.Zero);
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(PlacedIndustry.PlaceholderCargoCapacity));
            Assert.That(sawmill.MonthProduced, Is.EqualTo(2L));
        }

        [Test]
        public void Unload_AtTownStation_ConsumesNeededFreightAndCreditsSupply()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedPickupAndTownWorld(
                catalog,
                out RoadStation pickup,
                out RoadStation townStation);
            PlacedIndustry forest = FindIndustry(world, "forest");
            forest.TryAddCargo("wood", 4);
            Assert.That(world.Towns[0].HasNeed("wood"), Is.True);
            Contract woodToTown = OfferContract(world, "wood", DeliveryDestinationKind.Town, 0);
            Contract woodToIndustry = OfferContract(world, "wood", DeliveryDestinationKind.Industry, 0);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            long cashBefore = world.CompanyCash;
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(townStation.Id, waitForFull: false),
                            new RoadVehicleOrder(pickup.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            int townNode = world.Map.GetTileId(townStation.Coordinate);
            bool leftTownEmpty = false;
            var date = new GameDate(1900, 1, 1);
            for (int tick = 0; tick < 8000; tick++)
            {
                bool wasServicingTown = vehicle.IsServicingStop && vehicle.EdgeFromNodeId == townNode;
                RoadVehicleMovement.AdvanceAll(world, catalog, date);
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
            Assert.That(world.Towns[0].Needs[0].MonthSupply, Is.EqualTo(4));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Town), Is.EqualTo(4L));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Industry), Is.Zero);
            Assert.That(woodToTown.Delivered, Is.EqualTo(4L));
            Assert.That(woodToIndustry.Delivered, Is.Zero);
        }

        [Test]
        public void Unload_AtProducerOnlyStation_KeepsCargoAndDoesNotCreditCash()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedTwoForestStations(
                catalog,
                out RoadStation a,
                out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            long cashBefore = world.CompanyCash;
            RoadVehicle vehicle = world.RoadVehicles[0];
            vehicle.Cargo.Add("wood", 4);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(b.Id, waitForFull: false),
                            new RoadVehicleOrder(a.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);

            PlacedIndustry destinationForest = world.Industries[1];
            Contract rejected = OfferContract(world, "wood", DeliveryDestinationKind.Industry, 1);
            int woodBefore = destinationForest.Cargo.GetAmount("wood");
            bool visitedDestination = false;
            for (int tick = 0; tick < 8000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
                visitedDestination |= vehicle.IsServicingStop && vehicle.OrderIndex == 0;
            }

            // Wood is not a forest input (Issue #193): it is neither stocked nor paid and stays on the vehicle.
            Assert.That(visitedDestination, Is.True);
            Assert.That(destinationForest.Cargo.GetAmount("wood"), Is.EqualTo(woodBefore));
            Assert.That(vehicle.Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Any), Is.Zero);
            Assert.That(rejected.Delivered, Is.Zero);
        }

        [Test]
        public void WorldSimulationStep_PaidDeliveries_PublishesOneIncomeEventPerPayingTick()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedPickupAndConsumerWorld(
                out RoadStation pickup,
                out RoadStation consumer);
            FindIndustry(world, "forest").TryAddCargo("wood", 4);
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
                        },
                        catalog)).IsAccepted,
                Is.True);

            var calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
            var events = new EventBus();
            var received = new System.Collections.Generic.List<DeliveryIncomeEvent>();
            events.Subscribe<DeliveryIncomeEvent>(received.Add);
            var tick = new Tick(2);
            int maxEventsInOneTick = 0;
            for (int step = 0; step < 8000 && world.Finance.Current.Get(FinanceCategory.DeliveryIncome) < 8L * 4; step++)
            {
                tick = tick.Next();
                int before = received.Count;
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                Assert.That(events.DispatchPending().Failures, Is.Empty);
                int published = received.Count - before;
                maxEventsInOneTick = Math.Max(maxEventsInOneTick, published);
                if (published == 1)
                {
                    Assert.That(received[received.Count - 1].Date, Is.EqualTo(calendar.GetDate(tick)));
                }
            }

            long totalIncome = 0;
            foreach (DeliveryIncomeEvent paid in received)
            {
                Assert.That(paid.Income, Is.GreaterThan(0L));
                totalIncome += paid.Income;
            }

            Assert.That(received, Is.Not.Empty);
            Assert.That(maxEventsInOneTick, Is.EqualTo(1));
            Assert.That(totalIncome, Is.EqualTo(world.Finance.Current.Get(FinanceCategory.DeliveryIncome)));
            Assert.That(totalIncome, Is.EqualTo(8L * 4));
        }

        [Test]
        public void WorldSimulationStep_RouteSetWhileStandingAtPickup_LoadsBeforeFirstDeparture()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedPickupAndConsumerWorld(
                out RoadStation pickup,
                out RoadStation consumer);
            FindIndustry(world, "forest").TryAddCargo("wood", 4);
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
                        },
                        catalog)).IsAccepted,
                Is.True);

            var calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
            var events = new EventBus();
            RoadVehicle truck = world.RoadVehicles[0];
            Assert.That(truck.OrderIndex, Is.EqualTo(1), "The route starts at the pickup station the truck stands on.");
            var tick = new Tick(2);
            for (int step = 0; step < 200 && truck.OrderIndex == 1; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                Assert.That(events.DispatchPending().Failures, Is.Empty);
            }

            Assert.That(truck.OrderIndex, Is.EqualTo(0), "The truck should leave the pickup station.");
            Assert.That(truck.Cargo.GetAmount("wood"), Is.EqualTo(4));
        }

        [Test]
        public void WorldSimulationStep_UnpaidUnload_PublishesNoIncomeEvent()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateConnectedTwoForestStations(
                catalog,
                out RoadStation a,
                out RoadStation b);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            world.RoadVehicles[0].Cargo.Add("wood", 4);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(b.Id, waitForFull: false),
                            new RoadVehicleOrder(a.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);

            var calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
            var events = new EventBus();
            int received = 0;
            events.Subscribe<DeliveryIncomeEvent>(_ => received++);
            PlacedIndustry destinationForest = world.Industries[1];
            int woodBefore = destinationForest.Cargo.GetAmount("wood");
            var tick = new Tick(2);
            for (int step = 0; step < 8000 && destinationForest.Cargo.GetAmount("wood") <= woodBefore; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                Assert.That(events.DispatchPending().Failures, Is.Empty);
            }

            Assert.That(destinationForest.Cargo.GetAmount("wood"), Is.GreaterThan(woodBefore));
            Assert.That(received, Is.Zero);
        }

        private static ContentCatalog CreateWoodChainCatalog(EconomySettings? economy = null)
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
                demandPerNeedPerMonth: 10,
                populationPerSatisfiedNeed: 20);
            return new ContentCatalog(cargo, industries, townNeeds, economy);
        }

        private static GeneratedWorld CreateForestStationWorld(out RoadStation station)
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = GenerateForestWorld(48011UL, catalog);
            station = BuildAdjacentStation(world, world.Industries[0].Coordinate);
            return world;
        }

        private static GeneratedWorld CreateSawmillStationWorld(out RoadStation station)
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = GenerateForestAndSawmillWorld(48012UL, catalog);
            station = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            return world;
        }

        private static GeneratedWorld CreateTownStationWorld(ContentCatalog catalog, out RoadStation station)
        {
            GeneratedWorld world = GenerateForestWorld(48013UL, catalog);
            station = BuildAdjacentStation(world, world.TownAnchors[0]);
            return world;
        }

        private static GeneratedWorld CreateConnectedPickupAndConsumerWorld(
            out RoadStation pickup,
            out RoadStation consumer)
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = GenerateForestAndSawmillWorld(48014UL, catalog);
            pickup = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate);
            consumer = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, pickup.Coordinate, consumer.Coordinate, 10);
            return world;
        }

        private static GeneratedWorld CreateConnectedPickupAndTownWorld(
            ContentCatalog catalog,
            out RoadStation pickup,
            out RoadStation townStation)
        {
            GeneratedWorld world = GenerateForestWorld(48016UL, catalog);
            pickup = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate);
            townStation = BuildAdjacentStation(world, world.TownAnchors[0]);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, pickup.Coordinate, townStation.Coordinate, 10);
            return world;
        }

        private static GeneratedWorld CreateConnectedTwoForestStations(
            ContentCatalog catalog,
            out RoadStation a,
            out RoadStation b)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 2, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 48015UL, catalog.TownNeeds, catalog);
            Assert.That(world.Industries.Count, Is.GreaterThanOrEqualTo(2));
            a = BuildAdjacentStation(world, world.Industries[0].Coordinate);
            b = BuildAdjacentStation(world, world.Industries[1].Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, a.Coordinate, b.Coordinate, 10);
            return world;
        }

        private static GeneratedWorld GenerateForestWorld(ulong seed, ContentCatalog catalog)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }

        private static GeneratedWorld GenerateForestAndSawmillWorld(ulong seed, ContentCatalog catalog)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }

        private static Contract OfferContract(
            GeneratedWorld world,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex)
        {
            return world.OfferContract(
                cargoId,
                targetKind,
                targetIndex,
                1000,
                new GameDate(1900, 1, 1),
                new GameDate(1901, 1, 1));
        }

        private static int IndexOfIndustry(GeneratedWorld world, PlacedIndustry industry)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (ReferenceEquals(world.Industries[index], industry))
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Industry is not part of the world.");
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
