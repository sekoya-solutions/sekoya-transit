#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class TownGrowthTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };

        [Test]
        public void AdvanceAll_SatisfiedNeeds_GrowsPopulationAtMonthEnd()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = GenerateWorld(58001UL, catalog);
            Town town = world.Towns[0];
            Assert.That(town.Needs, Has.Count.EqualTo(1));
            Assert.That(town.Needs[0].CargoId, Is.EqualTo("wood"));

            int startPopulation = town.Population;
            town.EnsureMonthStats(new GameDate(1900, 1, 1));
            town.CreditNeedSupply("wood", catalog.TownNeeds.DemandPerNeedPerMonth);
            town.CreditMonthPassengersGenerated(5);
            town.CreditMonthPassengersTransported(2);
            town.CreditMonthMailGenerated(3);
            town.CreditMonthMailTransported(1);

            TownGrowth.AdvanceAll(world, catalog, new GameDate(1900, 2, 1));

            Assert.That(
                world.Towns[0].Population,
                Is.EqualTo(startPopulation + catalog.TownNeeds.PopulationPerSatisfiedNeed));
            Assert.That(world.Towns[0].Needs[0].MonthSupply, Is.EqualTo(0));
            Assert.That(world.Towns[0].MonthPassengersGenerated, Is.EqualTo(0L));
            Assert.That(world.Towns[0].MonthPassengersTransported, Is.EqualTo(0L));
            Assert.That(world.Towns[0].MonthMailGenerated, Is.EqualTo(0L));
            Assert.That(world.Towns[0].MonthMailTransported, Is.EqualTo(0L));
            Assert.That(world.Towns[0].StatsMonth, Is.EqualTo(2));
        }

        [Test]
        public void AdvanceAll_UnmetNeeds_DoesNotGrow()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = GenerateWorld(58002UL, catalog);
            Town town = world.Towns[0];
            int startPopulation = town.Population;
            town.EnsureMonthStats(new GameDate(1900, 1, 1));
            town.CreditNeedSupply("wood", catalog.TownNeeds.DemandPerNeedPerMonth - 1);

            TownGrowth.AdvanceAll(world, catalog, new GameDate(1900, 2, 1));

            Assert.That(world.Towns[0].Population, Is.EqualTo(startPopulation));
        }

        private static ContentCatalog CreateCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1)
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                demandPerNeedPerMonth: 10,
                populationPerSatisfiedNeed: 20);
            return new ContentCatalog(cargo, Array.Empty<IndustryDefinition>(), townNeeds);
        }

        private static GeneratedWorld GenerateWorld(ulong seed, ContentCatalog catalog)
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
    }
}
