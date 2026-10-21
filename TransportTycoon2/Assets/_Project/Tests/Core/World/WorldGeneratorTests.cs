#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.Tests.Simulation;
using TransportTycoon.Core.World;

#pragma warning disable CA1861 // Literal arrays keep each test scenario self-contained.
namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class WorldGeneratorTests
    {
        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void Generate_CreatesFlatMapAndUniqueTownAndIndustryObjectIds(MapSize mapSize)
        {
            GeneratedWorld world = WorldGenerator.Generate(CreateSettings(mapSize), 30001UL);

            Assert.That(world.Map.Size, Is.EqualTo(mapSize));
            Assert.That(world.TownAnchors.Count, Is.EqualTo(4));
            Assert.That(world.Industries.Count, Is.EqualTo(7));
            Assert.That(world.Map.TileCount, Is.EqualTo((int)mapSize * (int)mapSize));

            var occupiedTiles = new HashSet<TileCoordinate>();
            for (int townIndex = 0; townIndex < world.TownAnchors.Count; townIndex++)
            {
                TileCoordinate coordinate = world.TownAnchors[townIndex];
                Assert.That(occupiedTiles.Add(coordinate), Is.True);
                Assert.That(world.Map.GetTerrainTypeId(coordinate), Is.EqualTo(TileMap.DefaultTerrainTypeId));
                Assert.That(world.Map.GetOwnerId(coordinate), Is.EqualTo(TileMap.UnownedId));
                Assert.That(world.Map.GetObjectId(coordinate), Is.EqualTo(world.GetTownObjectId(townIndex)));
            }

            for (int industryIndex = 0; industryIndex < world.Industries.Count; industryIndex++)
            {
                TileCoordinate coordinate = world.Industries[industryIndex].Coordinate;
                Assert.That(occupiedTiles.Add(coordinate), Is.True);
                Assert.That(world.Map.GetTerrainTypeId(coordinate), Is.EqualTo(TileMap.DefaultTerrainTypeId));
                Assert.That(world.Map.GetOwnerId(coordinate), Is.EqualTo(TileMap.UnownedId));
                Assert.That(world.Map.GetObjectId(coordinate), Is.EqualTo(world.GetIndustryObjectId(industryIndex)));
            }

            for (int tileId = 0; tileId < world.Map.TileCount; tileId++)
            {
                TileCoordinate coordinate = world.Map.GetCoordinate(tileId);
                Assert.That(
                    world.Map.GetTerrainTypeId(coordinate),
                    Is.EqualTo(TileMap.DefaultTerrainTypeId));
                Assert.That(
                    world.Map.GetOwnerId(coordinate),
                    Is.EqualTo(TileMap.UnownedId));
                if (!occupiedTiles.Contains(coordinate))
                {
                    Assert.That(
                        world.Map.GetObjectId(coordinate),
                        Is.EqualTo(TileMap.NoObjectId));
                }
            }
        }

        [Test]
        public void Generate_HonorsTownAndIndustrySpacingAndProductionChains()
        {
            const int minimumTownDistance = 24;
            const int minimumIndustryDistance = 4;
            const int maximumProducerDistance = 60;
            GeneratedWorld world = WorldGenerator.Generate(
                CreateSettings(
                    MapSize.Small,
                    minimumTownDistance,
                    minimumIndustryDistance,
                    maximumProducerDistance),
                30002UL);

            AssertAllPairsRespectDistance(world.TownAnchors, minimumTownDistance);
            AssertIndustrySpacing(world.Industries, minimumIndustryDistance);
            AssertIndustryInputsHaveNearbyProducers(world.Industries, maximumProducerDistance);
        }

        [Test]
        public void Generate_IsRepeatableForEqualSettingsAndSeed()
        {
            WorldGenerationSettings settings = CreateSettings(MapSize.Small);

            GeneratedWorld first = WorldGenerator.Generate(settings, 30003UL);
            GeneratedWorld repeated = WorldGenerator.Generate(settings, 30003UL);
            GeneratedWorld differentSeed = WorldGenerator.Generate(settings, 30004UL);

            Assert.That(WorldGenerationSnapshot.Create(repeated), Is.EqualTo(WorldGenerationSnapshot.Create(first)));
            Assert.That(WorldGenerationSnapshot.Create(differentSeed), Is.Not.EqualTo(WorldGenerationSnapshot.Create(first)));
        }

        [TestCase(MapSize.Small, "607AF160C90D25BE753B6786F6FC040742759C7855747C26845D8F33FD37E2E3")]
        [TestCase(MapSize.Medium, "85072CBD8A881493624D022826F407685513606D240E77643F7A2556E3D6F772")]
        [TestCase(MapSize.Large, "5E56698E0F97219DFAB9DA9A0E686BC38218A6A66DE7EA3617DFE2379EDA76D3")]
        public void Generate_ProducesStableGoldenWorldHash(MapSize mapSize, string expectedHash)
        {
            GeneratedWorld world = WorldGenerator.Generate(
                CreateSettings(mapSize, maximumProducerDistance: (int)mapSize - 1),
                30005UL);
            string hash = DeterministicStateHasher.ToHexString(
                DeterministicStateHasher.ComputeSha256(WorldGenerationSnapshot.Create(world)));

            Assert.That(hash, Is.EqualTo(expectedHash));
        }

        [Test]
        public void Generate_RejectsDefaultSettings()
        {
            Assert.That(
                () => WorldGenerator.Generate(default, 30006UL),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Generate_ThrowsRatherThanReturningPartialWorldWhenPlacementCannotFit()
        {
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0),
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, (int)MapSize.Small * (int)MapSize.Small, 1),
                1,
                rules);

            Assert.That(
                () => WorldGenerator.Generate(settings, 30007UL),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static WorldGenerationSettings CreateSettings(
            MapSize mapSize,
            int minimumTownDistance = 24,
            int minimumIndustryDistance = 4,
            int maximumProducerDistance = 60)
        {
            return new WorldGenerationSettings(
                new TownPlacementSettings(mapSize, 4, minimumTownDistance),
                minimumIndustryDistance,
                CreateIndustryRules(maximumProducerDistance));
        }

        private static IndustryPlacementRule[] CreateIndustryRules(int maximumProducerDistance)
        {
            return new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0),
                new IndustryPlacementRule("mine", 1, Array.Empty<string>(), new[] { "metal" }, 2, 0),
                new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 2, maximumProducerDistance),
                new IndustryPlacementRule(
                    "furniture_factory",
                    3,
                    new[] { "planks", "metal" },
                    new[] { "furniture" },
                    1,
                    maximumProducerDistance),
            };
        }

        private static void AssertAllPairsRespectDistance(
            IReadOnlyList<TileCoordinate> coordinates,
            int minimumDistance)
        {
            for (int leftIndex = 0; leftIndex < coordinates.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < coordinates.Count; rightIndex++)
                {
                    Assert.That(
                        ChebyshevDistance(coordinates[leftIndex], coordinates[rightIndex]),
                        Is.GreaterThanOrEqualTo(minimumDistance));
                }
            }
        }

        private static void AssertIndustryInputsHaveNearbyProducers(
            IReadOnlyList<PlacedIndustry> industries,
            int maximumProducerDistance)
        {
            foreach (PlacedIndustry consumer in industries)
            {
                if (consumer.IndustryId == "sawmill")
                {
                    AssertHasNearbyProducer(industries, consumer.Coordinate, "forest", maximumProducerDistance);
                }
                else if (consumer.IndustryId == "furniture_factory")
                {
                    AssertHasNearbyProducer(industries, consumer.Coordinate, "sawmill", maximumProducerDistance);
                    AssertHasNearbyProducer(industries, consumer.Coordinate, "mine", maximumProducerDistance);
                }
            }
        }

        private static void AssertHasNearbyProducer(
            IReadOnlyList<PlacedIndustry> industries,
            TileCoordinate consumer,
            string producerIndustryId,
            int maximumProducerDistance)
        {
            foreach (PlacedIndustry producer in industries)
            {
                if (producer.IndustryId == producerIndustryId &&
                    ChebyshevDistance(consumer, producer.Coordinate) <= maximumProducerDistance)
                {
                    return;
                }
            }

            Assert.Fail($"No producer of '{producerIndustryId}' was within range of ({consumer.X}, {consumer.Y}).");
        }

        private static void AssertIndustrySpacing(IReadOnlyList<PlacedIndustry> industries, int minimumDistance)
        {
            for (int leftIndex = 0; leftIndex < industries.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < industries.Count; rightIndex++)
                {
                    Assert.That(
                        ChebyshevDistance(industries[leftIndex].Coordinate, industries[rightIndex].Coordinate),
                        Is.GreaterThanOrEqualTo(minimumDistance));
                }
            }
        }

        private static int ChebyshevDistance(TileCoordinate left, TileCoordinate right)
        {
            return Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y));
        }
    }
}
