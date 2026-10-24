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
    public sealed class VehicleRouteCargoFilterTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly string[] _expectedWoodOnlyLoads = { "wood" };

        [Test]
        public void SetOrders_WithCatalog_RebuildsAllowedLoadListFromRoute()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateForestAndSawmillWorld(catalog, out RoadStation forestStation, out RoadStation sawmillStation);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(forestStation.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[] { forestStation.Id, sawmillStation.Id },
                        catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            Assert.That(vehicle.AllowedLoadCargoDirty, Is.False);
            Assert.That(vehicle.AllowedLoadCargoIds, Is.EqualTo(_expectedWoodOnlyLoads));
            Assert.That(vehicle.AllowsLoadCargo("wood"), Is.True);
            Assert.That(vehicle.AllowsLoadCargo("planks"), Is.False);
        }

        [Test]
        public void IndustryLoading_AtSawmill_DoesNotLoadOwnInputWhenOnlyOtherStopIsIronMine()
        {
            ContentCatalog catalog = CreateCatalogWithIronMine();
            GeneratedWorld world = CreateSawmillAndIronMineWorld(
                catalog,
                out RoadStation sawmillStation,
                out RoadStation ironMineStation);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill");
            Assert.That(sawmill.TryAddCargo("wood", 4), Is.EqualTo(4));
            Assert.That(sawmill.TryAddCargo("planks", 3), Is.EqualTo(3));

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(sawmillStation.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[] { sawmillStation.Id, ironMineStation.Id },
                        catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            Assert.That(
                VehicleRouteCargoFilter.IsFreightAcceptedAtOtherRoadStop(
                    world,
                    vehicle,
                    catalog,
                    sawmillStation.Id,
                    "wood"),
                Is.False);
            Assert.That(
                VehicleRouteCargoFilter.IsFreightAcceptedAtOtherRoadStop(
                    world,
                    vehicle,
                    catalog,
                    sawmillStation.Id,
                    "planks"),
                Is.False);

            for (int tick = 0; tick < 2000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
                if (!vehicle.Cargo.IsEmpty)
                {
                    Assert.Fail("Vehicle loaded cargo with no other-stop acceptor on the route.");
                }
            }

            Assert.That(vehicle.Cargo.IsEmpty, Is.True);
            Assert.That(sawmill.Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(3));
        }

        [Test]
        public void IndustryLoading_SkipsCargoNotAcceptedOnRoute()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateTwoForestWorld(catalog, out RoadStation a, out RoadStation b);
            world.Industries[0].TryAddCargo("wood", 4);

            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(a.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(0, new[] { a.Id, b.Id }, catalog)).IsAccepted,
                Is.True);

            RoadVehicle vehicle = world.RoadVehicles[0];
            Assert.That(vehicle.AllowedLoadCargoIds, Is.Empty);

            for (int tick = 0; tick < 2000; tick++)
            {
                RoadVehicleMovement.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
                if (!vehicle.Cargo.IsEmpty)
                {
                    Assert.Fail("Vehicle loaded cargo that no route stop accepts.");
                }
            }

            Assert.That(vehicle.Cargo.IsEmpty, Is.True);
            Assert.That(world.Industries[0].Cargo.GetAmount("wood"), Is.EqualTo(4));
        }

        private static ContentCatalog CreateCatalog()
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
                10,
                20);
            return new ContentCatalog(cargo, industries, townNeeds);
        }

        private static ContentCatalog CreateCatalogWithIronMine()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2),
                new CargoDefinition("iron_ore", "cargo.iron_ore", CargoClass.Cargo, 7, 1)
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
                    new[] { new CargoAmountDefinition("planks", 1) }),
                new IndustryDefinition(
                    "iron_mine",
                    "industry.iron_mine",
                    1,
                    Array.Empty<CargoAmountDefinition>(),
                    new[] { new CargoAmountDefinition("iron_ore", 4) })
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                10,
                20);
            return new ContentCatalog(cargo, industries, townNeeds);
        }

        private static GeneratedWorld CreateSawmillAndIronMineWorld(
            ContentCatalog catalog,
            out RoadStation sawmillStation,
            out RoadStation ironMineStation)
        {
            var ironOreCargoIds = new[] { "iron_ore" };
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80),
                new IndustryPlacementRule("iron_mine", 1, _emptyCargoIds, ironOreCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 58102UL, catalog.TownNeeds, catalog);
            sawmillStation = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            ironMineStation = BuildAdjacentStation(world, FindIndustry(world, "iron_mine").Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, sawmillStation.Coordinate, ironMineStation.Coordinate, 20);
            return world;
        }

        private static GeneratedWorld CreateForestAndSawmillWorld(
            ContentCatalog catalog,
            out RoadStation forestStation,
            out RoadStation sawmillStation)
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
            GeneratedWorld world = WorldGenerator.Generate(settings, 58100UL, catalog.TownNeeds, catalog);
            forestStation = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate);
            sawmillStation = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, forestStation.Coordinate, sawmillStation.Coordinate, 20);
            return world;
        }

        private static GeneratedWorld CreateTwoForestWorld(
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
            GeneratedWorld world = WorldGenerator.Generate(settings, 58101UL, catalog.TownNeeds, catalog);
            a = BuildAdjacentStation(world, world.Industries[0].Coordinate);
            b = BuildAdjacentStation(world, world.Industries[1].Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, a.Coordinate, b.Coordinate, 20);
            return world;
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
                var neighbor = new TileCoordinate(coordinate.X + offsets[index].X, coordinate.Y + offsets[index].Y);
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                if (world.Map.GetObjectId(neighbor) != TileMap.NoObjectId)
                {
                    continue;
                }

                if (world.TryGetRoadStationAt(neighbor, out _))
                {
                    continue;
                }

                return neighbor;
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }

        private static void ConnectStationsWithRoads(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            TileCoordinate from,
            TileCoordinate to,
            int startTick)
        {
            int tick = startTick;
            int x = from.X;
            int y = from.Y;
            while (x != to.X)
            {
                x += x < to.X ? 1 : -1;
                var tile = new TileCoordinate(x, y);
                if (!world.TryGetRoadStationAt(tile, out _) && world.Map.GetObjectId(tile) == TileMap.NoObjectId)
                {
                    Assert.That(processor.Process(new Tick(tick++), new BuildRoadCommand(tile)).IsAccepted, Is.True);
                }
            }

            while (y != to.Y)
            {
                y += y < to.Y ? 1 : -1;
                var tile = new TileCoordinate(x, y);
                if (!world.TryGetRoadStationAt(tile, out _) && world.Map.GetObjectId(tile) == TileMap.NoObjectId)
                {
                    Assert.That(processor.Process(new Tick(tick++), new BuildRoadCommand(tile)).IsAccepted, Is.True);
                }
            }
        }
    }
}
