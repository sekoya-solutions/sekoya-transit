#nullable enable
using NUnit.Framework;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class TilePlacementPreviewRulesTests
    {
        [Test]
        public void IsBuildGhostValid_EmptyTile_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            Assert.That(TilePlacementPreviewRules.IsBuildGhostValid(map, new TileCoordinate(4, 5)), Is.True);
        }

        [Test]
        public void IsBuildGhostValid_OccupiedOrRoadTile_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            var occupied = new TileCoordinate(1, 1);
            var road = new TileCoordinate(2, 2);
            map.SetObjectId(occupied, 0);
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            Assert.That(TilePlacementPreviewRules.IsBuildGhostValid(map, occupied), Is.False);
            Assert.That(TilePlacementPreviewRules.IsBuildGhostValid(map, road), Is.False);
        }

        [Test]
        public void IsRailBuildGhostValid_RoadWithoutRail_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(7, 7);
            map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);

            Assert.That(TilePlacementPreviewRules.IsRailBuildGhostValid(map, tile), Is.True);
        }

        [Test]
        public void IsRailDemolishGhostValid_OnlyExistingRail_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var rail = new TileCoordinate(8, 8);
            map.SetRailTypeId(rail, TileMap.BasicRailTypeId);

            Assert.That(TilePlacementPreviewRules.IsRailDemolishGhostValid(map, rail), Is.True);
            Assert.That(TilePlacementPreviewRules.IsRailDemolishGhostValid(map, new TileCoordinate(0, 0)), Is.False);
        }

        [Test]
        public void IsDemolishGhostValid_OnlyExistingRoad_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var road = new TileCoordinate(3, 3);
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, road), Is.True);
            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, new TileCoordinate(0, 0)), Is.False);
        }

        [Test]
        public void IsStationBuildGhostValid_AdjacentToTown_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            map.SetObjectId(new TileCoordinate(5, 5), 0);

            Assert.That(
                TilePlacementPreviewRules.IsStationBuildGhostValid(map, new TileCoordinate(6, 5)),
                Is.True);
            Assert.That(
                TilePlacementPreviewRules.IsStationBuildGhostValid(map, new TileCoordinate(10, 10)),
                Is.False);
        }

        [Test]
        public void IsStationDemolishGhostValid_OnlyExistingStation_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var station = new TileCoordinate(4, 4);
            map.SetStationId(station, 1);

            Assert.That(TilePlacementPreviewRules.IsStationDemolishGhostValid(map, station), Is.True);
            Assert.That(
                TilePlacementPreviewRules.IsStationDemolishGhostValid(map, new TileCoordinate(0, 0)),
                Is.False);
        }
    }
}
