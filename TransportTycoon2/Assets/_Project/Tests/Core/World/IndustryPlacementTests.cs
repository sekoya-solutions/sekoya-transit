#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.World;

#pragma warning disable CA1861 // Literal arrays keep each test scenario self-contained.
namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class IndustryPlacementTests
    {
        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void Generate_ReturnsConfiguredIndustryCountsWithinMapBounds(MapSize mapSize)
        {
            int sideLength = (int)mapSize;
            var settings = CreateSettings(mapSize, 3, sideLength - 1);

            PlacedIndustry[] placements = IndustryPlacement.Generate(settings, 29001UL);

            Assert.That(placements.Length, Is.EqualTo(7));
            foreach (PlacedIndustry placement in placements)
            {
                Assert.That(placement.Coordinate.X, Is.InRange(0, sideLength - 1));
                Assert.That(placement.Coordinate.Y, Is.InRange(0, sideLength - 1));
            }

            Assert.That(CountIndustry(placements, "forest"), Is.EqualTo(2));
            Assert.That(CountIndustry(placements, "mine"), Is.EqualTo(2));
            Assert.That(CountIndustry(placements, "sawmill"), Is.EqualTo(2));
            Assert.That(CountIndustry(placements, "furniture_factory"), Is.EqualTo(1));
        }

        [Test]
        public void Generate_IsRepeatableAndSeeded()
        {
            var settings = CreateSettings(MapSize.Small, 3, 40);

            PlacedIndustry[] first = IndustryPlacement.Generate(settings, 29002UL);
            PlacedIndustry[] repeated = IndustryPlacement.Generate(settings, 29002UL);
            PlacedIndustry[] differentSeed = IndustryPlacement.Generate(settings, 29003UL);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(differentSeed, Is.Not.EqualTo(first));
        }

        [Test]
        public void Generate_HonorsMinimumSpacingAndProducerDistance()
        {
            const int minimumDistance = 4;
            const int maximumProducerDistance = 60;
            var settings = CreateSettings(MapSize.Small, minimumDistance, maximumProducerDistance);
            PlacedIndustry[] placements = IndustryPlacement.Generate(settings, 29004UL);

            for (int leftIndex = 0; leftIndex < placements.Length; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < placements.Length; rightIndex++)
                {
                    Assert.That(
                        ChebyshevDistance(placements[leftIndex].Coordinate, placements[rightIndex].Coordinate),
                        Is.GreaterThanOrEqualTo(minimumDistance));
                }
            }
            AssertInputHasNearbyProducer(placements, "sawmill", "wood", maximumProducerDistance);
            AssertInputHasNearbyProducer(placements, "furniture_factory", "planks", maximumProducerDistance);
            AssertInputHasNearbyProducer(placements, "furniture_factory", "metal", maximumProducerDistance);
        }

        [Test]
        public void Generate_OrdersByLevelIndustryIdAndInstanceIndex()
        {
            var settings = CreateSettings(MapSize.Small, 2, 30);

            PlacedIndustry[] placements = IndustryPlacement.Generate(settings, 29005UL);

            Assert.That(placements[0].IndustryId, Is.EqualTo("forest"));
            Assert.That(placements[0].InstanceIndex, Is.EqualTo(0));
            Assert.That(placements[1].IndustryId, Is.EqualTo("forest"));
            Assert.That(placements[1].InstanceIndex, Is.EqualTo(1));
            Assert.That(placements[2].IndustryId, Is.EqualTo("mine"));
            Assert.That(placements[4].IndustryId, Is.EqualTo("sawmill"));
            Assert.That(placements[6].IndustryId, Is.EqualTo("furniture_factory"));
        }

        [Test]
        public void Generate_ProducesStableKnownPlacementOrder()
        {
            var settings = CreateSettings(MapSize.Small, 4, 60);
            PlacedIndustry[] placements = IndustryPlacement.Generate(settings, 29009UL);
            string placementVector = string.Join(
                ";",
                Array.ConvertAll(
                    placements,
                    placement => $"{placement.IndustryId}:{placement.InstanceIndex}:{placement.Coordinate.X},{placement.Coordinate.Y}"));

            Assert.That(
                placementVector,
                Is.EqualTo(
                    "forest:0:113,93;forest:1:119,91;mine:0:92,91;mine:1:8,99;" +
                    "sawmill:0:108,71;sawmill:1:124,114;furniture_factory:0:117,40"));
        }

        [Test]
        public void Generate_ThrowsWhenNoProducerConsumerArrangementExists()
        {
            const int sideLength = (int)MapSize.Small;
            var occupiedTiles = new List<TileCoordinate>();
            for (int y = 0; y < sideLength; y++)
            {
                for (int x = 0; x < sideLength; x++)
                {
                    if ((x == 0 && y == 0) || (x == sideLength - 1 && y == sideLength - 1))
                    {
                        continue;
                    }

                    occupiedTiles.Add(new TileCoordinate(x, y));
                }
            }

            var settings = new IndustryPlacementSettings(
                MapSize.Small,
                1,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 1),
                },
                occupiedTiles);

            Assert.That(
                () => IndustryPlacement.Generate(settings, 29006UL),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void IndustryPlacementRule_RejectsPrimaryInputs()
        {
            Assert.That(
                () => new IndustryPlacementRule("forest", 1, new[] { "wood" }, new[] { "planks" }, 1, 0),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IndustryPlacementRule_RejectsDuplicateCargoIds()
        {
            Assert.That(
                () => new IndustryPlacementRule(
                    "sawmill",
                    2,
                    new[] { "wood", "wood" },
                    new[] { "planks" },
                    1,
                    10),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsUnsupportedMapSize()
        {
            Assert.That(
                () => new IndustryPlacementSettings((MapSize)129, 1, CreateRules(20)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsNonpositiveMinimumDistance()
        {
            Assert.That(
                () => new IndustryPlacementSettings(MapSize.Small, 0, CreateRules(20)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsEmptyRuleSet()
        {
            Assert.That(
                () => new IndustryPlacementSettings(MapSize.Small, 1, Array.Empty<IndustryPlacementRule>()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsMissingLowerLevelProducer()
        {
            var rules = new[]
            {
                new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 10),
            };

            Assert.That(
                () => new IndustryPlacementSettings(MapSize.Small, 1, rules),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsProducerDistanceBelowMinimumSpacing()
        {
            Assert.That(
                () => new IndustryPlacementSettings(
                    MapSize.Small,
                    5,
                    new[]
                    {
                        new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0),
                        new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 4),
                    }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsDuplicateIndustryIds()
        {
            Assert.That(
                () => new IndustryPlacementSettings(
                    MapSize.Small,
                    1,
                    new[]
                    {
                        new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0),
                        new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "lumber" }, 1, 0),
                    }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsCountAboveSpacingCapacity()
        {
            Assert.That(
                () => new IndustryPlacementSettings(
                    MapSize.Small,
                    128,
                    new[]
                    {
                        new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0),
                    }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Generate_DoesNotOverlapExistingOccupiedTiles()
        {
            var settings = new IndustryPlacementSettings(
                MapSize.Small,
                2,
                CreateRules(40),
                new[] { new TileCoordinate(0, 0), new TileCoordinate(64, 64) });

            PlacedIndustry[] placements = IndustryPlacement.Generate(settings, 29008UL);

            foreach (PlacedIndustry placement in placements)
            {
                Assert.That(placement.Coordinate, Is.Not.EqualTo(new TileCoordinate(0, 0)));
                Assert.That(placement.Coordinate, Is.Not.EqualTo(new TileCoordinate(64, 64)));
            }
        }

        [Test]
        public void IndustryPlacementSettings_RejectsOccupiedTileOutsideMap()
        {
            Assert.That(
                () => new IndustryPlacementSettings(
                    MapSize.Small,
                    1,
                    CreateRules(20),
                    new[] { new TileCoordinate(128, 0) }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void IndustryPlacementSettings_RejectsDuplicateOccupiedTiles()
        {
            Assert.That(
                () => new IndustryPlacementSettings(
                    MapSize.Small,
                    1,
                    CreateRules(20),
                    new[] { new TileCoordinate(10, 20), new TileCoordinate(10, 20) }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_RejectsDefaultUninitializedSettings()
        {
            Assert.That(
                () => IndustryPlacement.Generate(default, 29007UL),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static IndustryPlacementSettings CreateSettings(
            MapSize mapSize,
            int minimumDistance,
            int maximumProducerDistance)
        {
            return new IndustryPlacementSettings(
                mapSize,
                minimumDistance,
                CreateRules(maximumProducerDistance));
        }

        private static IndustryPlacementRule[] CreateRules(int maximumProducerDistance)
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

        private static int CountIndustry(PlacedIndustry[] placements, string industryId)
        {
            int count = 0;
            foreach (PlacedIndustry placement in placements)
            {
                if (placement.IndustryId == industryId)
                {
                    count++;
                }
            }

            return count;
        }

        private static int ChebyshevDistance(TileCoordinate left, TileCoordinate right)
        {
            return Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y));
        }

        private static void AssertInputHasNearbyProducer(
            PlacedIndustry[] placements,
            string consumerIndustryId,
            string inputCargoId,
            int maximumDistance)
        {
            foreach (PlacedIndustry consumer in placements)
            {
                if (consumer.IndustryId != consumerIndustryId)
                {
                    continue;
                }

                bool hasNearbyProducer = false;
                foreach (PlacedIndustry producer in placements)
                {
                    if ((producer.IndustryId == "forest" && inputCargoId == "wood") ||
                        (producer.IndustryId == "sawmill" && inputCargoId == "planks") ||
                        (producer.IndustryId == "mine" && inputCargoId == "metal"))
                    {
                        if (ChebyshevDistance(consumer.Coordinate, producer.Coordinate) <= maximumDistance)
                        {
                            hasNearbyProducer = true;
                            break;
                        }
                    }
                }

                Assert.That(hasNearbyProducer, Is.True, $"{consumerIndustryId} has no nearby producer of {inputCargoId}.");
            }
        }
    }
}
