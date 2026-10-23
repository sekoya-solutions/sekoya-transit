#nullable enable
using NUnit.Framework;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class RailPathfinderTests
    {
        [Test]
        public void TryFindPath_UnknownNode_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 3);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);

            Assert.That(RailPathfinder.TryFindPath(graph, fromNodeId: -1, toNodeId: graph.Nodes[0].Id, out RailPath? path), Is.False);
            Assert.That(path, Is.Null);
        }

        [Test]
        public void TryFindPath_SameNode_ReturnsEmptyRoute()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 3);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 1), out int nodeId), Is.True);

            Assert.That(RailPathfinder.TryFindPath(graph, nodeId, nodeId, out RailPath? path), Is.True);
            Assert.That(path, Is.Not.Null);
            Assert.That(path!.Length, Is.EqualTo(0));
            Assert.That(path.NodeIds, Is.EqualTo(new[] { nodeId }));
            Assert.That(path.EdgeIds, Is.Empty);
        }

        [Test]
        public void TryFindPath_StraightRun_ReturnsSingleEdge()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 5);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 1), out int startId), Is.True);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(5, 1), out int endId), Is.True);

            Assert.That(RailPathfinder.TryFindPath(graph, startId, endId, out RailPath? path), Is.True);
            Assert.That(path, Is.Not.Null);
            Assert.That(path!.Length, Is.EqualTo(4));
            Assert.That(path.NodeIds, Is.EqualTo(new[] { startId, endId }));
            Assert.That(path.EdgeIds, Has.Count.EqualTo(1));
            Assert.That(path.EdgeIds[0], Is.EqualTo(graph.Edges[0].Id));
        }

        [Test]
        public void TryFindPath_DisconnectedComponents_ReturnsFalse()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 2);
            PlaceRailLine(map, new TileCoordinate(5, 5), dx: 1, dy: 0, length: 2);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 1), out int a), Is.True);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(5, 5), out int b), Is.True);

            Assert.That(RailPathfinder.TryFindPath(graph, a, b, out RailPath? path), Is.False);
            Assert.That(path, Is.Null);
        }

        [Test]
        public void TryFindPath_EqualCostBranches_PrefersLowerNodeId()
        {
            // Two equal-length corridors S → JA → G and S → JB → G.
            // Spurs keep S/JA/JB/G as junctions so mid nodes stay in the path.
            var map = new TileMap(MapSize.Small);
            var start = new TileCoordinate(2, 2);
            var junctionA = new TileCoordinate(0, 2);
            var junctionB = new TileCoordinate(4, 2);
            var goal = new TileCoordinate(2, 4);
            TileCoordinate[] roads =
            {
                start, new TileCoordinate(2, 1),
                new TileCoordinate(1, 2), junctionA, new TileCoordinate(0, 1),
                new TileCoordinate(0, 3), new TileCoordinate(0, 4), new TileCoordinate(1, 4), goal, new TileCoordinate(2, 5),
                new TileCoordinate(3, 2), junctionB, new TileCoordinate(4, 1),
                new TileCoordinate(4, 3), new TileCoordinate(4, 4), new TileCoordinate(3, 4),
            };
            foreach (TileCoordinate tile in roads)
            {
                map.SetRailTypeId(tile, TileMap.BasicRailTypeId);
            }

            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(start, out int startId), Is.True);
            Assert.That(graph.TryGetNodeId(goal, out int goalId), Is.True);
            Assert.That(graph.TryGetNodeId(junctionA, out int junctionAId), Is.True);
            Assert.That(graph.TryGetNodeId(junctionB, out int junctionBId), Is.True);

            int preferredMidId = junctionAId < junctionBId ? junctionAId : junctionBId;

            Assert.That(RailPathfinder.TryFindPath(graph, startId, goalId, out RailPath? path), Is.True);
            Assert.That(path, Is.Not.Null);
            Assert.That(path!.NodeIds, Has.Count.EqualTo(3));
            Assert.That(path.NodeIds[0], Is.EqualTo(startId));
            Assert.That(path.NodeIds[1], Is.EqualTo(preferredMidId));
            Assert.That(path.NodeIds[2], Is.EqualTo(goalId));

            Assert.That(RailPathfinder.TryFindPath(graph, startId, goalId, out RailPath? again), Is.True);
            Assert.That(again!.NodeIds, Is.EqualTo(path.NodeIds));
            Assert.That(again.EdgeIds, Is.EqualTo(path.EdgeIds));
            Assert.That(again.Length, Is.EqualTo(path.Length));
        }

        [Test]
        public void TryFindPath_PrefersShorterBranch()
        {
            // Short: S(1,2)-J(3,2)-G(5,2) via the horizontal (length 4).
            // Long: S-J via detour through (1,4)-(3,4)-(5,4) then to G — longer than short.
            // Spurs keep S, J, G as nodes.
            var map = new TileMap(MapSize.Small);
            TileCoordinate[] roads =
            {
                new TileCoordinate(1, 2), new TileCoordinate(1, 1),
                new TileCoordinate(2, 2), new TileCoordinate(3, 2), new TileCoordinate(3, 1),
                new TileCoordinate(4, 2), new TileCoordinate(5, 2), new TileCoordinate(5, 1),
                new TileCoordinate(1, 3), new TileCoordinate(1, 4),
                new TileCoordinate(2, 4), new TileCoordinate(3, 4), new TileCoordinate(4, 4), new TileCoordinate(5, 4),
                new TileCoordinate(5, 3),
            };
            foreach (TileCoordinate tile in roads)
            {
                map.SetRailTypeId(tile, TileMap.BasicRailTypeId);
            }

            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 2), out int startId), Is.True);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(5, 2), out int goalId), Is.True);

            Assert.That(RailPathfinder.TryFindPath(graph, startId, goalId, out RailPath? path), Is.True);
            Assert.That(path!.Length, Is.EqualTo(4));
            Assert.That(path.NodeIds[0], Is.EqualTo(startId));
            Assert.That(path.NodeIds[path.NodeIds.Count - 1], Is.EqualTo(goalId));
        }

        private static void PlaceRailLine(TileMap map, TileCoordinate start, int dx, int dy, int length)
        {
            for (int i = 0; i < length; i++)
            {
                map.SetRailTypeId(new TileCoordinate(start.X + (dx * i), start.Y + (dy * i)), TileMap.BasicRailTypeId);
            }
        }
    }

    [TestFixture]
    public sealed class RailRouteCacheTests
    {
        [Test]
        public void TryGetPath_SecondCall_ReturnsCachedInstance()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 4);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 1), out int startId), Is.True);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(4, 1), out int endId), Is.True);
            var cache = new RailRouteCache();

            Assert.That(cache.TryGetPath(graph, startId, endId, out RailPath? first), Is.True);
            Assert.That(cache.TryGetPath(graph, startId, endId, out RailPath? second), Is.True);
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void TryGetPath_AfterGraphVersionBump_Recomputes()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 4);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 1), out int startId), Is.True);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(4, 1), out int endId), Is.True);
            var cache = new RailRouteCache();

            Assert.That(cache.TryGetPath(graph, startId, endId, out RailPath? first), Is.True);
            int versionBefore = graph.Version;
            graph.RebuildFromMap(map);
            Assert.That(graph.Version, Is.EqualTo(versionBefore + 1));

            Assert.That(cache.TryGetPath(graph, startId, endId, out RailPath? second), Is.True);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second!.Length, Is.EqualTo(first!.Length));
            Assert.That(second.NodeIds, Is.EqualTo(first.NodeIds));
        }

        [Test]
        public void TryGetPath_Unreachable_ReturnsFalseAndDoesNotCacheSuccess()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRailLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 2);
            PlaceRailLine(map, new TileCoordinate(5, 5), dx: 1, dy: 0, length: 2);
            var graph = new RailGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(1, 1), out int a), Is.True);
            Assert.That(graph.TryGetNodeId(new TileCoordinate(5, 5), out int b), Is.True);
            var cache = new RailRouteCache();

            Assert.That(cache.TryGetPath(graph, a, b, out RailPath? path), Is.False);
            Assert.That(path, Is.Null);
            Assert.That(cache.TryGetPath(graph, a, b, out _), Is.False);
        }

        private static void PlaceRailLine(TileMap map, TileCoordinate start, int dx, int dy, int length)
        {
            for (int i = 0; i < length; i++)
            {
                map.SetRailTypeId(new TileCoordinate(start.X + (dx * i), start.Y + (dy * i)), TileMap.BasicRailTypeId);
            }
        }
    }
}
