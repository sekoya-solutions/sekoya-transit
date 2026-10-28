#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

#pragma warning disable CA1861 // Literal arrays keep each test scenario self-contained.
namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class NewGameTests
    {
        [Test]
        public void Create_SameRequest_GivesIdenticalWorld()
        {
            ContentCatalog catalog = CreateCatalog();
            var request = new NewGameRequest(777UL, MapSize.Small);

            byte[] first = WorldGenerationSnapshot.Create(NewGame.Create(request, catalog));
            byte[] second = WorldGenerationSnapshot.Create(NewGame.Create(request, catalog));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Create_DifferentSeeds_GiveDifferentWorlds()
        {
            ContentCatalog catalog = CreateCatalog();

            byte[] first = WorldGenerationSnapshot.Create(NewGame.Create(new NewGameRequest(1UL, MapSize.Small), catalog));
            byte[] second = WorldGenerationSnapshot.Create(NewGame.Create(new NewGameRequest(2UL, MapSize.Small), catalog));

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void Create_EachSize_UsesItsOwnWidthAndCounts(MapSize mapSize)
        {
            ContentCatalog catalog = CreateCatalog();
            WorldGenerationSizeDefinition expected = catalog.WorldGeneration.Get(mapSize);
            int expectedIndustries = 0;
            for (int index = 0; index < expected.Industries.Count; index++)
            {
                expectedIndustries += expected.Industries[index].Count;
            }

            GeneratedWorld world = NewGame.Create(new NewGameRequest(31UL, mapSize), catalog);

            Assert.That(world.Map.Width, Is.EqualTo((int)mapSize));
            Assert.That(
                world.Towns.Count,
                Is.EqualTo(WorldGenerator.ResolveTownCount(expected.TownCount, catalog.TownNeeds, catalog)));
            Assert.That(world.Industries.Count, Is.EqualTo(expectedIndustries));
            Assert.That(world.MapSeed, Is.EqualTo(31UL));
        }

        [Test]
        public void Create_LargerSizes_PlaceMoreTownsAndIndustries()
        {
            ContentCatalog catalog = CreateCatalog();

            GeneratedWorld small = NewGame.Create(new NewGameRequest(5UL, MapSize.Small), catalog);
            GeneratedWorld medium = NewGame.Create(new NewGameRequest(5UL, MapSize.Medium), catalog);
            GeneratedWorld large = NewGame.Create(new NewGameRequest(5UL, MapSize.Large), catalog);

            Assert.That(medium.Towns.Count, Is.GreaterThan(small.Towns.Count));
            Assert.That(large.Towns.Count, Is.GreaterThan(medium.Towns.Count));
            Assert.That(medium.Industries.Count, Is.GreaterThan(small.Industries.Count));
            Assert.That(large.Industries.Count, Is.GreaterThan(medium.Industries.Count));
        }

        [Test]
        public void Create_Small_MatchesGeneratorWithShippedSettings()
        {
            ContentCatalog catalog = CreateCatalog();
            WorldGenerationSettings settings = catalog.WorldGeneration.CreateSettings(MapSize.Small, catalog.Industries);

            byte[] expected = WorldGenerationSnapshot.Create(
                WorldGenerator.Generate(settings, 37001UL, catalog.TownNeeds, catalog));
            byte[] actual = WorldGenerationSnapshot.Create(
                NewGame.Create(new NewGameRequest(37001UL, MapSize.Small), catalog));

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(MapSize.Small, 3, 2, 1)]
        [TestCase(MapSize.Medium, 6, 4, 2)]
        [TestCase(MapSize.Large, 12, 8, 4)]
        public void Create_EachSize_PlacesLevelCountsAndPlansEveryCargoInThreeTowns(
            MapSize mapSize,
            int level1Count,
            int level2Count,
            int level3Count)
        {
            ContentCatalog catalog = CreateCatalog();
            int[] expectedPerLevel = { 0, level1Count, level2Count, level3Count };
            for (ulong seed = 1UL; seed <= 5UL; seed++)
            {
                GeneratedWorld world = NewGame.Create(new NewGameRequest(seed, mapSize), catalog);

                foreach (IndustryDefinition definition in catalog.Industries)
                {
                    int placed = world.Industries.Count(industry => industry.IndustryId == definition.Id);
                    Assert.That(placed, Is.EqualTo(expectedPerLevel[definition.Level]), $"{mapSize} {seed} {definition.Id}");
                }

                foreach (CargoDefinition cargo in catalog.Cargo.Where(cargo => cargo.Class == CargoClass.Cargo))
                {
                    int planningTowns = world.Towns.Count(town => town.NeedPlan.Contains(cargo.Id));
                    Assert.That(planningTowns, Is.GreaterThanOrEqualTo(3), $"{mapSize} {seed} {cargo.Id}");
                }

                Assert.That(world.Towns.Select(town => town.Name).Distinct().Count(), Is.EqualTo(world.Towns.Count));
                foreach (Town town in world.Towns)
                {
                    foreach (TownNeed need in town.Needs)
                    {
                        Assert.That(catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo), Is.True);
                        Assert.That(cargo!.ManufacturingLevel, Is.EqualTo(1), "towns start with level 1 needs only");
                    }
                }
            }
        }

        [TestCase(MapSize.Small, 0UL)]
        [TestCase(MapSize.Medium, 123456789UL)]
        [TestCase(MapSize.Large, ulong.MaxValue)]
        public void SaveSnapshot_RoundTripKeepsSeedAndMapSize(MapSize mapSize, ulong seed)
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = NewGame.Create(new NewGameRequest(seed, mapSize), catalog);

            SaveSnapshot snapshot = SaveSnapshot.Capture(world.MapSeed, Tick.Zero, world);

            Assert.That(snapshot.MapSeed, Is.EqualTo(seed));
            Assert.That(snapshot.MapSize, Is.EqualTo(mapSize));
            Assert.That(snapshot.TerrainTypeIds.Length, Is.EqualTo((int)mapSize * (int)mapSize));
        }

        [Test]
        public void Create_UnknownIndustryInWorldGeneration_Throws()
        {
            var sizes = new[]
            {
                CreateSize(MapSize.Small, "mystery"),
                CreateSize(MapSize.Medium, "forest"),
                CreateSize(MapSize.Large, "forest"),
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new ContentCatalog(
                    CreateCatalog().Cargo,
                    CreateCatalog().Industries,
                    worldGeneration: new WorldGenerationData(sizes)))!;

            StringAssert.Contains("mystery", exception.Message);
        }

        [Test]
        public void NewGameRequest_UnsupportedSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NewGameRequest(1UL, (MapSize)64));
        }

        private static WorldGenerationSizeDefinition CreateSize(MapSize mapSize, string industryId)
        {
            return new WorldGenerationSizeDefinition(
                mapSize,
                2,
                16,
                4,
                new[] { new WorldGenerationIndustryEntry(industryId, 1, 0) });
        }

        private static ContentCatalog CreateCatalog()
        {
            // The five V1 chains (raw, processed, final), mirroring the shipped content (Issue #193).
            string[][] chains =
            {
                new[] { "forest", "wood", "sawmill", "planks", "furniture_factory", "furniture" },
                new[] { "wheat_farm", "wheat", "flour_mill", "flour", "bakery", "bread" },
                new[] { "iron_mine", "iron_ore", "iron_foundry", "iron", "machine_shop", "machinery" },
                new[] { "livestock_ranch", "livestock", "butcher", "meat", "cannery", "canned_food" },
                new[] { "sheep_farm", "wool", "textile_mill", "fabric", "clothing_workshop", "clothing" },
            };
            var cargo = new List<CargoDefinition>
            {
                new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 0),
            };
            var industries = new List<IndustryDefinition>();
            foreach (string[] chain in chains)
            {
                for (int level = 1; level <= 3; level++)
                {
                    string industryId = chain[(level - 1) * 2];
                    string outputId = chain[((level - 1) * 2) + 1];
                    cargo.Add(new CargoDefinition(outputId, "cargo." + outputId, CargoClass.Cargo, 8 * level, level));
                    CargoAmountDefinition[] inputs = level == 1
                        ? Array.Empty<CargoAmountDefinition>()
                        : new[] { new CargoAmountDefinition(chain[((level - 2) * 2) + 1], 2) };
                    industries.Add(
                        new IndustryDefinition(
                            industryId,
                            "industry." + industryId,
                            level,
                            inputs,
                            new[] { new CargoAmountDefinition(outputId, level == 1 ? 4 : 1) }));
                }
            }

            TownNeedsSettings placeholder = TownNeedsSettings.CreatePlaceholder();
            var townNeeds = new TownNeedsSettings(
                placeholder.MinStartingPopulation,
                placeholder.MaxStartingPopulation,
                placeholder.MaxPopulation,
                placeholder.Tiers,
                names: Enumerable.Range(1, 40).Select(index => "Capital " + index).ToArray());
            return new ContentCatalog(cargo, industries, townNeeds);
        }
    }
}
