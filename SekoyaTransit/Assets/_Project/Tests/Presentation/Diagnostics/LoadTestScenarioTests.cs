#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Diagnostics;

namespace SekoyaTransit.Presentation.Tests.Diagnostics
{
    public sealed class LoadTestScenarioTests
    {
        private static readonly TimeSpan _tickDuration = TimeSpan.FromMilliseconds(50);

        [Test]
        public void Build_GeneratedWorld_PlacesAboutTwoHundredVehiclesThatRun()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorld(catalog);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            LoadTestScenarioResult result = LoadTestScenario.Build(world, catalog, processor, GameDate.FirstYear);

            Assert.That(result.RoadLines, Is.EqualTo(20));
            Assert.That(result.RailLines, Is.EqualTo(10));
            Assert.That(result.RoadVehicles, Is.EqualTo(160));
            Assert.That(result.RailVehicles, Is.EqualTo(40));
            Assert.That(world.RoadVehicles.Count, Is.EqualTo(result.RoadVehicles));
            Assert.That(world.RailVehicles.Count, Is.EqualTo(result.RailVehicles));
            Assert.That(world.RailVehicles[0].WagonCount, Is.Positive);

            var calendar = new SimulationCalendar(_tickDuration);
            var events = new EventBus();
            Tick tick = Tick.Zero;
            for (int step = 0; step < 400; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                events.DispatchPending();
            }

            Assert.That(CountRoadVehiclesOnEdge(world), Is.Positive, "trucks must be driving their lines");
        }

        private static int CountRoadVehiclesOnEdge(GeneratedWorld world)
        {
            int moving = 0;
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                if (world.RoadVehicles[index].IsOnEdge)
                {
                    moving++;
                }
            }

            return moving;
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
                demandPerNeedPerMonth: 10,
                allNeedsMetGrowthMultiplier: 1);
            return new ContentCatalog(cargo, industries, townNeeds);
        }

        private static GeneratedWorld CreateWorld(ContentCatalog catalog)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Medium, 30, 12),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 20, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 10, 20),
                });
            return WorldGenerator.Generate(settings, 70003UL, catalog.TownNeeds, catalog);
        }
    }
}
