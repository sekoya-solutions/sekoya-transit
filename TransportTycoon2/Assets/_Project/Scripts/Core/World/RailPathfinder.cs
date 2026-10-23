#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic A* over a <see cref="RailGraph"/> (node id to node id).
    /// </summary>
    public static class RailPathfinder
    {
        /// <summary>
        /// Attempts to find a shortest route between two graph nodes.
        /// </summary>
        /// <param name="graph">The rail graph to search.</param>
        /// <param name="fromNodeId">Start node id.</param>
        /// <param name="toNodeId">Goal node id.</param>
        /// <param name="path">The route when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when a route exists; otherwise <see langword="false"/>.</returns>
        public static bool TryFindPath(RailGraph graph, int fromNodeId, int toNodeId, out RailPath? path)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
#pragma warning restore CA1510

            path = null;
            if (!graph.TryGetNode(fromNodeId, out RailGraphNode? fromNode) || fromNode == null)
            {
                return false;
            }

            if (!graph.TryGetNode(toNodeId, out RailGraphNode? toNode) || toNode == null)
            {
                return false;
            }

            if (fromNodeId == toNodeId)
            {
                path = new RailPath(new[] { fromNodeId }, Array.Empty<int>(), 0);
                return true;
            }

            var gScore = new Dictionary<int, int>
            {
                { fromNodeId, 0 }
            };
            var cameFromNode = new Dictionary<int, int>();
            var cameFromEdge = new Dictionary<int, int>();
            var open = new SortedSet<OpenKey>();
            open.Add(new OpenKey(Heuristic(fromNode.Coordinate, toNode.Coordinate), 0, fromNodeId));

            while (open.Count > 0)
            {
                OpenKey currentKey = open.Min;
                open.Remove(currentKey);

                int currentId = currentKey.NodeId;
                if (!gScore.TryGetValue(currentId, out int currentG) || currentKey.G > currentG)
                {
                    continue;
                }

                if (currentId == toNodeId)
                {
                    path = ReconstructPath(fromNodeId, toNodeId, cameFromNode, cameFromEdge, currentG);
                    return true;
                }

                if (!graph.TryGetNode(currentId, out RailGraphNode? currentNode) || currentNode == null)
                {
                    continue;
                }

                IReadOnlyList<int> incidentEdgeIds = graph.GetIncidentEdgeIds(currentId);
                for (int i = 0; i < incidentEdgeIds.Count; i++)
                {
                    int edgeId = incidentEdgeIds[i];
                    if (!graph.TryGetEdge(edgeId, out RailGraphEdge? edge) || edge == null)
                    {
                        continue;
                    }

                    int neighborId = edge.NodeIdA == currentId ? edge.NodeIdB : edge.NodeIdA;
                    if (neighborId == currentId)
                    {
                        continue;
                    }

                    if (!graph.TryGetNode(neighborId, out RailGraphNode? neighborNode) || neighborNode == null)
                    {
                        continue;
                    }

                    int tentativeG = checked(currentG + edge.Length);
                    if (gScore.TryGetValue(neighborId, out int existingG))
                    {
                        if (tentativeG > existingG)
                        {
                            continue;
                        }

                        if (tentativeG == existingG)
                        {
                            continue;
                        }
                    }

                    gScore[neighborId] = tentativeG;
                    cameFromNode[neighborId] = currentId;
                    cameFromEdge[neighborId] = edgeId;
                    int f = checked(tentativeG + Heuristic(neighborNode.Coordinate, toNode.Coordinate));
                    open.Add(new OpenKey(f, tentativeG, neighborId));
                }
            }

            return false;
        }

        private static RailPath ReconstructPath(
            int fromNodeId,
            int toNodeId,
            Dictionary<int, int> cameFromNode,
            Dictionary<int, int> cameFromEdge,
            int length)
        {
            var nodesReversed = new List<int>();
            var edgesReversed = new List<int>();
            int current = toNodeId;
            nodesReversed.Add(current);
            while (current != fromNodeId)
            {
                edgesReversed.Add(cameFromEdge[current]);
                current = cameFromNode[current];
                nodesReversed.Add(current);
            }

            nodesReversed.Reverse();
            edgesReversed.Reverse();
            return new RailPath(nodesReversed, edgesReversed, length);
        }

        private static int Heuristic(TileCoordinate from, TileCoordinate to)
        {
            return Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
        }

        private readonly struct OpenKey : IComparable<OpenKey>
        {
            public OpenKey(int f, int g, int nodeId)
            {
                F = f;
                G = g;
                NodeId = nodeId;
            }

            public int F { get; }

            public int G { get; }

            public int NodeId { get; }

            public int CompareTo(OpenKey other)
            {
                int compare = F.CompareTo(other.F);
                if (compare != 0)
                {
                    return compare;
                }

                compare = G.CompareTo(other.G);
                if (compare != 0)
                {
                    return compare;
                }

                return NodeId.CompareTo(other.NodeId);
            }
        }
    }
}
