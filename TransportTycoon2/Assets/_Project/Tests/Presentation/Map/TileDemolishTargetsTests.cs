#nullable enable
using NUnit.Framework;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class TileDemolishTargetsTests
    {
        [Test]
        public void Resolve_EmptyTile_HasNothingToRemove()
        {
            var map = new TileMap(MapSize.Small);

            Assert.That(TileDemolishTargets.Resolve(map, new TileCoordinate(1, 1)).Any, Is.False);
        }

        [Test]
        public void Resolve_RoadOnly_RemovesRoad()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(2, 2);
            map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);

            TileDemolishTargets targets = TileDemolishTargets.Resolve(map, tile);

            Assert.That(targets.Road, Is.True);
            Assert.That(targets.Rail, Is.False);
            Assert.That(targets.RoadStation, Is.False);
            Assert.That(targets.RailStation, Is.False);
        }

        [Test]
        public void Resolve_RoadAndRail_RemovesBoth()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(3, 3);
            map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);
            map.SetRailTypeId(tile, TileMap.BasicRailTypeId);

            TileDemolishTargets targets = TileDemolishTargets.Resolve(map, tile);

            Assert.That(targets.Road, Is.True);
            Assert.That(targets.Rail, Is.True);
        }

        [Test]
        public void Resolve_RoadStation_RemovesStationOnly()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(4, 4);
            map.SetStationId(tile, 1);

            TileDemolishTargets targets = TileDemolishTargets.Resolve(map, tile);

            Assert.That(targets.RoadStation, Is.True);
            Assert.That(targets.Road, Is.False);
            Assert.That(targets.RailStation, Is.False);
        }

        [Test]
        public void Resolve_RailStation_RemovesStationOnly()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(5, 5);
            map.SetRailStationId(tile, 1);

            TileDemolishTargets targets = TileDemolishTargets.Resolve(map, tile);

            Assert.That(targets.RailStation, Is.True);
            Assert.That(targets.Rail, Is.False);
            Assert.That(targets.RoadStation, Is.False);
        }
    }
}
