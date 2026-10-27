#nullable enable
using System;
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
            Assert.That(world.Towns.Count, Is.EqualTo(expected.TownCount));
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
        public void Create_Small_ReproducesTheFormerDemoWorld()
        {
            ContentCatalog catalog = CreateCatalog();
            var legacy = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 24),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 4, 0),
                    new IndustryPlacementRule("iron_mine", 1, Array.Empty<string>(), new[] { "iron_ore" }, 3, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 60),
                    new IndustryPlacementRule(
                        "furniture_factory",
                        3,
                        new[] { "planks" },
                        new[] { "furniture" },
                        1,
                        60),
                });

            byte[] expected = WorldGenerationSnapshot.Create(
                WorldGenerator.Generate(legacy, 37001UL, catalog.TownNeeds, catalog));
            byte[] actual = WorldGenerationSnapshot.Create(
                NewGame.Create(new NewGameRequest(37001UL, MapSize.Small), catalog));

            Assert.That(actual, Is.EqualTo(expected));
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
            return new ContentCatalog(
                new[]
                {
                    new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 0),
                    new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                    new CargoDefinition("iron_ore", "cargo.iron_ore", CargoClass.Cargo, 9, 1),
                    new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2),
                    new CargoDefinition("furniture", "cargo.furniture", CargoClass.Cargo, 30, 3),
                    new CargoDefinition("wheat", "cargo.wheat", CargoClass.Cargo, 8, 1),
                    new CargoDefinition("flour", "cargo.flour", CargoClass.Cargo, 14, 2),
                    new CargoDefinition("iron", "cargo.iron", CargoClass.Cargo, 16, 2),
                    new CargoDefinition("bread", "cargo.bread", CargoClass.Cargo, 30, 3),
                    new CargoDefinition("machinery", "cargo.machinery", CargoClass.Cargo, 40, 3),
                },
                new[]
                {
                    new IndustryDefinition(
                        "forest",
                        "industry.forest",
                        1,
                        Array.Empty<CargoAmountDefinition>(),
                        new[] { new CargoAmountDefinition("wood", 4) }),
                    new IndustryDefinition(
                        "iron_mine",
                        "industry.iron_mine",
                        1,
                        Array.Empty<CargoAmountDefinition>(),
                        new[] { new CargoAmountDefinition("iron_ore", 4) }),
                    new IndustryDefinition(
                        "sawmill",
                        "industry.sawmill",
                        2,
                        new[] { new CargoAmountDefinition("wood", 2) },
                        new[] { new CargoAmountDefinition("planks", 1) }),
                    new IndustryDefinition(
                        "furniture_factory",
                        "industry.furniture_factory",
                        3,
                        new[] { new CargoAmountDefinition("planks", 2) },
                        new[] { new CargoAmountDefinition("furniture", 1) }),
                });
        }
    }
}
