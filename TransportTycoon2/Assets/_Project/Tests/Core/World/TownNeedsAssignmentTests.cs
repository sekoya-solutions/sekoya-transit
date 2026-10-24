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
            Assert.That(maxTier.Level1Count, Is.EqualTo(6));
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

            Assert.That(world.Towns, Has.Count.EqualTo(2));
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
