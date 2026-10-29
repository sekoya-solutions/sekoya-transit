#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
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
        public void AdvanceAll_SecondaryIndustry_DoesNotProduceOnPulse()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out _);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill")!;
            sawmill.InputLeftover.Add("wood", 1);

            IndustryProduction.AdvanceAll(
                world,
                catalog,
                new Tick(IndustryProduction.ProductionIntervalTicks),
                _january);

            Assert.That(sawmill.Cargo.IsEmpty, Is.True);
            Assert.That(sawmill.InputLeftover.GetAmount("wood"), Is.EqualTo(1));
            Assert.That(sawmill.MonthProduced, Is.EqualTo(0L));
        }

        [Test]
        public void TryDeliverInput_CompleteRecipes_ProducesAtOnceAndKeepsLeftoverHidden()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out _);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill")!;
            Assert.That(catalog.TryGetIndustry("sawmill", out IndustryDefinition? definition), Is.True);

            bool taken = IndustryProduction.TryDeliverInput(sawmill, definition!, "wood", 5, _january);

            Assert.That(taken, Is.True);
            Assert.That(sawmill.Cargo.GetAmount("wood"), Is.EqualTo(0));
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(2));
            Assert.That(sawmill.InputLeftover.GetAmount("wood"), Is.EqualTo(1));
            Assert.That(sawmill.FreeCargoCapacity, Is.EqualTo(PlacedIndustry.PlaceholderCargoCapacity - 2));
            Assert.That(sawmill.MonthProduced, Is.EqualTo(2L));

            IndustryProduction.TryDeliverInput(sawmill, definition!, "wood", 1, _january);

            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(3));
            Assert.That(sawmill.InputLeftover.IsEmpty, Is.True);
        }

        [Test]
        public void TryDeliverInput_OutputStockFull_TakesInputAndLosesOutput()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out _);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill")!;
            sawmill.TryAddCargo("planks", PlacedIndustry.PlaceholderCargoCapacity);
            Assert.That(catalog.TryGetIndustry("sawmill", out IndustryDefinition? definition), Is.True);

            bool taken = IndustryProduction.TryDeliverInput(sawmill, definition!, "wood", 4, _january);

            Assert.That(taken, Is.True);
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(PlacedIndustry.PlaceholderCargoCapacity));
            Assert.That(sawmill.InputLeftover.IsEmpty, Is.True);
            Assert.That(sawmill.MonthProduced, Is.EqualTo(2L));
        }

        [Test]
        public void TryDeliverInput_NotAnInput_TakesNothing()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            GeneratedWorld world = CreateSawmillWithAdjacentStation(out _);
            PlacedIndustry sawmill = FindIndustry(world, "sawmill")!;
            Assert.That(catalog.TryGetIndustry("sawmill", out IndustryDefinition? definition), Is.True);

            bool taken = IndustryProduction.TryDeliverInput(sawmill, definition!, "planks", 4, _january);

            Assert.That(taken, Is.False);
            Assert.That(sawmill.Cargo.IsEmpty, Is.True);
            Assert.That(sawmill.InputLeftover.IsEmpty, Is.True);
        }

        [Test]
        public void TryDeliverInput_MissingSecondInput_CapsLeftoverAtOneRecipe()
        {
            var definition = new IndustryDefinition(
                "furniture_factory",
                "industry.furniture_factory",
                3,
                new[] { new CargoAmountDefinition("planks", 2), new CargoAmountDefinition("iron", 1) },
                new[] { new CargoAmountDefinition("furniture", 1) });
            var factory = new PlacedIndustry("furniture_factory", 0, new TileCoordinate(4, 4));

            IndustryProduction.TryDeliverInput(factory, definition, "planks", 5, _january);

            Assert.That(factory.InputLeftover.GetAmount("planks"), Is.EqualTo(2));
            Assert.That(factory.Cargo.IsEmpty, Is.True);

            IndustryProduction.TryDeliverInput(factory, definition, "iron", 1, _january);

            Assert.That(factory.Cargo.GetAmount("furniture"), Is.EqualTo(1));
            Assert.That(factory.InputLeftover.IsEmpty, Is.True);
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
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2),
                new CargoDefinition("coal", "cargo.coal", CargoClass.Cargo, 5, 1)
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
