#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Keeps a vehicle in place when the road or rail graph is rebuilt under it (Issue #193). Joining new track to the
    /// middle of an edge turns that tile into a node and splits the edge; the vehicle's old edge then no longer exists
    /// between its two nodes. This finds the part of the old edge the vehicle is on, bounded by the nearest new nodes.
    /// </summary>
    internal static class GraphEdgeSplit
    {
        /// <summary>
        /// Locates the vehicle on the old edge <paramref name="path"/> (in travel order when
        /// <paramref name="reversed"/> is false, opposite otherwise) at <paramref name="progress"/> tile steps from
        /// its start, and returns the nearest node tiles behind and ahead of it in the new graph.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when the old edge is too short to hold a position or a bounding tile is not a node.
        /// </returns>
        internal static bool TryLocate(
            IReadOnlyList<TileCoordinate> path,
            bool reversed,
            FixedPoint progress,
            Func<TileCoordinate, bool> isNode,
            out TileCoordinate fromTile,
            out TileCoordinate nextTile,
            out TileCoordinate toTile,
            out int segmentLength,
            out FixedPoint progressOnSegment)
        {
            fromTile = default;
            nextTile = default;
            toTile = default;
            segmentLength = 0;
            progressOnSegment = FixedPoint.Zero;
            int last = path.Count - 1;
            if (last < 1 || progress < FixedPoint.Zero)
            {
                return false;
            }

            long step = progress.ToInt64();
            if (step > last - 1)
            {
                step = last - 1;
            }

            int start = (int)step;
            while (start > 0 && !isNode(At(path, reversed, start)))
            {
                start--;
            }

            int end = (int)step + 1;
            while (end < last && !isNode(At(path, reversed, end)))
            {
                end++;
            }

            fromTile = At(path, reversed, start);
            nextTile = At(path, reversed, start + 1);
            toTile = At(path, reversed, end);
            segmentLength = end - start;
            progressOnSegment = progress - FixedPoint.FromInt64(start);
            FixedPoint max = FixedPoint.FromInt64(segmentLength);
            if (progressOnSegment > max)
            {
                progressOnSegment = max;
            }

            return isNode(fromTile) && isNode(toTile);
        }

        private static TileCoordinate At(IReadOnlyList<TileCoordinate> path, bool reversed, int index)
        {
            return reversed ? path[path.Count - 1 - index] : path[index];
        }
    }
}
