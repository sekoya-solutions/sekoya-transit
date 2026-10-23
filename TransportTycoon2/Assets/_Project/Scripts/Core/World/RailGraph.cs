#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A compressed rail-network node anchored on a rail tile.
    /// Node ids equal the underlying tile id while that tile remains a node.
    /// </summary>
    public sealed class RailGraphNode
    {
        internal RailGraphNode(int id, TileCoordinate coordinate)
        {
            Id = id;
            Coordinate = coordinate;
        }

        /// <summary>
        /// Gets the stable node id (the tile id of <see cref="Coordinate"/>).
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the map tile this node occupies.
        /// </summary>
        public TileCoordinate Coordinate { get; }
    }

    /// <summary>
    /// A bidirectional rail segment between two nodes, including both endpoint tiles in <see cref="Path"/>.
    /// </summary>
    public sealed class RailGraphEdge
    {
        private readonly ReadOnlyCollection<TileCoordinate> _path;

        internal RailGraphEdge(int id, int nodeIdA, int nodeIdB, TileCoordinate[] path)
        {
            Id = id;
            NodeIdA = nodeIdA;
            NodeIdB = nodeIdB;
            Length = checked(path.Length - 1);
            _path = Array.AsReadOnly(path);
        }

        /// <summary>
        /// Gets the deterministic edge id assigned during rebuild.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the node id at the start of <see cref="Path"/>.
        /// </summary>
        public int NodeIdA { get; }

        /// <summary>
        /// Gets the node id at the end of <see cref="Path"/>.
        /// </summary>
        public int NodeIdB { get; }

        /// <summary>
        /// Gets the travel length in tile steps (<c>Path.Count - 1</c>).
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the ordered tiles from <see cref="NodeIdA"/> to <see cref="NodeIdB"/>, inclusive.
        /// </summary>
        public IReadOnlyList<TileCoordinate> Path
        {
            get
            {
                return _path;
            }
        }
    }

    /// <summary>
    /// Orthogonal compressed rail graph derived from a <see cref="TileMap"/> rail channel.
    /// V1 refreshes with a full rebuild after infrastructure edits; the graph is not persisted.
    /// </summary>
    public sealed class RailGraph
    {
        private static readonly int[] _offsetX = { 0, 1, 0, -1 };
        private static readonly int[] _offsetY = { -1, 0, 1, 0 };

        private readonly List<RailGraphNode> _nodes = new List<RailGraphNode>();
        private readonly List<RailGraphEdge> _edges = new List<RailGraphEdge>();
        private readonly Dictionary<int, RailGraphNode> _nodesById = new Dictionary<int, RailGraphNode>();
        private readonly Dictionary<int, List<int>> _incidentEdgeIds = new Dictionary<int, List<int>>();
        private readonly ReadOnlyCollection<RailGraphNode> _nodesView;
        private readonly ReadOnlyCollection<RailGraphEdge> _edgesView;

        /// <summary>
        /// Initializes an empty rail graph at version zero.
        /// </summary>
        public RailGraph()
        {
            _nodesView = _nodes.AsReadOnly();
            _edgesView = _edges.AsReadOnly();
        }

        /// <summary>
        /// Gets the monotonic rebuild counter. Route caches should invalidate when this changes.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Gets the current nodes in ascending id order.
        /// </summary>
        public IReadOnlyList<RailGraphNode> Nodes
        {
            get
            {
                return _nodesView;
            }
        }

        /// <summary>
        /// Gets the current edges in ascending id order.
        /// </summary>
        public IReadOnlyList<RailGraphEdge> Edges
        {
            get
            {
                return _edgesView;
            }
        }

        /// <summary>
        /// Returns whether a rail graph node currently exists at the tile.
        /// </summary>
        public bool TryGetNodeId(TileCoordinate coordinate, out int nodeId)
        {
            foreach (RailGraphNode node in _nodes)
            {
                if (node.Coordinate.Equals(coordinate))
                {
                    nodeId = node.Id;
                    return true;
                }
            }

            nodeId = 0;
            return false;
        }

        /// <summary>
        /// Returns the node with the given id when it exists in the current graph.
        /// </summary>
        public bool TryGetNode(int nodeId, out RailGraphNode? node)
        {
            if (_nodesById.TryGetValue(nodeId, out RailGraphNode? found))
            {
                node = found;
                return true;
            }

            node = null;
            return false;
        }

        /// <summary>
        /// Returns the edge with the given id when it exists in the current graph.
        /// </summary>
        public bool TryGetEdge(int edgeId, out RailGraphEdge? edge)
        {
            if (edgeId >= 0 && edgeId < _edges.Count)
            {
                edge = _edges[edgeId];
                return true;
            }

            edge = null;
            return false;
        }

        /// <summary>
        /// Finds the edge whose endpoints are <paramref name="fromNodeId"/> and <paramref name="toNodeId"/>.
        /// </summary>
        /// <param name="fromNodeId">Travel-from node id.</param>
        /// <param name="toNodeId">Travel-to node id.</param>
        /// <param name="edge">The matching edge when found.</param>
        /// <param name="length">Edge length in tile steps.</param>
        /// <param name="reverse">
        /// <see langword="true"/> when travel is opposite to <see cref="RailGraphEdge.Path"/> order
        /// (path runs to→from).
        /// </param>
        /// <returns><see langword="true"/> when an edge connects the two nodes.</returns>
        public bool TryFindEdgeBetween(
            int fromNodeId,
            int toNodeId,
            out RailGraphEdge? edge,
            out int length,
            out bool reverse)
        {
            if (_incidentEdgeIds.TryGetValue(fromNodeId, out List<int>? edgeIds))
            {
                for (int index = 0; index < edgeIds.Count; index++)
                {
                    RailGraphEdge candidate = _edges[edgeIds[index]];
                    if (candidate.NodeIdA == fromNodeId && candidate.NodeIdB == toNodeId)
                    {
                        edge = candidate;
                        length = candidate.Length;
                        reverse = false;
                        return true;
                    }

                    if (candidate.NodeIdB == fromNodeId && candidate.NodeIdA == toNodeId)
                    {
                        edge = candidate;
                        length = candidate.Length;
                        reverse = true;
                        return true;
                    }
                }
            }

            edge = null;
            length = 0;
            reverse = false;
            return false;
        }

        /// <summary>
        /// Returns incident edge ids for a node, or an empty list when the node is unknown.
        /// </summary>
        public IReadOnlyList<int> GetIncidentEdgeIds(int nodeId)
        {
            if (_incidentEdgeIds.TryGetValue(nodeId, out List<int>? edgeIds))
            {
                return edgeIds.AsReadOnly();
            }

            return Array.Empty<int>();
        }

        /// <summary>
        /// Rebuilds the compressed graph from the map rail channel and increments <see cref="Version"/>.
        /// </summary>
        /// <param name="map">The map whose rail tiles define the network.</param>
        public void RebuildFromMap(TileMap map)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            _nodes.Clear();
            _edges.Clear();
            _nodesById.Clear();
            _incidentEdgeIds.Clear();

            var railTiles = new List<TileCoordinate>();
            var isRail = new bool[map.TileCount];
            for (int tileId = 0; tileId < map.TileCount; tileId++)
            {
                TileCoordinate coordinate = map.GetCoordinate(tileId);
                if (map.GetRailTypeId(coordinate) != TileMap.NoRailTypeId)
                {
                    isRail[tileId] = true;
                    railTiles.Add(coordinate);
                }
            }

            var isNode = new bool[map.TileCount];
            if (railTiles.Count > 0)
            {
                var degree = new int[map.TileCount];
                foreach (TileCoordinate coordinate in railTiles)
                {
                    int tileId = map.GetTileId(coordinate);
                    degree[tileId] = CountRailNeighbors(map, isRail, coordinate);
                }

                foreach (TileCoordinate coordinate in railTiles)
                {
                    int tileId = map.GetTileId(coordinate);
                    if (degree[tileId] != 2)
                    {
                        isNode[tileId] = true;
                    }
                }

                PromoteCycleAnchors(map, isRail, isNode, railTiles);
            }

            var nodeTileIds = new List<int>();
            for (int tileId = 0; tileId < isNode.Length; tileId++)
            {
                if (isNode[tileId])
                {
                    nodeTileIds.Add(tileId);
                }
            }

            nodeTileIds.Sort();
            foreach (int tileId in nodeTileIds)
            {
                var node = new RailGraphNode(tileId, map.GetCoordinate(tileId));
                _nodes.Add(node);
                _nodesById.Add(tileId, node);
                _incidentEdgeIds.Add(tileId, new List<int>());
            }

            var usedHalfEdges = new bool[checked(map.TileCount * 4)];
            int nextEdgeId = 0;
            foreach (int startTileId in nodeTileIds)
            {
                if (!isRail[startTileId])
                {
                    continue;
                }

                TileCoordinate start = map.GetCoordinate(startTileId);
                for (int direction = 0; direction < 4; direction++)
                {
                    int halfIndex = checked((startTileId * 4) + direction);
                    if (usedHalfEdges[halfIndex])
                    {
                        continue;
                    }

                    TileCoordinate neighbor = Offset(start, direction);
                    if (!map.Contains(neighbor))
                    {
                        continue;
                    }

                    int neighborTileId = map.GetTileId(neighbor);
                    if (!isRail[neighborTileId])
                    {
                        continue;
                    }

                    TileCoordinate[] path = WalkToNode(map, isRail, isNode, start, direction);
                    int endTileId = map.GetTileId(path[path.Length - 1]);
                    int reverseDirection = DirectionBetween(path[path.Length - 1], path[path.Length - 2]);
                    usedHalfEdges[halfIndex] = true;
                    usedHalfEdges[checked((endTileId * 4) + reverseDirection)] = true;

                    var edge = new RailGraphEdge(nextEdgeId, startTileId, endTileId, path);
                    _edges.Add(edge);
                    _incidentEdgeIds[startTileId].Add(nextEdgeId);
                    if (endTileId != startTileId)
                    {
                        _incidentEdgeIds[endTileId].Add(nextEdgeId);
                    }

                    nextEdgeId = checked(nextEdgeId + 1);
                }
            }

            Version = checked(Version + 1);
        }

        private static void PromoteCycleAnchors(
            TileMap map,
            bool[] isRail,
            bool[] isNode,
            List<TileCoordinate> railTiles)
        {
            var visited = new bool[map.TileCount];
            var component = new List<int>();
            foreach (TileCoordinate seed in railTiles)
            {
                int seedId = map.GetTileId(seed);
                if (visited[seedId])
                {
                    continue;
                }

                component.Clear();
                var queue = new Queue<int>();
                queue.Enqueue(seedId);
                visited[seedId] = true;
                bool hasNode = false;
                int minimumTileId = seedId;

                while (queue.Count > 0)
                {
                    int tileId = queue.Dequeue();
                    component.Add(tileId);
                    if (isNode[tileId])
                    {
                        hasNode = true;
                    }

                    if (tileId < minimumTileId)
                    {
                        minimumTileId = tileId;
                    }

                    TileCoordinate coordinate = map.GetCoordinate(tileId);
                    for (int direction = 0; direction < 4; direction++)
                    {
                        TileCoordinate neighbor = Offset(coordinate, direction);
                        if (!map.Contains(neighbor))
                        {
                            continue;
                        }

                        int neighborId = map.GetTileId(neighbor);
                        if (!isRail[neighborId] || visited[neighborId])
                        {
                            continue;
                        }

                        visited[neighborId] = true;
                        queue.Enqueue(neighborId);
                    }
                }

                if (!hasNode)
                {
                    isNode[minimumTileId] = true;
                }
            }
        }

        private static TileCoordinate[] WalkToNode(
            TileMap map,
            bool[] isRail,
            bool[] isNode,
            TileCoordinate start,
            int startDirection)
        {
            var path = new List<TileCoordinate>(8)
            {
                start
            };

            TileCoordinate previous = start;
            TileCoordinate current = Offset(start, startDirection);
            while (true)
            {
                path.Add(current);
                int currentTileId = map.GetTileId(current);
                if (isNode[currentTileId])
                {
                    break;
                }

                int nextDirection = -1;
                for (int direction = 0; direction < 4; direction++)
                {
                    TileCoordinate candidate = Offset(current, direction);
                    if (!map.Contains(candidate) || candidate.Equals(previous))
                    {
                        continue;
                    }

                    int candidateId = map.GetTileId(candidate);
                    if (!isRail[candidateId])
                    {
                        continue;
                    }

                    nextDirection = direction;
                    break;
                }

                if (nextDirection < 0)
                {
                    throw new InvalidOperationException("Rail walk reached a dead end that is not a graph node.");
                }

                previous = current;
                current = Offset(current, nextDirection);
            }

            return path.ToArray();
        }

        private static int CountRailNeighbors(TileMap map, bool[] isRail, TileCoordinate coordinate)
        {
            int count = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                TileCoordinate neighbor = Offset(coordinate, direction);
                if (map.Contains(neighbor) && isRail[map.GetTileId(neighbor)])
                {
                    count++;
                }
            }

            return count;
        }

        private static TileCoordinate Offset(TileCoordinate coordinate, int direction)
        {
            return new TileCoordinate(coordinate.X + _offsetX[direction], coordinate.Y + _offsetY[direction]);
        }

        private static int DirectionBetween(TileCoordinate from, TileCoordinate to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            for (int direction = 0; direction < 4; direction++)
            {
                if (_offsetX[direction] == dx && _offsetY[direction] == dy)
                {
                    return direction;
                }
            }

            throw new InvalidOperationException("Tiles are not orthogonal neighbors.");
        }
    }
}
