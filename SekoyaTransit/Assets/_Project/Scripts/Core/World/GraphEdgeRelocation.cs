#nullable enable
using System.Collections.Generic;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Keeps a vehicle in place when a track or station edit rebuilds the road or rail graph (Issue #193). Joining
    /// track to the middle of an edge splits it at a new junction; removing a side track merges two edges into one.
    /// Either way the vehicle's edge may no longer exist, but the tiles it runs on do:
    /// <c>RebuildGraphKeepingVehicles</c> reads each vehicle's tile and next tile here before the rebuild, then finds
    /// the new edge holding them (<c>TryFindEdgeThrough</c>).
    /// </summary>
    internal static class GraphEdgeRelocation
    {
        /// <summary>
        /// Returns the tile the vehicle is on, the next tile it moves toward, and how far it is past the tile, at
        /// <paramref name="progress"/> tile steps along <paramref name="path"/> (in travel order when
        /// <paramref name="reversed"/> is false, opposite otherwise). At the end of the path, the step is the last one
        /// with a fraction of one.
        /// </summary>
        /// <returns><see langword="false"/> when the path has no step or the progress is negative.</returns>
        internal static bool TryGetTravelStep(
            IReadOnlyList<TileCoordinate> path,
            bool reversed,
            FixedPoint progress,
            out TileCoordinate tile,
            out TileCoordinate next,
            out FixedPoint fraction)
        {
            tile = default;
            next = default;
            fraction = FixedPoint.Zero;
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

            tile = At(path, reversed, (int)step);
            next = At(path, reversed, (int)step + 1);
            fraction = progress - FixedPoint.FromInt64(step);
            if (fraction > FixedPoint.FromInt64(1))
            {
                fraction = FixedPoint.FromInt64(1);
            }

            return true;
        }

        private static TileCoordinate At(IReadOnlyList<TileCoordinate> path, bool reversed, int index)
        {
            return reversed ? path[path.Count - 1 - index] : path[index];
        }
    }
}
