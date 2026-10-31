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
        private const int _demand = 10;

        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly GameDate _january = new GameDate(1900, 1, 1);
        private static readonly GameDate _february = new GameDate(1900, 2, 1);

        [Test]
        public void AdvanceAll_FullyMetProcessedNeed_GrowsByProcessedFullAmount()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24201UL);
            world.Towns[0].CreditNeedSupply("planks", _demand);

            TownGrowth.AdvanceAll(world, catalog, _february);

            Assert.That(world.Towns[0].Population, Is.EqualTo(start + 30));
            Assert.That(world.Towns[0].LastMonthGrowth, Is.EqualTo(30));
        }

        [Test]
        public void AdvanceAll_PartlyMetRawNeed_GrowsByRawPartialAmount()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24202UL);
            world.Towns[0].CreditNeedSupply("wood", 1);

            TownGrowth.AdvanceAll(world, catalog, _february);

            Assert.That(world.Towns[0].Population, Is.EqualTo(start + 10));
        }

        [Test]
        public void AdvanceAll_MixedNeeds_SumsEachNeedByLevelAndStatus()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24203UL);
            Town town = world.Towns[0];
            town.CreditNeedSupply("wood", _demand);
            town.CreditNeedSupply("wool", _demand - 1);
            town.CreditNeedSupply("planks", 3);

            TownGrowth.AdvanceAll(world, catalog, _february);

            // Raw full 20 + raw partial 10 + processed partial 15; finished unmet; no all-needs bonus.
            Assert.That(world.Towns[0].Population, Is.EqualTo(start + 45));
        }

        [Test]
        public void AdvanceAll_AllNeedsFullyMet_AppliesMultiplier()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24204UL);
            Town town = world.Towns[0];
            town.CreditNeedSupply("wood", _demand);
            town.CreditNeedSupply("wool", _demand + 5);
            town.CreditNeedSupply("planks", _demand);
            town.CreditNeedSupply("furniture", _demand);

            TownGrowth.AdvanceAll(world, catalog, _february);

            Assert.That(world.Towns[0].Population, Is.EqualTo(start + ((20 + 20 + 30 + 40) * 2)));
        }

        [Test]
        public void AdvanceAll_UnmetNeeds_DoesNotGrow()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24205UL);

            TownGrowth.AdvanceAll(world, catalog, _february);

            Assert.That(world.Towns[0].Population, Is.EqualTo(start));
            Assert.That(world.Towns[0].LastMonthGrowth, Is.EqualTo(0));
        }

        [Test]
        public void AdvanceAll_GrowthPastMaximum_CapsPopulationAndRecordsCappedGrowth()
        {
            (GeneratedWorld world, ContentCatalog catalog, _) = StartMonth(24206UL);
            world.SetTownPopulation(0, 9995, catalog.TownNeeds, catalog);
            world.Towns[0].CreditNeedSupply("planks", _demand);

            TownGrowth.AdvanceAll(world, catalog, _february);

            Assert.That(world.Towns[0].Population, Is.EqualTo(10000));
            Assert.That(world.Towns[0].LastMonthGrowth, Is.EqualTo(5));
        }

        [Test]
        public void AdvanceAll_NewMonth_RollsSupplyToLastMonthAndResetsCounters()
        {
            (GeneratedWorld world, ContentCatalog catalog, _) = StartMonth(24207UL);
            Town town = world.Towns[0];
            town.CreditNeedSupply("wood", 7);
            town.CreditMonthPassengersGenerated(5);
            town.CreditMonthPassengersTransported(2);
            town.CreditMonthMailGenerated(3);
            town.CreditMonthMailTransported(1);

            TownGrowth.AdvanceAll(world, catalog, _february);

            Town grown = world.Towns[0];
            TownNeed wood = FindNeed(grown, "wood");
            Assert.That(wood.LastMonthSupply, Is.EqualTo(7));
            Assert.That(wood.MonthSupply, Is.EqualTo(0));
            Assert.That(FindNeed(grown, "wool").LastMonthSupply, Is.EqualTo(0));
            Assert.That(grown.MonthPassengersGenerated, Is.EqualTo(0L));
            Assert.That(grown.MonthPassengersTransported, Is.EqualTo(0L));
            Assert.That(grown.MonthMailGenerated, Is.EqualTo(0L));
            Assert.That(grown.MonthMailTransported, Is.EqualTo(0L));
            Assert.That(grown.StatsMonth, Is.EqualTo(2));
        }

        [Test]
        public void AdvanceAll_SameMonth_DoesNothing()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24208UL);
            world.Towns[0].CreditNeedSupply("planks", _demand);

            TownGrowth.AdvanceAll(world, catalog, new GameDate(1900, 1, 20));

            Assert.That(world.Towns[0].Population, Is.EqualTo(start));
            Assert.That(FindNeed(world.Towns[0], "planks").MonthSupply, Is.EqualTo(_demand));
        }

        [Test]
        public void AdvanceAll_FirstMonthWithoutStats_DoesNotGrow()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = GenerateWorld(24209UL, catalog);
            int start = world.Towns[0].Population;
            world.Towns[0].CreditNeedSupply("planks", _demand);

            TownGrowth.AdvanceAll(world, catalog, _january);

            Assert.That(world.Towns[0].Population, Is.EqualTo(start));
            Assert.That(world.Towns[0].StatsMonth, Is.EqualTo(1));
        }

        [Test]
        public void SetTownPopulation_KeptNeeds_KeepMonthAndLastMonthSupply()
        {
            (GeneratedWorld world, ContentCatalog catalog, int start) = StartMonth(24210UL);
            world.Towns[0].CreditNeedSupply("wood", 4);
            TownGrowth.AdvanceAll(world, catalog, _february);
            world.Towns[0].CreditNeedSupply("wood", 6);

            world.SetTownPopulation(0, start + 100, catalog.TownNeeds, catalog);

            TownNeed wood = FindNeed(world.Towns[0], "wood");
            Assert.That(wood.LastMonthSupply, Is.EqualTo(4));
            Assert.That(wood.MonthSupply, Is.EqualTo(6));
        }

        [Test]
        public void ComputeGrowth_NoNeeds_ReturnsZero()
        {
            ContentCatalog catalog = CreateCatalog();

            int growth = TownGrowth.ComputeGrowth(Array.Empty<TownNeed>(), catalog.TownNeeds, catalog);

            Assert.That(growth, Is.EqualTo(0));
        }

        [TestCase(0, TownNeedStatus.Unmet)]
        [TestCase(1, TownNeedStatus.PartlyMet)]
        [TestCase(9, TownNeedStatus.PartlyMet)]
        [TestCase(10, TownNeedStatus.FullyMet)]
        [TestCase(25, TownNeedStatus.FullyMet)]
        public void GetStatus_Supply_ClassifiesAgainstDemand(int supply, TownNeedStatus expected)
        {
            Assert.That(TownGrowth.GetStatus(supply, _demand), Is.EqualTo(expected));
        }

        private static (GeneratedWorld World, ContentCatalog Catalog, int StartPopulation) StartMonth(ulong seed)
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = GenerateWorld(seed, catalog);
            Assert.That(world.Towns[0].Needs, Has.Count.EqualTo(4));
            world.Towns[0].EnsureMonthStats(_january);
            return (world, catalog, world.Towns[0].Population);
        }

        private static TownNeed FindNeed(Town town, string cargoId)
        {
            for (int index = 0; index < town.Needs.Count; index++)
            {
                if (town.Needs[index].CargoId == cargoId)
                {
                    return town.Needs[index];
                }
            }

            throw new AssertionException($"Town has no {cargoId} need.");
        }

        private static ContentCatalog CreateCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("wool", "cargo.wool", CargoClass.Cargo, 5, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2),
                new CargoDefinition("furniture", "cargo.furniture", CargoClass.Cargo, 40, 3)
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 2, 1, 1) },
                demandPerNeedPerMonth: _demand,
                populationPerPartlyMetNeed: new TownNeedLevelAmounts(10, 15, 20),
                populationPerFullyMetNeed: new TownNeedLevelAmounts(20, 30, 40),
                allNeedsMetGrowthMultiplier: 2);
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
