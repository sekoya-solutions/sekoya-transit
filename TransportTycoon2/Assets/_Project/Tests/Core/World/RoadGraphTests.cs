#nullable enable
using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class RoadGraphTests
    {
        [Test]
        public void RebuildFromMap_RepeatedOnLargeMap_ReusesMapSizedArraysAndGivesTheSameGraph()
        {
#if UNITY_5_3_OR_NEWER
            Assert.Ignore("Unity's Mono runtime does not report per-thread allocated bytes; dotnet test enforces this.");
#endif
            var map = new TileMap(MapSize.Large);
            for (int x = 10; x < 60; x++)
            {
                map.SetRoadTypeId(new TileCoordinate(x, 20), TileMap.BasicRoadTypeId);
            }

            for (int y = 21; y < 40; y++)
            {
                map.SetRoadTypeId(new TileCoordinate(59, y), TileMap.BasicRoadTypeId);
            }

            var graph = new RoadGraph();
            graph.RebuildFromMap(map);
            string first = RoadGraphTopology.Fingerprint(graph);

            long before = GC.GetAllocatedBytesForCurrentThread();
            graph.RebuildFromMap(map);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(RoadGraphTopology.Fingerprint(graph), Is.EqualTo(first));
            Assert.That(allocated, Is.LessThan(map.TileCount), "a rebuild must not allocate a map-sized array");
        }

        [Test]
        public void RebuildFromMap_EmptyMap_HasNoNodesAndBumpsVersion()
        {
            var map = new TileMap(MapSize.Small);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            Assert.That(graph.Version, Is.EqualTo(1));
            Assert.That(graph.Nodes, Is.Empty);
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void RebuildFromMap_IsolatedTile_CreatesDegreeZeroNode()
        {
            var map = new TileMap(MapSize.Small);
            var tile = new TileCoordinate(3, 4);
            map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            Assert.That(graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(graph.Nodes[0].Id, Is.EqualTo(map.GetTileId(tile)));
            Assert.That(graph.Nodes[0].Coordinate, Is.EqualTo(tile));
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(graph.TryGetNodeId(tile, out int nodeId), Is.True);
            Assert.That(nodeId, Is.EqualTo(map.GetTileId(tile)));
        }

        [Test]
        public void RebuildFromMap_DiagonalTiles_AreNotConnected()
        {
            var map = new TileMap(MapSize.Small);
            var a = new TileCoordinate(2, 2);
            var b = new TileCoordinate(3, 3);
            map.SetRoadTypeId(a, TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(b, TileMap.BasicRoadTypeId);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void RebuildFromMap_StraightRun_CompressesToSingleEdge()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRoadLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 5);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));
            RoadGraphEdge edge = graph.Edges[0];
            Assert.That(edge.Length, Is.EqualTo(4));
            Assert.That(edge.Path, Has.Count.EqualTo(5));
            Assert.That(edge.Path[0], Is.EqualTo(new TileCoordinate(1, 1)));
            Assert.That(edge.Path[4], Is.EqualTo(new TileCoordinate(5, 1)));
        }

        [Test]
        public void RebuildFromMap_CornerRun_CompressesWithoutCornerNode()
        {
            var map = new TileMap(MapSize.Small);
            map.SetRoadTypeId(new TileCoordinate(1, 1), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(2, 1), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(3, 1), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(3, 2), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(3, 3), TileMap.BasicRoadTypeId);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));
            Assert.That(graph.Edges[0].Length, Is.EqualTo(4));
            Assert.That(graph.TryGetNodeId(new TileCoordinate(3, 1), out _), Is.False);
        }

        [Test]
        public void RebuildFromMap_TJunction_CreatesJunctionNode()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRoadLine(map, new TileCoordinate(2, 2), dx: 1, dy: 0, length: 3);
            map.SetRoadTypeId(new TileCoordinate(3, 3), TileMap.BasicRoadTypeId);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            Assert.That(graph.TryGetNodeId(new TileCoordinate(3, 2), out int junctionId), Is.True);
            Assert.That(graph.GetIncidentEdgeIds(junctionId), Has.Count.EqualTo(3));
            Assert.That(graph.Nodes, Has.Count.EqualTo(4));
            Assert.That(graph.Edges, Has.Count.EqualTo(3));
        }

        [Test]
        public void RebuildFromMap_PureCycle_PromotesMinimumTileAsNode()
        {
            var map = new TileMap(MapSize.Small);
            map.SetRoadTypeId(new TileCoordinate(2, 2), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(3, 2), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(3, 3), TileMap.BasicRoadTypeId);
            map.SetRoadTypeId(new TileCoordinate(2, 3), TileMap.BasicRoadTypeId);
            var graph = new RoadGraph();

            graph.RebuildFromMap(map);

            int expectedAnchorId = map.GetTileId(new TileCoordinate(2, 2));
            Assert.That(graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(graph.Nodes[0].Id, Is.EqualTo(expectedAnchorId));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));
            Assert.That(graph.Edges[0].NodeIdA, Is.EqualTo(expectedAnchorId));
            Assert.That(graph.Edges[0].NodeIdB, Is.EqualTo(expectedAnchorId));
            Assert.That(graph.Edges[0].Length, Is.EqualTo(4));
        }

        [Test]
        public void RebuildFromMap_SplitAndMergeShapes_MatchExpectedTopology()
        {
            var map = new TileMap(MapSize.Small);
            PlaceRoadLine(map, new TileCoordinate(1, 1), dx: 1, dy: 0, length: 4);
            var graph = new RoadGraph();
            graph.RebuildFromMap(map);
            Assert.That(graph.Edges, Has.Count.EqualTo(1));

            map.SetRoadTypeId(new TileCoordinate(2, 1), TileMap.NoRoadTypeId);
            graph.RebuildFromMap(map);
            Assert.That(graph.Nodes, Has.Count.EqualTo(3));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));

            map.SetRoadTypeId(new TileCoordinate(2, 1), TileMap.BasicRoadTypeId);
            graph.RebuildFromMap(map);
            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));
            Assert.That(graph.Edges[0].Length, Is.EqualTo(3));
        }

        [Test]
        public void RebuildFromMap_RepeatedCalls_IncrementVersion()
        {
            var map = new TileMap(MapSize.Small);
            var graph = new RoadGraph();
            graph.RebuildFromMap(map);
            int first = graph.Version;
            graph.RebuildFromMap(map);
            Assert.That(graph.Version, Is.EqualTo(first + 1));
        }

        private static void PlaceRoadLine(TileMap map, TileCoordinate start, int dx, int dy, int length)
        {
            for (int i = 0; i < length; i++)
            {
                map.SetRoadTypeId(new TileCoordinate(start.X + (dx * i), start.Y + (dy * i)), TileMap.BasicRoadTypeId);
            }
        }
    }

    internal static class RoadGraphTopology
    {
        public static string Fingerprint(RoadGraph graph)
        {
            var nodeIds = graph.Nodes.Select(node => node.Id).OrderBy(id => id);
            CultureInfo culture = CultureInfo.InvariantCulture;
            var edges = graph.Edges
                .Select(edge =>
                {
                    int min = edge.NodeIdA < edge.NodeIdB ? edge.NodeIdA : edge.NodeIdB;
                    int max = edge.NodeIdA < edge.NodeIdB ? edge.NodeIdB : edge.NodeIdA;
                    string path = string.Join(
                        ">",
                        edge.Path.Select(tile => tile.X.ToString(culture) + "," + tile.Y.ToString(culture)));
                    return min.ToString(culture) + "-" + max.ToString(culture) + ":" + edge.Length.ToString(culture) + ":" + path;
                })
                .OrderBy(value => value);
            return "n:" + string.Join(",", nodeIds.Select(id => id.ToString(culture))) + "|e:" + string.Join(";", edges);
        }
    }
}
