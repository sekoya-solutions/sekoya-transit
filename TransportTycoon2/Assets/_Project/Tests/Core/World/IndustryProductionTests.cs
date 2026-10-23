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
        private static readonly GameDate _january = new GameDate(1900, 1, 15);
        private static readonly GameDate _february = new GameDate(1900, 2, 1);

        [Test]
        public void AdvanceAll_PrimaryIndustry_ProducesOntoIndustryStockOnInterval()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out _);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks - 1),
                _january);
            Assert.That(world.Industries[0].Cargo.IsEmpty, Is.True);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);
            Assert.That(world.Industries[0].Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(world.Industries[0].MonthProduced, Is.EqualTo(4L));
            Assert.That(world.Industries[0].MonthTransported, Is.EqualTo(0L));
        }

        [Test]
        public void AdvanceAll_SecondaryIndustry_ConsumesInputsFromIndustryStock()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out _);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill")!;
            sawmill.TryAddCargo("wood", 4);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);

            Assert.That(sawmill.Cargo.GetAmount("wood"), Is.EqualTo(2));
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(1));
            Assert.That(sawmill.MonthProduced, Is.EqualTo(1L));
            Assert.That(sawmill.MonthTransported, Is.EqualTo(0L));
        }

        [Test]
        public void AdvanceAll_WhenFull_StillCreditsProducedAndWastesExcess()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out _);
            PlacedIndustry forest = world.Industries[0];
            while (forest.FreeCargoCapacity > 0)
            {
                forest.TryAddCargo("coal", Math.Min(5, forest.FreeCargoCapacity));
            }

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);

            Assert.That(forest.FreeCargoCapacity, Is.EqualTo(0));
            Assert.That(forest.Cargo.GetAmount("wood"), Is.EqualTo(0));
            Assert.That(forest.MonthProduced, Is.EqualTo(4L));
            Assert.That(forest.MonthTransported, Is.EqualTo(0L));
        }

        [Test]
        public void AdvanceAll_SecondaryWhenFull_ConsumesInputsAndWastesOutputs()
        {
            // 1 wood in → 5 planks out so most output is wasted when the bag is full.
            var catalog = new ContentCatalog(
                new[]
                {
                    new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8),
                    new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14),
                    new CargoDefinition("coal", "cargo.coal", CargoClass.Cargo, 5)
                },
                new[]
                {
                    new IndustryDefinition(
                        "sawmill",
                        "industry.sawmill",
                        2,
                        new[] { new CargoAmountDefinition("wood", 1) },
                        new[] { new CargoAmountDefinition("planks", 5) })
                });
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out _);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill")!;
            sawmill.Cargo.Clear();
            sawmill.TryAddCargo("wood", 1);
            while (sawmill.FreeCargoCapacity > 0)
            {
                sawmill.TryAddCargo("coal", Math.Min(5, sawmill.FreeCargoCapacity));
            }

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);

            Assert.That(sawmill.Cargo.GetAmount("wood"), Is.EqualTo(0));
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(1));
            Assert.That(sawmill.MonthProduced, Is.EqualTo(5L));
        }

        [Test]
        public void AdvanceAll_IsDeterministicAcrossWorlds()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world1 = CreateForestWithAdjacentStation(out _);
            GeneratedWorld world2 = CreateForestWithAdjacentStation(out _);

            IndustryProduction.AdvanceAll(
                world1,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);
            IndustryProduction.AdvanceAll(
                world2,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);

            Assert.That(
                world1.Industries[0].Cargo.GetAmount("wood"),
                Is.EqualTo(world2.Industries[0].Cargo.GetAmount("wood")));
        }

        [Test]
        public void AdvanceAll_ResetsMonthCountersAtCalendarMonthBoundary()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out _);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);
            Assert.That(world.Industries[0].MonthProduced, Is.EqualTo(4L));
            Assert.That(world.Industries[0].StatsMonth, Is.EqualTo(1));

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks * 2),
                _february);
            Assert.That(world.Industries[0].MonthProduced, Is.EqualTo(4L));
            Assert.That(world.Industries[0].MonthTransported, Is.EqualTo(0L));
            Assert.That(world.Industries[0].StatsYear, Is.EqualTo(1900));
            Assert.That(world.Industries[0].StatsMonth, Is.EqualTo(2));
        }

        [Test]
        public void AdvanceIndustriesAdjacentTo_PrimaryIndustry_FillsIndustryImmediately()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateForestWithAdjacentStation(out RoadStation station);
            world.Industries[0].Cargo.Clear();

            IndustryProduction.AdvanceIndustriesAdjacentTo(world, catalog, station.Coordinate, _january);

            Assert.That(world.Industries[0].Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(world.Industries[0].MonthProduced, Is.EqualTo(4L));
            Assert.That(world.Industries[0].MonthTransported, Is.EqualTo(0L));
        }

        private static PlacedIndustry? FindIndustry(GeneratedWorld world, string industryId)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (string.Equals(world.Industries[index].IndustryId, industryId, StringComparison.Ordinal))
                {
                    return world.Industries[index];
                }
            }

            return null;
        }

        private static ContentCatalog CreateWoodChainCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14),
                new CargoDefinition("coal", "cargo.coal", CargoClass.Cargo, 5)
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
            PlacedIndustry? sawmill = FindIndustry(world, "sawmill");
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
