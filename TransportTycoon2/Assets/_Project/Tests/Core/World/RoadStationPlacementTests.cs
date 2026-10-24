#nullable enable
using NUnit.Framework;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class RoadStationPlacementTests
    {
        [Test]
        public void CanBuild_AdjacentToTown_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var adjacent = new TileCoordinate(6, 5);
            map.SetObjectId(town, 0);

            Assert.That(RoadStationPlacement.CanBuild(map, adjacent), Is.True);
            Assert.That(RoadStationPlacement.CanDemolish(map, adjacent), Is.False);
        }

        [Test]
        public void CanBuild_DoesNotRequireAdjacentRoad()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var adjacent = new TileCoordinate(6, 5);
            map.SetObjectId(town, 0);

            Assert.That(RoadStationPlacement.CanBuild(map, adjacent), Is.True);
            Assert.That(map.GetRoadTypeId(adjacent), Is.EqualTo(TileMap.NoRoadTypeId));
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
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            Assert.That(RoadStationPlacement.CanBuild(map, diagonal), Is.False);
            Assert.That(RoadStationPlacement.CanBuild(map, far), Is.False);
            Assert.That(RoadStationPlacement.CanBuild(map, town), Is.False);
            Assert.That(RoadStationPlacement.CanBuild(map, road), Is.False);
        }

        [Test]
        public void CanDemolish_OnlyExistingStation_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var station = new TileCoordinate(3, 3);
            map.SetStationId(station, 1);

            Assert.That(RoadStationPlacement.CanDemolish(map, station), Is.True);
            Assert.That(RoadStationPlacement.CanDemolish(map, new TileCoordinate(0, 0)), Is.False);
        }

        [Test]
        public void CanBuild_RailStationOrRailOccupied_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var adjacent = new TileCoordinate(6, 5);
            map.SetObjectId(town, 0);
            map.SetRailStationId(adjacent, 1);

            Assert.That(RoadStationPlacement.CanBuild(map, adjacent), Is.False);

            map.SetRailStationId(adjacent, TileMap.NoRailStationId);
            map.SetRailTypeId(adjacent, TileMap.BasicRailTypeId);
            Assert.That(RoadStationPlacement.CanBuild(map, adjacent), Is.False);
        }

        [Test]
        public void CanBuild_MultimodalHub_RoadAndRailStationsAdjacentToSameTown_BothAllowed()
        {
            var map = new TileMap(MapSize.Small);
            var town = new TileCoordinate(5, 5);
            var roadStationTile = new TileCoordinate(6, 5);
            var railStationTile = new TileCoordinate(5, 6);
            map.SetObjectId(town, 0);

            Assert.That(RoadStationPlacement.CanBuild(map, roadStationTile), Is.True);
            Assert.That(RailStationPlacement.CanBuild(map, railStationTile), Is.True);

            map.SetStationId(roadStationTile, 1);
            map.SetRailStationId(railStationTile, 1);

            Assert.That(RoadStationPlacement.CanBuild(map, roadStationTile), Is.False);
            Assert.That(RailStationPlacement.CanBuild(map, railStationTile), Is.False);
            Assert.That(RoadStationPlacement.CanBuild(map, railStationTile), Is.False);
            Assert.That(RailStationPlacement.CanBuild(map, roadStationTile), Is.False);
        }
    }
}
