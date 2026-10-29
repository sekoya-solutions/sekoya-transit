#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class RailPlacementTests
    {
        [Test]
        public void CanBuild_EmptyInBoundsTile_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(10, 12);

            Assert.That(RailPlacement.CanBuild(map, tile), Is.True);
            Assert.That(RailPlacement.CanDemolish(map, tile), Is.False);
        }

        [Test]
        public void CanBuild_OccupiedOrExistingRail_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var occupied = new TileCoordinate(1, 1);
            var rail = new TileCoordinate(2, 2);
            map.SetObjectId(occupied, 0);
            map.SetRailTypeId(rail, TileMap.BasicRailTypeId);

            Assert.That(RailPlacement.CanBuild(map, occupied), Is.False);
            Assert.That(RailPlacement.CanBuild(map, rail), Is.False);
            Assert.That(RailPlacement.CanDemolish(map, rail), Is.True);
        }

        [Test]
        public void CanBuild_RoadPresentWithoutRail_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(4, 4);
            map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);

            Assert.That(RailPlacement.CanBuild(map, tile), Is.True);
        }

        [Test]
        public void CanBuild_OutOfBounds_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);

            Assert.That(RailPlacement.CanBuild(map, new TileCoordinate(-1, 0)), Is.False);
            Assert.That(RailPlacement.CanDemolish(map, new TileCoordinate(128, 0)), Is.False);
        }

        [Test]
        public void CanBuild_RoadOrRailStationOccupied_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var roadStation = new TileCoordinate(4, 4);
            var railStation = new TileCoordinate(5, 5);
            map.SetStationId(roadStation, 1);
            map.SetRailStationId(railStation, 1);

            Assert.That(RailPlacement.CanBuild(map, roadStation), Is.False);
            Assert.That(RailPlacement.CanBuild(map, railStation), Is.False);
        }
    }
}
