#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class TownPlacementTests
    {
        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void Generate_ReturnsRequestedCountWithinMapBounds(MapSize mapSize)
        {
            var settings = new TownPlacementSettings(mapSize, 12, 8);
            int sideLength = (int)mapSize;

            TileCoordinate[] towns = TownPlacement.Generate(settings, 28001UL);

            Assert.That(towns.Length, Is.EqualTo(settings.TownCount));
            foreach (TileCoordinate town in towns)
            {
                Assert.That(town.X, Is.InRange(0, sideLength - 1));
                Assert.That(town.Y, Is.InRange(0, sideLength - 1));
            }
        }

        [Test]
        public void Generate_IsRepeatableAndSeeded()
        {
            var settings = new TownPlacementSettings(MapSize.Small, 24, 6);

            TileCoordinate[] first = TownPlacement.Generate(settings, 28002UL);
            TileCoordinate[] repeated = TownPlacement.Generate(settings, 28002UL);
            TileCoordinate[] differentSeed = TownPlacement.Generate(settings, 28003UL);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(differentSeed, Is.Not.EqualTo(first));
        }

        [Test]
        public void Generate_ProducesStableKnownPlacementOrder()
        {
            var settings = new TownPlacementSettings(MapSize.Small, 8, 1);
            TileCoordinate[] towns = TownPlacement.Generate(settings, 28008UL);

            TileCoordinate[] expected =
            {
                new TileCoordinate(26, 67),
                new TileCoordinate(58, 22),
                new TileCoordinate(24, 82),
                new TileCoordinate(36, 94),
                new TileCoordinate(37, 13),
                new TileCoordinate(68, 11),
                new TileCoordinate(120, 91),
                new TileCoordinate(27, 109),
            };

            Assert.That(towns, Is.EqualTo(expected));
        }

        [Test]
        public void Generate_HonorsMinimumChebyshevDistance()
        {
            const int minimumDistance = 13;
            var settings = new TownPlacementSettings(MapSize.Small, 30, minimumDistance);
            TileCoordinate[] towns = TownPlacement.Generate(settings, 28004UL);

            for (int leftIndex = 0; leftIndex < towns.Length; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < towns.Length; rightIndex++)
                {
                    int distanceX = Math.Abs(towns[leftIndex].X - towns[rightIndex].X);
                    int distanceY = Math.Abs(towns[leftIndex].Y - towns[rightIndex].Y);

                    Assert.That(Math.Max(distanceX, distanceY), Is.GreaterThanOrEqualTo(minimumDistance));
                }
            }
        }

        [Test]
        public void Generate_ReturnsEmptyArrayWhenNoTownsRequested()
        {
            var settings = new TownPlacementSettings(MapSize.Small, 0, 4);

            Assert.That(TownPlacement.Generate(settings, 28005UL), Is.Empty);
        }

        [Test]
        public void Generate_CanPlaceEveryTileWhenMinimumDistanceIsOne()
        {
            var settings = new TownPlacementSettings(MapSize.Small, 128 * 128, 1);

            TileCoordinate[] towns = TownPlacement.Generate(settings, 28006UL);

            Assert.That(towns.Length, Is.EqualTo(128 * 128));
        }

        [Test]
        public void TownPlacementSettings_RejectsUnsupportedMapSize()
        {
            Assert.That(
                () => new TownPlacementSettings((MapSize)129, 1, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TownPlacementSettings_RejectsNegativeTownCount()
        {
            Assert.That(
                () => new TownPlacementSettings(MapSize.Small, -1, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TownPlacementSettings_RejectsNonpositiveMinimumDistance()
        {
            Assert.That(
                () => new TownPlacementSettings(MapSize.Small, 1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TownPlacementSettings_RejectsTownCountAboveSpacingCapacity()
        {
            Assert.That(
                () => new TownPlacementSettings(MapSize.Small, 2, 128),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Generate_RejectsDefaultUninitializedSettings()
        {
            Assert.That(
                () => TownPlacement.Generate(default, 28007UL),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
