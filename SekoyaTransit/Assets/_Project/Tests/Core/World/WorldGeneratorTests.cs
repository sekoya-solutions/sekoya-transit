#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.Tests.Simulation;
using SekoyaTransit.Core.World;

#pragma warning disable CA1861 // Literal arrays keep each test scenario self-contained.
namespace SekoyaTransit.Core.Tests.World
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
                AssertFootprint(world.Map, coordinate, world.GetTownObjectId(townIndex), SiteFootprint.TownSize, occupiedTiles);
            }

            for (int industryIndex = 0; industryIndex < world.Industries.Count; industryIndex++)
            {
                TileCoordinate coordinate = world.Industries[industryIndex].Coordinate;
                AssertFootprint(world.Map, coordinate, world.GetIndustryObjectId(industryIndex), SiteFootprint.IndustrySize, occupiedTiles);
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
            const int minimumIndustryDistance = 10;
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
            foreach (TileCoordinate town in world.TownAnchors)
            {
                foreach (PlacedIndustry industry in world.Industries)
                {
                    Assert.That(ChebyshevDistance(town, industry.Coordinate), Is.GreaterThanOrEqualTo(minimumIndustryDistance));
                }
            }
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

        [TestCase(MapSize.Small, "1C3FF21955AAE01CBDA62F3003F9C44F41D14EF79FC83C8A89E8C1F69FC4D975")]
        [TestCase(MapSize.Medium, "756BFBB86705A014BE4AF9DAA3CF00BA8CF7C3CD1B7C926657DA82F0EDF88012")]
        [TestCase(MapSize.Large, "52648D407922996C94964B1FA7E1759C8390D42AD2071220DF77C468D4D086DD")]
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
                new TownPlacementSettings(MapSize.Small, 125 * 125, 1),
                StationAdjacency.MinimumSiteSeparationChebyshev,
                rules);

            Assert.That(
                () => WorldGenerator.Generate(settings, 30007UL),
                Throws.TypeOf<InvalidOperationException>());
        }

        private static void AssertFootprint(
            TileMap map,
            TileCoordinate anchor,
            int expectedObjectId,
            int size,
            HashSet<TileCoordinate> occupiedTiles)
        {
            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    var tile = new TileCoordinate(anchor.X + dx, anchor.Y + dy);
                    Assert.That(occupiedTiles.Add(tile), Is.True);
                    Assert.That(map.GetTerrainTypeId(tile), Is.EqualTo(TileMap.DefaultTerrainTypeId));
                    Assert.That(map.GetOwnerId(tile), Is.EqualTo(TileMap.UnownedId));
                    Assert.That(map.GetObjectId(tile), Is.EqualTo(expectedObjectId));
                }
            }
        }

        private static WorldGenerationSettings CreateSettings(
            MapSize mapSize,
            int minimumTownDistance = 24,
            int minimumIndustryDistance = 5,
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
