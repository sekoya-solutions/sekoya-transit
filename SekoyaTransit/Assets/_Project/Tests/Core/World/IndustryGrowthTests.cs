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
    public sealed class IndustryGrowthTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly IndustryGrowthSettings _growth = new IndustryGrowthSettings(60, 3, 2, 2);

        [TestCase(100L, 60L, true)]
        [TestCase(100L, 59L, false)]
        [TestCase(0L, 0L, false)]
        public void IsWellServed_ComparesTransportedShare(long produced, long transported, bool expected)
        {
            Assert.That(IndustryGrowth.IsWellServed(produced, transported, _growth), Is.EqualTo(expected));
        }

        [Test]
        public void CloseMonth_FullStreak_GainsALevelAndRestartsTheStreak()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 400L, monthTransported: 300L, level: 0, servedMonths: 0);

            IndustryGrowth.CloseMonth(forest, _growth);
            IndustryGrowth.CloseMonth(forest, _growth);
            Assert.That(forest.ProductionLevel, Is.Zero);
            Assert.That(forest.ServedMonths, Is.EqualTo(2));

            IndustryGrowth.CloseMonth(forest, _growth);

            Assert.That(forest.ProductionLevel, Is.EqualTo(1));
            Assert.That(forest.ServedMonths, Is.Zero);
        }

        [Test]
        public void CloseMonth_PoorlyServedMonth_ResetsTheStreakAndKeepsTheLevel()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 400L, monthTransported: 100L, level: 1, servedMonths: 2);

            IndustryGrowth.CloseMonth(forest, _growth);

            Assert.That(forest.ProductionLevel, Is.EqualTo(1));
            Assert.That(forest.ServedMonths, Is.Zero);
        }

        [Test]
        public void CloseMonth_AtMaxLevel_StaysThere()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 400L, monthTransported: 400L, level: 2, servedMonths: 0);

            for (int month = 0; month < 6; month++)
            {
                IndustryGrowth.CloseMonth(forest, _growth);
            }

            Assert.That(forest.ProductionLevel, Is.EqualTo(2));
            Assert.That(forest.ServedMonths, Is.Zero);
        }

        [Test]
        public void Step_ServedForest_GrowsAndProducesMorePerPulse()
        {
            // Any transport counts and one month is enough, so a single truck load in January grows the forest.
            ContentCatalog catalog = CreateCatalog(new IndustryGrowthSettings(1, 1, 3, 2));
            GeneratedWorld world = CreateForestAndSawmillWorld(catalog, out RoadStation pickup, out RoadStation consumer);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.PlaceRoadVehicle(
                processor,
                world,
                new[] { new RoadVehicleOrder(pickup.Id, waitForFull: true), new RoadVehicleOrder(consumer.Id) },
                catalog);
            PlacedIndustry forest = FindIndustry(world, "forest");
            var calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
            long tick = 101;
            while (calendar.GetDate(new Tick(tick)).Month == 1)
            {
                tick++;
                WorldSimulationStep.Advance(world, catalog, calendar, new Tick(tick), new EventBus());
            }

            Assert.That(forest.ProductionLevel, Is.EqualTo(1));
            Assert.That(forest.LastMonthProduced, Is.GreaterThan(0L));
            Assert.That(forest.LastMonthTransported, Is.GreaterThan(0L));
            long producedBefore = forest.MonthProduced;
            long nextPulse = ((tick / IndustryProduction.ProductionIntervalTicks) + 1) * IndustryProduction.ProductionIntervalTicks;
            while (tick < nextPulse)
            {
                tick++;
                WorldSimulationStep.Advance(world, catalog, calendar, new Tick(tick), new EventBus());
            }

            Assert.That(forest.MonthProduced - producedBefore, Is.EqualTo(8L));
        }

        [Test]
        public void Step_UnservedForest_KeepsTheBaseLevel()
        {
            ContentCatalog catalog = CreateCatalog(new IndustryGrowthSettings(1, 1, 3, 2));
            GeneratedWorld world = CreateForestAndSawmillWorld(catalog, out _, out _);
            var calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
            for (long tick = 1; tick < 9000; tick++)
            {
                WorldSimulationStep.Advance(world, catalog, calendar, new Tick(tick), new EventBus());
            }

            PlacedIndustry forest = FindIndustry(world, "forest");
            Assert.That(forest.ProductionLevel, Is.Zero);
            Assert.That(forest.ServedMonths, Is.Zero);
        }

        [Test]
        public void Step_GrowthDisabled_NeverGrows()
        {
            ContentCatalog catalog = CreateCatalog(IndustryGrowthSettings.Disabled);
            GeneratedWorld world = CreateForestAndSawmillWorld(catalog, out RoadStation pickup, out RoadStation consumer);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.PlaceRoadVehicle(
                processor,
                world,
                new[] { new RoadVehicleOrder(pickup.Id, waitForFull: true), new RoadVehicleOrder(consumer.Id) },
                catalog);
            var calendar = new SimulationCalendar(TimeSpan.FromMilliseconds(50));
            for (long tick = 102; tick < 9000; tick++)
            {
                WorldSimulationStep.Advance(world, catalog, calendar, new Tick(tick), new EventBus());
            }

            Assert.That(FindIndustry(world, "forest").ProductionLevel, Is.Zero);
        }

        [Test]
        public void CloseMonth_StrictStreak_NeedsTwelveStraightMonthsAtEightyPercent()
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 500L, monthTransported: 400L, level: 0, servedMonths: 0);

            for (int month = 0; month < 11; month++)
            {
                IndustryGrowth.CloseMonth(forest, growth);
            }

            Assert.That(forest.ProductionLevel, Is.Zero);
            Assert.That(forest.ServedMonths, Is.EqualTo(11));

            IndustryGrowth.CloseMonth(forest, growth);

            Assert.That(forest.ProductionLevel, Is.EqualTo(1));
            Assert.That(forest.ServedMonths, Is.Zero);
        }

        [Test]
        public void CloseMonth_JustBelowEightyPercent_ResetsAStreakOfEleven()
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 500L, monthTransported: 399L, level: 0, servedMonths: 11);

            IndustryGrowth.CloseMonth(forest, growth);

            Assert.That(forest.ProductionLevel, Is.Zero);
            Assert.That(forest.ServedMonths, Is.Zero);
        }

        [Test]
        public void CloseMonth_FiveLevels_GrowsEveryTwelveServedMonthsUpToLevelFive()
        {
            // Issue #328: the shipped rule (80 %, 12 months, zero-based max tier 4, x2) reaches level 5 after 48
            // well-served months and stops there.
            var growth = new IndustryGrowthSettings(80, 12, 4, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 500L, monthTransported: 400L, level: 0, servedMonths: 0);

            for (int level = 1; level <= 4; level++)
            {
                for (int month = 0; month < 12; month++)
                {
                    IndustryGrowth.CloseMonth(forest, growth);
                }

                Assert.That(forest.ProductionLevel, Is.EqualTo(level));
                Assert.That(forest.ServedMonths, Is.Zero);
            }

            for (int month = 0; month < 24; month++)
            {
                IndustryGrowth.CloseMonth(forest, growth);
            }

            Assert.That(forest.ProductionLevel, Is.EqualTo(4));
            Assert.That(forest.ServedMonths, Is.Zero);
            Assert.That(forest.CargoCapacity, Is.EqualTo(640));
            Assert.That(growth.ScaleOutput(4, forest.ProductionLevel), Is.EqualTo(64));
        }

        [TestCase(0, 40)]
        [TestCase(1, 80)]
        [TestCase(2, 160)]
        [TestCase(3, 320)]
        [TestCase(4, 640)]
        public void ApplyTier_ScalesTheStockCapacityPerTier(int level, int expectedCapacity)
        {
            var growth = new IndustryGrowthSettings(80, 12, 4, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 0L, monthTransported: 0L, level: level, servedMonths: 0);

            IndustryGrowth.ApplyTier(forest, growth);

            Assert.That(forest.CargoCapacity, Is.EqualTo(expectedCapacity));
            Assert.That(forest.FreeCargoCapacity, Is.EqualTo(expectedCapacity));
        }

        [Test]
        public void ApplyTier_LevelAboveTheMaximum_IsCappedAndKeepsTheStreak()
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 0L, monthTransported: 0L, level: 6, servedMonths: 4);

            IndustryGrowth.ApplyTier(forest, growth);

            Assert.That(forest.ProductionLevel, Is.EqualTo(2));
            Assert.That(forest.ServedMonths, Is.EqualTo(4));
            Assert.That(forest.CargoCapacity, Is.EqualTo(160));
        }

        [Test]
        public void ApplyTier_LowerMaximum_TrimsTheStockToTheNewCapacity()
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 0L, monthTransported: 0L, level: 2, servedMonths: 0);
            IndustryGrowth.ApplyTier(forest, growth);
            forest.TryAddCargo("wood", 150);

            IndustryGrowth.ApplyTier(forest, new IndustryGrowthSettings(80, 12, 0, 2));

            Assert.That(forest.ProductionLevel, Is.Zero);
            Assert.That(forest.Cargo.TotalAmount, Is.EqualTo(40));
        }

        [Test]
        public void EnsureMonthStats_NextMonth_KeepsLastMonthsProductionAndDelivery()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 500L, monthTransported: 450L, level: 0, servedMonths: 0);

            forest.EnsureMonthStats(new GameDate(1900, 2, 1));

            Assert.That(forest.LastMonthProduced, Is.EqualTo(500L));
            Assert.That(forest.LastMonthTransported, Is.EqualTo(450L));
            Assert.That(forest.MonthProduced, Is.Zero);
            Assert.That(forest.MonthTransported, Is.Zero);
        }

        [Test]
        public void EnsureMonthStats_DeliveredAboveProduced_CapsLastMonthDeliveredAtProduction()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 404L, monthTransported: 406L, level: 0, servedMonths: 0);

            forest.EnsureMonthStats(new GameDate(1900, 2, 1));

            Assert.That(forest.LastMonthProduced, Is.EqualTo(404L));
            Assert.That(forest.LastMonthTransported, Is.EqualTo(404L));
        }

        [Test]
        public void EnsureMonthStats_SkippedMonth_ReportsZeroForLastMonth()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 500L, monthTransported: 450L, level: 0, servedMonths: 0);

            forest.EnsureMonthStats(new GameDate(1900, 3, 1));

            Assert.That(forest.LastMonthProduced, Is.Zero);
            Assert.That(forest.LastMonthTransported, Is.Zero);
        }

        [Test]
        public void EnsureMonthStats_SameMonth_KeepsTheCountersAndLastMonth()
        {
            PlacedIndustry forest = CreateForest(monthProduced: 500L, monthTransported: 450L, level: 0, servedMonths: 0);

            forest.EnsureMonthStats(new GameDate(1900, 1, 20));

            Assert.That(forest.MonthProduced, Is.EqualTo(500L));
            Assert.That(forest.LastMonthProduced, Is.Zero);
        }

        [Test]
        public void Copy_KeepsLastMonthAndStockCapacity()
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);
            PlacedIndustry forest = CreateForest(monthProduced: 0L, monthTransported: 0L, level: 1, servedMonths: 3);
            IndustryGrowth.ApplyTier(forest, growth);
            forest.EnsureMonthStats(new GameDate(1900, 2, 1));

            PlacedIndustry copy = forest.Copy();

            Assert.That(copy.CargoCapacity, Is.EqualTo(80));
            Assert.That(copy.LastMonthProduced, Is.EqualTo(forest.LastMonthProduced));
            Assert.That(copy.ServedMonths, Is.EqualTo(3));
        }

        private static PlacedIndustry CreateForest(long monthProduced, long monthTransported, int level, int servedMonths)
        {
            return new PlacedIndustry(
                "forest",
                0,
                new TileCoordinate(4, 4),
                null,
                monthProduced,
                monthTransported,
                1900,
                1,
                0,
                null,
                level,
                servedMonths);
        }

        private static ContentCatalog CreateCatalog(IndustryGrowthSettings growth)
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
                demandPerNeedPerMonth: new TownNeedLevelAmounts(10, 10, 10),
                allNeedsMetGrowthMultiplier: 1);
            return new ContentCatalog(cargo, industries, townNeeds, new EconomySettings(100000L, null, growth));
        }

        private static GeneratedWorld CreateForestAndSawmillWorld(
            ContentCatalog catalog,
            out RoadStation pickup,
            out RoadStation consumer)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(new TownPlacementSettings(MapSize.Small, 1, 8), 5, industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 48014UL, catalog.TownNeeds, catalog);
            pickup = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate, 1);
            consumer = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate, 2);
            return world;
        }

        private static RoadStation BuildAdjacentStation(GeneratedWorld world, TileCoordinate anchor, long tick)
        {
            var offsets = new[]
            {
                new TileCoordinate(0, 1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, -1),
                new TileCoordinate(-1, 0)
            };
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            for (int index = 0; index < offsets.Length; index++)
            {
                var tile = new TileCoordinate(anchor.X + offsets[index].X, anchor.Y + offsets[index].Y);
                if (world.Map.Contains(tile) &&
                    processor.Process(new Tick(tick), new BuildRoadStationCommand(tile)).IsAccepted &&
                    world.TryGetRoadStationAt(tile, out RoadStation? station) &&
                    station != null)
                {
                    return station;
                }
            }

            throw new InvalidOperationException("No buildable station tile next to the industry.");
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

            throw new InvalidOperationException($"No {industryId} in the world.");
        }
    }
}
