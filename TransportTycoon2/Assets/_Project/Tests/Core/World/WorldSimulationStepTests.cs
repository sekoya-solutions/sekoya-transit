#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class WorldSimulationStepTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly TimeSpan _tickDuration = TimeSpan.FromMilliseconds(50);

        [Test]
        public void Advance_IdleWorldMidMonth_AllocatesNothing()
        {
#if UNITY_5_3_OR_NEWER
            Assert.Ignore("Unity's Mono runtime does not report per-thread allocated bytes; dotnet test enforces this.");
#endif
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld world = GenerateForestAndSawmillWorld(catalog);
            world.OfferContract(
                "wood",
                DeliveryDestinationKind.Industry,
                0,
                50,
                new GameDate(1900, 1, 1),
                new GameDate(1902, 1, 1));
            var events = new EventBus();
            Tick tick = Tick.Zero;

            // Warm up past the first tick of the month, which books the month's contracts and stats.
            for (int step = 0; step < 100; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                events.DispatchPending();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int step = 0; step < 200; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                events.DispatchPending();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(calendar.GetDate(tick).Month, Is.EqualTo(1), "the measured ticks must stay inside one month");
            Assert.That(world.OpenContracts.Count, Is.Positive, "open contracts must be scanned every tick");
            Assert.That(allocated, Is.Zero);
        }

        private static ContentCatalog CreateWoodChainCatalog()
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
            return new ContentCatalog(cargo, industries, townNeeds);
        }

        private static GeneratedWorld GenerateForestAndSawmillWorld(ContentCatalog catalog)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 3, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, 70001UL, catalog.TownNeeds, catalog);
        }
    }
}
