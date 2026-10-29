#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class RoadPlacementTests
    {
        [Test]
        public void CanBuild_EmptyInBoundsTile_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(10, 12);

            Assert.That(RoadPlacement.CanBuild(map, tile), Is.True);
            Assert.That(RoadPlacement.CanDemolish(map, tile), Is.False);
        }

        [Test]
        public void CanBuild_OccupiedOrExistingRoad_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var occupied = new TileCoordinate(1, 1);
            var road = new TileCoordinate(2, 2);
            map.SetObjectId(occupied, 0);
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            Assert.That(RoadPlacement.CanBuild(map, occupied), Is.False);
            Assert.That(RoadPlacement.CanBuild(map, road), Is.False);
            Assert.That(RoadPlacement.CanDemolish(map, road), Is.True);
        }

        [Test]
        public void CanBuild_OutOfBounds_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);

            Assert.That(RoadPlacement.CanBuild(map, new TileCoordinate(-1, 0)), Is.False);
            Assert.That(RoadPlacement.CanDemolish(map, new TileCoordinate(128, 0)), Is.False);
        }

        [Test]
        public void CanBuild_RoadOrRailStationOccupied_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var roadStation = new TileCoordinate(4, 4);
            var railStation = new TileCoordinate(5, 5);
            map.SetStationId(roadStation, 1);
            map.SetRailStationId(railStation, 1);

            Assert.That(RoadPlacement.CanBuild(map, roadStation), Is.False);
            Assert.That(RoadPlacement.CanBuild(map, railStation), Is.False);
        }
    }
}
