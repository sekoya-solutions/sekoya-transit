#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
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
        public void IsDemolishGhostValid_RoadRailOrStation_ReturnsTrueOnlyWhenSomethingToRemove()
        {
            var map = new TileMap(MapSize.Small);
            var road = new TileCoordinate(3, 3);
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            var rail = new TileCoordinate(8, 8);
            var station = new TileCoordinate(4, 4);
            map.SetRailTypeId(rail, TileMap.BasicRailTypeId);
            map.SetStationId(station, 1);

            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, road), Is.True);
            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, rail), Is.True);
            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, station), Is.True);
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
        public void IsRailStationBuildGhostValid_AdjacentToTown_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            map.SetObjectId(new TileCoordinate(5, 5), 0);

            Assert.That(
                TilePlacementPreviewRules.IsRailStationBuildGhostValid(map, new TileCoordinate(6, 5)),
                Is.True);
            Assert.That(
                TilePlacementPreviewRules.IsRailStationBuildGhostValid(map, new TileCoordinate(10, 10)),
                Is.False);
        }
    }
}
