#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class TownNeedsAssignmentTests
    {
        private static readonly string[] _forestOutputs = { "wood" };
        private static readonly string[] _twoNames = { "Paris", "Rome" };
        private static readonly string[] _fourNames = { "Paris", "Rome", "Oslo", "Lima" };

        [Test]
        public void CreateTowns_RollsPopulationInRangeAndAssignsStartingNeeds()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            var anchors = new[]
            {
                new TileCoordinate(1, 1),
                new TileCoordinate(10, 10),
            };

            Town[] towns = TownNeedsAssignment.CreateTowns(anchors, 57001UL, settings, catalog);

            Assert.That(towns, Has.Length.EqualTo(2));
            for (int index = 0; index < towns.Length; index++)
            {
                Assert.That(towns[index].Id, Is.EqualTo(index));
                Assert.That(towns[index].Coordinate, Is.EqualTo(anchors[index]));
                Assert.That(
                    towns[index].Population,
                    Is.InRange(settings.MinStartingPopulation, settings.MaxStartingPopulation));
                Assert.That(towns[index].Needs, Has.Count.EqualTo(3));
                Assert.That(towns[index].Needs.Select(need => need.CargoId).Distinct().Count(), Is.EqualTo(3));
                foreach (TownNeed need in towns[index].Needs)
                {
                    Assert.That(catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo), Is.True);
                    Assert.That(cargo!.ManufacturingLevel, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void CreateTowns_IsDeterministicForSameSeed()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            var anchors = new[] { new TileCoordinate(2, 3), new TileCoordinate(8, 9) };

            Town[] first = TownNeedsAssignment.CreateTowns(anchors, 57002UL, settings, catalog);
            Town[] second = TownNeedsAssignment.CreateTowns(anchors, 57002UL, settings, catalog);

            Assert.That(second.Select(town => town.Population).ToArray(), Is.EqualTo(first.Select(town => town.Population).ToArray()));
            Assert.That(
                second.Select(town => town.Needs.Select(need => need.CargoId).ToArray()).ToArray(),
                Is.EqualTo(first.Select(town => town.Needs.Select(need => need.CargoId).ToArray()).ToArray()));
        }

        [Test]
        public void SetPopulation_PreservesNeedPrefixWhenTierOnlyAddNeeds()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            var anchors = new[] { new TileCoordinate(4, 5) };
            Town[] towns = TownNeedsAssignment.CreateTowns(anchors, 57003UL, settings, catalog);
            Town town = towns[0];
            string[] startingNeeds = town.Needs.Select(need => need.CargoId).ToArray();
            Assert.That(startingNeeds, Has.Length.EqualTo(3));

            TownNeedsAssignment.SetPopulation(town, 1000, settings, catalog, 57003UL);

            Assert.That(town.Population, Is.EqualTo(1000));
            Assert.That(town.Needs, Has.Count.EqualTo(4));
            Assert.That(town.Needs.Take(3).Select(need => need.CargoId).ToArray(), Is.EqualTo(startingNeeds));
        }

        [Test]
        public void GetTierForPopulation_UsesHighestMatchingThreshold()
        {
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();

            Assert.That(settings.GetTierForPopulation(100).Level1Count, Is.EqualTo(3));
            Assert.That(settings.GetTierForPopulation(999).Level1Count, Is.EqualTo(3));
            Assert.That(settings.GetTierForPopulation(1000).Level1Count, Is.EqualTo(4));
            TownNeedTier maxTier = settings.GetTierForPopulation(10000);
            Assert.That(maxTier.Level1Count, Is.EqualTo(5));
            Assert.That(maxTier.Level2Count, Is.EqualTo(3));
            Assert.That(maxTier.Level3Count, Is.EqualTo(1));
        }

        [Test]
        public void WorldGenerator_WithCatalog_CreatesTownsWithNeeds()
        {
            ContentCatalog catalog = CreateCatalog();
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputs, 1, 0),
                });

            GeneratedWorld world = WorldGenerator.Generate(settings, 57004UL, catalog.TownNeeds, catalog);

            Assert.That(world.Towns, Has.Count.EqualTo(WorldGenerator.ResolveTownCount(2, catalog.TownNeeds, catalog)));
            Assert.That(world.MapSeed, Is.EqualTo(57004UL));
            Assert.That(world.Towns[0].Needs, Has.Count.EqualTo(3));
            world.SetTownPopulation(0, 2000, catalog.TownNeeds, catalog);
            Assert.That(world.Towns[0].Population, Is.EqualTo(2000));
            Assert.That(world.Towns[0].Needs.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void CargoDefinition_RejectsInvalidManufacturingLevels()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 1));
        }

        [Test]
        public void MinimumTownCount_FiveChains_NeedsFifteenTowns()
        {
            // At maximum population a town plans 5 level 1, 3 level 2 and 1 level 3 needs; 5 level 3 cargos x 3 towns.
            Assert.That(
                TownNeedsAssignment.MinimumTownCount(TownNeedsSettings.CreatePlaceholder(), CreateCatalog()),
                Is.EqualTo(15));
        }

        [TestCase(15)]
        [TestCase(20)]
        [TestCase(30)]
        public void DealNeedPlans_AtOrAboveMinimum_PlansEveryCargoInEnoughTowns(int townCount)
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            for (ulong seed = 1UL; seed <= 20UL; seed++)
            {
                TownNeedPlan[] plans = TownNeedsAssignment.DealNeedPlans(townCount, seed, settings, catalog);

                Assert.That(plans, Has.Length.EqualTo(townCount));
                foreach (CargoDefinition cargo in catalog.Cargo.Where(cargo => cargo.Class == CargoClass.Cargo))
                {
                    int planningTowns = plans.Count(plan => plan.Contains(cargo.Id));
                    Assert.That(
                        planningTowns,
                        Is.GreaterThanOrEqualTo(settings.MinTownsPerNeededCargo),
                        $"seed {seed}, cargo {cargo.Id}");
                }

                foreach (TownNeedPlan plan in plans)
                {
                    Assert.That(plan.GetLevel(1), Has.Count.EqualTo(5));
                    Assert.That(plan.GetLevel(2), Has.Count.EqualTo(3));
                    Assert.That(plan.GetLevel(3), Has.Count.EqualTo(1));
                    for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
                    {
                        foreach (string cargoId in plan.GetLevel(level))
                        {
                            Assert.That(catalog.TryGetCargo(cargoId, out CargoDefinition? cargo), Is.True);
                            Assert.That(cargo!.ManufacturingLevel, Is.EqualTo(level));
                        }
                    }
                }
            }
        }

        [Test]
        public void DealNeedPlans_SameSeed_IsDeterministicAndOtherSeedDiffers()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();

            string[] first = Flatten(TownNeedsAssignment.DealNeedPlans(15, 57010UL, settings, catalog));
            string[] second = Flatten(TownNeedsAssignment.DealNeedPlans(15, 57010UL, settings, catalog));
            string[] other = Flatten(TownNeedsAssignment.DealNeedPlans(15, 57011UL, settings, catalog));

            Assert.That(second, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void CreateTowns_NeedsAreThePlanPrefixAndGrowthRevealsTheRest()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            var anchors = new[] { new TileCoordinate(4, 5), new TileCoordinate(30, 30) };
            Town town = TownNeedsAssignment.CreateTowns(anchors, 57012UL, settings, catalog)[0];
            string[] planned = Flatten(new[] { town.NeedPlan });

            Assert.That(town.Needs.Select(need => need.CargoId), Is.EqualTo(town.NeedPlan.GetLevel(1).Take(3)));

            TownNeedsAssignment.SetPopulation(town, settings.MaxPopulation, settings, catalog, 57012UL);

            Assert.That(town.Needs.Select(need => need.CargoId).ToArray(), Is.EqualTo(planned));
        }

        [Test]
        public void CreateTowns_WithNames_GivesEachTownADistinctListedName()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = CreateNamedSettings(_fourNames);
            var anchors = new[] { new TileCoordinate(1, 1), new TileCoordinate(20, 20), new TileCoordinate(40, 40) };

            Town[] towns = TownNeedsAssignment.CreateTowns(anchors, 57013UL, settings, catalog);
            Town[] again = TownNeedsAssignment.CreateTowns(anchors, 57013UL, settings, catalog);

            Assert.That(towns.Select(town => town.Name).Distinct().Count(), Is.EqualTo(3));
            Assert.That(towns.Select(town => town.Name), Is.SubsetOf(_fourNames));
            Assert.That(again.Select(town => town.Name), Is.EqualTo(towns.Select(town => town.Name)));
        }

        [Test]
        public void PickNames_MoreTownsThanNames_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => TownNeedsAssignment.PickNames(_twoNames, 3, 1UL));
        }

        [Test]
        public void AssignMissingNamesAndPlans_LegacyTown_KeepsSavedNeedsAsPlanPrefix()
        {
            ContentCatalog catalog = CreateCatalog();
            TownNeedsSettings settings = CreateNamedSettings("Paris", "Rome");
            const ulong seed = 57014UL;
            TownNeedPlan legacyPlan = TownNeedsAssignment.CreateLegacyPlan(0, seed, settings, catalog);
            TownNeed[] savedNeeds = TownNeedsAssignment.NeedsForPopulation(2000, settings, legacyPlan);
            var town = new Town(0, new TileCoordinate(3, 3), 2000, savedNeeds);

            TownNeedsAssignment.AssignMissingNamesAndPlans(new[] { town }, seed, settings, catalog);

            Assert.That(_twoNames, Does.Contain(town.Name));
            Assert.That(Flatten(new[] { town.NeedPlan }), Is.EqualTo(Flatten(new[] { legacyPlan })));
            Assert.That(town.Needs.Select(need => need.CargoId), Is.EqualTo(savedNeeds.Select(need => need.CargoId)));
        }

        [Test]
        public void ContentCatalog_TooFewTownNames_Throws()
        {
            ContentCatalog source = CreateCatalog();
            var sizes = new[]
            {
                new WorldGenerationSizeDefinition(MapSize.Small, 2, 16, 4, CreateForestEntry()),
                new WorldGenerationSizeDefinition(MapSize.Medium, 2, 16, 4, CreateForestEntry()),
                new WorldGenerationSizeDefinition(MapSize.Large, 2, 16, 4, CreateForestEntry()),
            };
            var forest = new IndustryDefinition(
                "forest",
                "industry.forest",
                1,
                Array.Empty<CargoAmountDefinition>(),
                new[] { new CargoAmountDefinition("wood", 4) });

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new ContentCatalog(
                    source.Cargo,
                    new[] { forest },
                    CreateNamedSettings("Paris", "Rome"),
                    worldGeneration: new WorldGenerationData(sizes)))!;

            StringAssert.Contains("town name list", exception.Message);
        }

        private static WorldGenerationIndustryEntry[] CreateForestEntry()
        {
            return new[] { new WorldGenerationIndustryEntry("forest", 1, 0) };
        }

        private static TownNeedsSettings CreateNamedSettings(params string[] names)
        {
            TownNeedsSettings placeholder = TownNeedsSettings.CreatePlaceholder();
            return new TownNeedsSettings(
                placeholder.MinStartingPopulation,
                placeholder.MaxStartingPopulation,
                placeholder.MaxPopulation,
                placeholder.Tiers,
                names: names);
        }

        private static string[] Flatten(IEnumerable<TownNeedPlan> plans)
        {
            var cargoIds = new List<string>();
            foreach (TownNeedPlan plan in plans)
            {
                for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
                {
                    cargoIds.AddRange(plan.GetLevel(level));
                }
            }

            return cargoIds.ToArray();
        }

        private static ContentCatalog CreateCatalog()
        {
            var cargo = new List<CargoDefinition>
            {
                new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 0),
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("wheat", "cargo.wheat", CargoClass.Cargo, 6, 1),
                new CargoDefinition("iron_ore", "cargo.iron_ore", CargoClass.Cargo, 7, 1),
                new CargoDefinition("livestock", "cargo.livestock", CargoClass.Cargo, 8, 1),
                new CargoDefinition("wool", "cargo.wool", CargoClass.Cargo, 5, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2),
                new CargoDefinition("flour", "cargo.flour", CargoClass.Cargo, 12, 2),
                new CargoDefinition("iron", "cargo.iron", CargoClass.Cargo, 15, 2),
                new CargoDefinition("meat", "cargo.meat", CargoClass.Cargo, 18, 2),
                new CargoDefinition("fabric", "cargo.fabric", CargoClass.Cargo, 12, 2),
                new CargoDefinition("furniture", "cargo.furniture", CargoClass.Cargo, 40, 3),
                new CargoDefinition("bread", "cargo.bread", CargoClass.Cargo, 24, 3),
                new CargoDefinition("machinery", "cargo.machinery", CargoClass.Cargo, 45, 3),
                new CargoDefinition("canned_food", "cargo.canned_food", CargoClass.Cargo, 36, 3),
                new CargoDefinition("clothing", "cargo.clothing", CargoClass.Cargo, 30, 3),
            };
            return new ContentCatalog(cargo, Array.Empty<IndustryDefinition>(), TownNeedsSettings.CreatePlaceholder());
        }
    }
}
