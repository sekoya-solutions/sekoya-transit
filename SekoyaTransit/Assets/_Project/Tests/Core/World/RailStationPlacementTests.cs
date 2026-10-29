#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class RailStationPlacementTests
    {
        [Test]
        public void CanBuild_AdjacentToTown_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var adjacent = new TileCoordinate(6, 5);
            map.SetObjectId(town, 0);

            Assert.That(RailStationPlacement.CanBuild(map, adjacent), Is.True);
            Assert.That(RailStationPlacement.CanDemolish(map, adjacent), Is.False);
        }

        [Test]
        public void CanBuild_NotAdjacentOrOccupied_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var diagonal = new TileCoordinate(6, 6);
            var far = new TileCoordinate(10, 10);
            var road = new TileCoordinate(5, 6);
            map.SetObjectId(town, 0);
            map.SetRailTypeId(road, TileMap.BasicRailTypeId);

            Assert.That(RailStationPlacement.CanBuild(map, diagonal), Is.False);
            Assert.That(RailStationPlacement.CanBuild(map, far), Is.False);
            Assert.That(RailStationPlacement.CanBuild(map, town), Is.False);
            Assert.That(RailStationPlacement.CanBuild(map, road), Is.False);
        }

        [Test]
        public void CanDemolish_OnlyExistingStation_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var station = new TileCoordinate(3, 3);
            map.SetRailStationId(station, 1);

            Assert.That(RailStationPlacement.CanDemolish(map, station), Is.True);
            Assert.That(RailStationPlacement.CanDemolish(map, new TileCoordinate(0, 0)), Is.False);
        }

        [Test]
        public void CanBuild_RoadStationOrRoadOccupied_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var adjacent = new TileCoordinate(6, 5);
            map.SetObjectId(town, 0);
            map.SetStationId(adjacent, 1);

            Assert.That(RailStationPlacement.CanBuild(map, adjacent), Is.False);

            map.SetStationId(adjacent, TileMap.NoStationId);
            map.SetRoadTypeId(adjacent, TileMap.BasicRoadTypeId);
            Assert.That(RailStationPlacement.CanBuild(map, adjacent), Is.False);
        }

        [Test]
        public void CanBuild_DoesNotRequireAdjacentRail()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var adjacent = new TileCoordinate(6, 5);
            map.SetObjectId(town, 0);

            Assert.That(RailStationPlacement.CanBuild(map, adjacent), Is.True);
            Assert.That(map.GetRailTypeId(adjacent), Is.EqualTo(TileMap.NoRailTypeId));
        }
    }
}
