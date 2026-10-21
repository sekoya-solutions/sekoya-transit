#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class TileMapTests
    {
        [TestCase(MapSize.Small, 128)]
        [TestCase(MapSize.Medium, 256)]
        [TestCase(MapSize.Large, 512)]
        public void TileMap_CreatesSupportedSquareSizes(MapSize size, int sideLength)
        {
            var map = new TileMap(size);

            Assert.That(map.Size, Is.EqualTo(size));
            Assert.That(map.Width, Is.EqualTo(sideLength));
            Assert.That(map.Height, Is.EqualTo(sideLength));
            Assert.That(map.TileCount, Is.EqualTo(sideLength * sideLength));
            Assert.That(map.TerrainTypeIds.Length, Is.EqualTo(map.TileCount));
            Assert.That(map.ObjectIds.Length, Is.EqualTo(map.TileCount));
            Assert.That(map.OwnerIds.Length, Is.EqualTo(map.TileCount));
        }

        [Test]
        public void TileMap_RejectsUnsupportedSize()
        {
            Assert.That(
                () => new TileMap((MapSize)129),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void TileMap_MapsEveryTileIdToItsRowMajorCoordinateAndBack(MapSize size)
        {
            var map = new TileMap(size);

            for (int tileId = 0; tileId < map.TileCount; tileId++)
            {
                TileCoordinate coordinate = map.GetCoordinate(tileId);

                Assert.That(map.GetTileId(coordinate), Is.EqualTo(tileId));
                Assert.That(coordinate.X, Is.EqualTo(tileId % map.Width));
                Assert.That(coordinate.Y, Is.EqualTo(tileId / map.Width));
            }
        }

        [TestCase(MapSize.Small, 128)]
        [TestCase(MapSize.Medium, 256)]
        [TestCase(MapSize.Large, 512)]
        public void TileMap_UsesExpectedCornerAndRowBoundaryIds(MapSize size, int sideLength)
        {
            var map = new TileMap(size);

            Assert.That(map.GetTileId(new TileCoordinate(0, 0)), Is.Zero);
            Assert.That(map.GetTileId(new TileCoordinate(sideLength - 1, 0)), Is.EqualTo(sideLength - 1));
            Assert.That(map.GetTileId(new TileCoordinate(0, 1)), Is.EqualTo(sideLength));
            Assert.That(
                map.GetTileId(new TileCoordinate(sideLength - 1, sideLength - 1)),
                Is.EqualTo(map.TileCount - 1));
        }

        [Test]
        public void TileMap_InitializesEveryChannelWithItsDocumentedDefault()
        {
            var map = new TileMap(MapSize.Small);
            TileCoordinate first = new TileCoordinate(0, 0);
            TileCoordinate last = new TileCoordinate(map.Width - 1, map.Height - 1);

            Assert.That(map.GetTerrainTypeId(first), Is.EqualTo(TileMap.DefaultTerrainTypeId));
            Assert.That(map.GetObjectId(first), Is.EqualTo(TileMap.NoObjectId));
            Assert.That(map.GetOwnerId(first), Is.EqualTo(TileMap.UnownedId));
            Assert.That(map.GetTerrainTypeId(last), Is.EqualTo(TileMap.DefaultTerrainTypeId));
            Assert.That(map.GetObjectId(last), Is.EqualTo(TileMap.NoObjectId));
            Assert.That(map.GetOwnerId(last), Is.EqualTo(TileMap.UnownedId));
        }

        [Test]
        public void TileMap_StoresPerTileChannelsIndependentlyAndAllowsZeroIds()
        {
            var map = new TileMap(MapSize.Small);
            TileCoordinate left = new TileCoordinate(127, 0);
            TileCoordinate below = new TileCoordinate(0, 1);

            map.SetTerrainTypeId(left, 4);
            map.SetObjectId(left, 0);
            map.SetOwnerId(left, 0);
            map.SetTerrainTypeId(below, 7);
            map.SetObjectId(below, 21);
            map.SetOwnerId(below, 3);

            Assert.That(map.GetTerrainTypeId(left), Is.EqualTo(4));
            Assert.That(map.GetObjectId(left), Is.Zero);
            Assert.That(map.GetOwnerId(left), Is.Zero);
            Assert.That(map.GetTerrainTypeId(below), Is.EqualTo(7));
            Assert.That(map.GetObjectId(below), Is.EqualTo(21));
            Assert.That(map.GetOwnerId(below), Is.EqualTo(3));
        }

        [TestCase(-1, 0)]
        [TestCase(128, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 128)]
        public void GetTileId_RejectsOutOfBoundsCoordinates(int x, int y)
        {
            var map = new TileMap(MapSize.Small);

            Assert.That(
                () => map.GetTileId(new TileCoordinate(x, y)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-1)]
        [TestCase(16384)]
        public void GetCoordinate_RejectsOutOfBoundsTileIds(int tileId)
        {
            var map = new TileMap(MapSize.Small);

            Assert.That(
                () => map.GetCoordinate(tileId),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TileMap_RejectsInvalidChannelIds()
        {
            var map = new TileMap(MapSize.Small);
            var coordinate = new TileCoordinate(0, 0);

            Assert.That(
                () => map.SetTerrainTypeId(coordinate, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => map.SetObjectId(coordinate, -2),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => map.SetOwnerId(coordinate, -2),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
