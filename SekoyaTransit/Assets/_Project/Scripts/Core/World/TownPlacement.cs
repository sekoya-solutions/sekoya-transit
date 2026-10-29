#nullable enable
using System;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Generates deterministic town anchor coordinates for a map.
    /// </summary>
    public static class TownPlacement
    {
        private const ulong _randomSequence = 0x544F574E504C4143UL;

        /// <summary>
        /// Generates town anchors by shuffling the row-major tile candidates and accepting
        /// positions that satisfy the configured minimum Chebyshev distance.
        /// </summary>
        /// <param name="settings">The data-driven map and placement constraints.</param>
        /// <param name="seed">The deterministic placement seed.</param>
        /// <returns>Town anchors in deterministic selection order.</returns>
        /// <exception cref="InvalidOperationException">
        /// The complete candidate list cannot satisfy the requested count and spacing.
        /// </exception>
        public static TileCoordinate[] Generate(TownPlacementSettings settings, ulong seed)
        {
            settings.Validate();
            if (settings.TownCount == 0)
            {
                return Array.Empty<TileCoordinate>();
            }

            int sideLength = (int)settings.MapSize;
            int tileCount = checked(sideLength * sideLength);
            var candidates = new int[tileCount];
            for (int tileId = 0; tileId < candidates.Length; tileId++)
            {
                candidates[tileId] = tileId;
            }

            var random = new Pcg32Random(seed, _randomSequence);
            for (int index = candidates.Length - 1; index > 0; index--)
            {
                int swapIndex = random.NextInt32(0, index + 1);
                int tileId = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = tileId;
            }

            int bucketCount = ((sideLength - 1) / settings.MinimumChebyshevDistance) + 1;
            var occupiedBuckets = new int[bucketCount, bucketCount];
            for (int y = 0; y < bucketCount; y++)
            {
                for (int x = 0; x < bucketCount; x++)
                {
                    occupiedBuckets[x, y] = -1;
                }
            }

            var towns = new TileCoordinate[settings.TownCount];
            int placedTownCount = 0;
            foreach (int tileId in candidates)
            {
                int x = tileId % sideLength;
                int y = tileId / sideLength;
                int bucketX = x / settings.MinimumChebyshevDistance;
                int bucketY = y / settings.MinimumChebyshevDistance;
                if (!IsSpacedFromPlacedTowns(
                    tileId,
                    x,
                    y,
                    bucketX,
                    bucketY,
                    sideLength,
                    settings.MinimumChebyshevDistance,
                    occupiedBuckets))
                {
                    continue;
                }

                towns[placedTownCount] = new TileCoordinate(x, y);
                occupiedBuckets[bucketX, bucketY] = tileId;
                placedTownCount++;
                if (placedTownCount == towns.Length)
                {
                    return towns;
                }
            }

            throw new InvalidOperationException(
                "The seeded candidate order could not satisfy the requested town count and minimum distance.");
        }

        private static bool IsSpacedFromPlacedTowns(
            int tileId,
            int x,
            int y,
            int bucketX,
            int bucketY,
            int sideLength,
            int minimumDistance,
            int[,] occupiedBuckets)
        {
            int firstBucketX = Math.Max(0, bucketX - 1);
            int lastBucketX = Math.Min(occupiedBuckets.GetLength(0) - 1, bucketX + 1);
            int firstBucketY = Math.Max(0, bucketY - 1);
            int lastBucketY = Math.Min(occupiedBuckets.GetLength(1) - 1, bucketY + 1);

            for (int neighborY = firstBucketY; neighborY <= lastBucketY; neighborY++)
            {
                for (int neighborX = firstBucketX; neighborX <= lastBucketX; neighborX++)
                {
                    int existingTileId = occupiedBuckets[neighborX, neighborY];
                    if (existingTileId < 0 || existingTileId == tileId)
                    {
                        continue;
                    }

                    int existingX = existingTileId % sideLength;
                    int existingY = existingTileId / sideLength;
                    int distanceX = Math.Abs(x - existingX);
                    int distanceY = Math.Abs(y - existingY);
                    if (Math.Max(distanceX, distanceY) < minimumDistance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
