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
        public void IsDemolishGhostValid_OnlyExistingRoad_ReturnsTrue()
        {
            var map = new TileMap(MapSize.Small);
            var road = new TileCoordinate(3, 3);
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, road), Is.True);
            Assert.That(TilePlacementPreviewRules.IsDemolishGhostValid(map, new TileCoordinate(0, 0)), Is.False);
        }
    }
}
