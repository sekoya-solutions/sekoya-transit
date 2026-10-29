#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Content
{
    [TestFixture]
    public sealed class WorldGenerationDataTests
    {
        [Test]
        public void Placeholder_DefinesEverySize_WithGrowingCounts()
        {
            WorldGenerationData data = WorldGenerationData.CreatePlaceholder();

            WorldGenerationSizeDefinition small = data.Get(MapSize.Small);
            WorldGenerationSizeDefinition medium = data.Get(MapSize.Medium);
            WorldGenerationSizeDefinition large = data.Get(MapSize.Large);

            Assert.That(small.TownCount, Is.EqualTo(15));
            Assert.That(small.TownSpacing, Is.EqualTo(20));
            Assert.That(small.IndustrySpacing, Is.EqualTo(10));
            Assert.That(medium.TownCount, Is.GreaterThan(small.TownCount));
            Assert.That(large.TownCount, Is.GreaterThan(medium.TownCount));
        }

        [Test]
        public void Constructor_MissingSize_Throws()
        {
            var sizes = new[] { CreateSize(MapSize.Small), CreateSize(MapSize.Medium) };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new WorldGenerationData(sizes))!;

            StringAssert.Contains("Large", exception.Message);
        }

        [Test]
        public void Constructor_DuplicateSize_Throws()
        {
            var sizes = new[]
            {
                CreateSize(MapSize.Small),
                CreateSize(MapSize.Small),
                CreateSize(MapSize.Medium),
                CreateSize(MapSize.Large),
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new WorldGenerationData(sizes))!;

            StringAssert.Contains("more than once", exception.Message);
        }

        [Test]
        public void SizeDefinition_DuplicateIndustry_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new WorldGenerationSizeDefinition(
                    MapSize.Small,
                    2,
                    16,
                    4,
                    new[]
                    {
                        new WorldGenerationIndustryEntry("forest", 1, 0),
                        new WorldGenerationIndustryEntry("forest", 2, 0),
                    }));
        }

        [TestCase(-1, 16, 4)]
        [TestCase(2, 0, 4)]
        [TestCase(2, 16, 2)]
        public void SizeDefinition_BadValues_Throw(int townCount, int townSpacing, int industrySpacing)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WorldGenerationSizeDefinition(
                    MapSize.Small,
                    townCount,
                    townSpacing,
                    industrySpacing,
                    new[] { new WorldGenerationIndustryEntry("forest", 1, 0) }));
        }

        [TestCase(0, 0)]
        [TestCase(1, -1)]
        public void IndustryEntry_BadValues_Throw(int count, int distance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGenerationIndustryEntry("forest", count, distance));
        }

        [Test]
        public void CreateSettings_PrimaryWithProducerDistance_Throws()
        {
            var data = new WorldGenerationData(
                new[]
                {
                    CreateSize(MapSize.Small, new WorldGenerationIndustryEntry("forest", 1, 5)),
                    CreateSize(MapSize.Medium),
                    CreateSize(MapSize.Large),
                });
            IndustryDefinition forest = new IndustryDefinition(
                "forest",
                "industry.forest",
                1,
                Array.Empty<CargoAmountDefinition>(),
                new[] { new CargoAmountDefinition("wood", 4) });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => data.CreateSettings(MapSize.Small, new[] { forest }));
        }

        private static WorldGenerationSizeDefinition CreateSize(MapSize mapSize, WorldGenerationIndustryEntry? entry = null)
        {
            return new WorldGenerationSizeDefinition(
                mapSize,
                2,
                16,
                4,
                new[] { entry ?? new WorldGenerationIndustryEntry("forest", 1, 0) });
        }
    }
}
