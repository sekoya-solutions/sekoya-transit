#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class MapTileShapesTests
    {
        private const int N = MapTileShapes.North;
        private const int E = MapTileShapes.East;
        private const int S = MapTileShapes.South;
        private const int W = MapTileShapes.West;

        [TestCase(0, MapTileAtlas.RoadNS)]
        [TestCase(N, MapTileAtlas.RoadEndN)]
        [TestCase(E, MapTileAtlas.RoadEndE)]
        [TestCase(S, MapTileAtlas.RoadEndS)]
        [TestCase(W, MapTileAtlas.RoadEndW)]
        [TestCase(N | S, MapTileAtlas.RoadNS)]
        [TestCase(E | W, MapTileAtlas.RoadEW)]
        [TestCase(N | E, MapTileAtlas.RoadNE)]
        [TestCase(E | S, MapTileAtlas.RoadES)]
        [TestCase(S | W, MapTileAtlas.RoadSW)]
        [TestCase(N | W, MapTileAtlas.RoadNW)]
        [TestCase(N | E | S, MapTileAtlas.RoadNES)]
        [TestCase(E | S | W, MapTileAtlas.RoadESW)]
        [TestCase(N | S | W, MapTileAtlas.RoadNSW)]
        [TestCase(N | E | W, MapTileAtlas.RoadNEW)]
        [TestCase(N | E | S | W, MapTileAtlas.RoadCross)]
        public void RoadCell_EveryNeighborMask_PicksItsShape(int mask, int expectedCell)
        {
            Assert.That(MapTileShapes.RoadCell(mask), Is.EqualTo(expectedCell));
        }

        [TestCase(0, MapTileAtlas.RailNS)]
        [TestCase(N, MapTileAtlas.RailNS)]
        [TestCase(E, MapTileAtlas.RailEW)]
        [TestCase(S, MapTileAtlas.RailNS)]
        [TestCase(W, MapTileAtlas.RailEW)]
        [TestCase(N | S, MapTileAtlas.RailNS)]
        [TestCase(E | W, MapTileAtlas.RailEW)]
        [TestCase(N | E, MapTileAtlas.RailNE)]
        [TestCase(E | S, MapTileAtlas.RailES)]
        [TestCase(S | W, MapTileAtlas.RailSW)]
        [TestCase(N | W, MapTileAtlas.RailNW)]
        [TestCase(N | E | S, MapTileAtlas.RailNES)]
        [TestCase(E | S | W, MapTileAtlas.RailESW)]
        [TestCase(N | S | W, MapTileAtlas.RailNSW)]
        [TestCase(N | E | W, MapTileAtlas.RailNEW)]
        [TestCase(N | E | S | W, MapTileAtlas.RailCross)]
        public void RailCell_EveryNeighborMask_PicksItsShapeWithDeadEndsStraight(int mask, int expectedCell)
        {
            Assert.That(MapTileShapes.RailCell(mask), Is.EqualTo(expectedCell));
        }

        [TestCase(N | S, MapTileAtlas.RailsOnlyNS)]
        [TestCase(N, MapTileAtlas.RailsOnlyNS)]
        [TestCase(E | W, MapTileAtlas.RailsOnlyEW)]
        [TestCase(W, MapTileAtlas.RailsOnlyEW)]
        [TestCase(N | E | S, MapTileAtlas.RailNES)]
        [TestCase(N | E, MapTileAtlas.RailNE)]
        public void LevelCrossingRailCell_StraightRail_DropsSleepersOtherwiseFullShape(int railMask, int expectedCell)
        {
            Assert.That(MapTileShapes.LevelCrossingRailCell(railMask), Is.EqualTo(expectedCell));
        }

        [Test]
        public void GetRoadMask_RoadAndRoadStationNeighbors_CountRailDoesNot()
        {
            var map = new TileMap(MapSize.Small);
            var center = new TileCoordinate(5, 5);
            map.SetRoadTypeId(new TileCoordinate(5, 6), TileMap.BasicRoadTypeId);
            map.SetStationId(new TileCoordinate(6, 5), 1);
            map.SetRailTypeId(new TileCoordinate(5, 4), TileMap.BasicRailTypeId);
            map.SetRailStationId(new TileCoordinate(4, 5), 1);

            Assert.That(MapTileShapes.GetRoadMask(map, center), Is.EqualTo(N | E));
        }

        [Test]
        public void GetRailMask_RailAndRailStationNeighbors_CountRoadDoesNot()
        {
            var map = new TileMap(MapSize.Small);
            var center = new TileCoordinate(5, 5);
            map.SetRoadTypeId(new TileCoordinate(5, 6), TileMap.BasicRoadTypeId);
            map.SetStationId(new TileCoordinate(6, 5), 1);
            map.SetRailTypeId(new TileCoordinate(5, 4), TileMap.BasicRailTypeId);
            map.SetRailStationId(new TileCoordinate(4, 5), 1);

            Assert.That(MapTileShapes.GetRailMask(map, center), Is.EqualTo(S | W));
        }

        [Test]
        public void GetRoadMask_MapCorner_IgnoresSidesOutsideTheMap()
        {
            var map = new TileMap(MapSize.Small);
            map.SetRoadTypeId(new TileCoordinate(1, 0), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(0, 1), TileMap.BasicRoadTypeId);

            Assert.That(MapTileShapes.GetRoadMask(map, new TileCoordinate(0, 0)), Is.EqualTo(N | E));
        }

        [Test]
        public void AtlasCells_AllShapes_AreDistinctAndInsideTheTexture()
        {
            var cells = new HashSet<int>();
            for (int mask = 0; mask < 16; mask++)
            {
                cells.Add(MapTileShapes.RoadCell(mask));
            }

            int roadShapes = cells.Count;
            for (int mask = 0; mask < 16; mask++)
            {
                cells.Add(MapTileShapes.RailCell(mask));
                cells.Add(MapTileShapes.LevelCrossingRailCell(mask));
            }

            Assert.That(roadShapes, Is.EqualTo(15));
            Assert.That(cells.Count, Is.EqualTo(15 + 11 + 2));
            foreach (int cell in cells)
            {
                UnityEngine.RectInt rect = MapTileAtlas.GetPixelRect(cell);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(MapTileAtlas.TextureWidth));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(MapTileAtlas.TextureHeight));
            }
        }
    }
}
