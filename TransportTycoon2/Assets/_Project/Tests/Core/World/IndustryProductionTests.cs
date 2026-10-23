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
    public sealed class IndustryProductionTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };

        [Test]
        public void AdvanceAll_PrimaryIndustry_ProducesIntoAdjacentStationOnInterval()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out RoadStation station);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks - 1));
            Assert.That(station.Cargo.IsEmpty, Is.True);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks));
            Assert.That(station.Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(world.Industries[0].Cargo.IsEmpty, Is.True);
        }

        [Test]
        public void AdvanceAll_SecondaryIndustry_ConsumesInputsAndPushesOutputs()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out RoadStation station);
            station.TryAddCargo("wood", 4);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks));

            Assert.That(station.Cargo.GetAmount("wood"), Is.EqualTo(2));
            Assert.That(station.Cargo.GetAmount("planks"), Is.EqualTo(1));
        }

        [Test]
        public void AdvanceAll_RespectsStationCapacityWhenPushingOutputs()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out RoadStation station);
            while (station.FreeCargoCapacity > 0)
            {
                station.TryAddCargo("coal", Math.Min(5, station.FreeCargoCapacity));
            }

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks));

            Assert.That(station.FreeCargoCapacity, Is.EqualTo(0));
            Assert.That(world.Industries[0].Cargo.GetAmount("wood"), Is.EqualTo(4));
        }

        [Test]
        public void AdvanceAll_IsDeterministicAcrossWorlds()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world1 = CreateForestWithAdjacentStation(out RoadStation station1);
            GeneratedWorld world2 = CreateForestWithAdjacentStation(out RoadStation station2);

            IndustryProduction.AdvanceAll(
                world1,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks));
            IndustryProduction.AdvanceAll(
                world2,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks));

            Assert.That(station1.Cargo.GetAmount("wood"), Is.EqualTo(station2.Cargo.GetAmount("wood")));
        }

        [Test]
        public void AdvanceIndustriesAdjacentTo_PrimaryIndustry_FillsNewStationImmediately()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out RoadStation station);
            station.Cargo.Clear();

            IndustryProduction.AdvanceIndustriesAdjacentTo(world, catalog, station.Coordinate);

            Assert.That(station.Cargo.GetAmount("wood"), Is.EqualTo(4));
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

        private static GeneratedWorld CreateForestWithAdjacentStation(out RoadStation station)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 48001UL);
            PlacedIndustry forest = world.Industries[0];
            var stationCoordinate = new TileCoordinate(forest.Coordinate.X + 1, forest.Coordinate.Y);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(new Tick(1), new BuildRoadStationCommand(stationCoordinate)).IsAccepted,
                Is.True);
            Assert.That(world.TryGetRoadStationAt(stationCoordinate, out RoadStation? built), Is.True);
            station = built!;
            return world;
        }

        private static GeneratedWorld CreateSawmillWithAdjacentStation(out RoadStation station)
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
            GeneratedWorld world = WorldGenerator.Generate(settings, 48002UL);
            PlacedIndustry? sawmill = null;
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (string.Equals(world.Industries[index].IndustryId, "sawmill", StringComparison.Ordinal))
                {
                    sawmill = world.Industries[index];
                    break;
                }
            }

            Assert.That(sawmill, Is.Not.Null);
            TileCoordinate stationCoordinate = FindEmptyOrthogonalNeighbor(world, sawmill!.Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(new Tick(2), new BuildRoadStationCommand(stationCoordinate)).IsAccepted,
                Is.True);
            Assert.That(world.TryGetRoadStationAt(stationCoordinate, out RoadStation? built), Is.True);
            station = built!;
            return world;
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
    }
}
