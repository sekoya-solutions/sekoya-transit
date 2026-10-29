#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Generates deterministic industry anchor coordinates that satisfy configured chain constraints.
    /// </summary>
    public static class IndustryPlacement
    {
        private const ulong _randomSequence = 0x494E445553545259UL;

        /// <summary>
        /// Generates industry anchors in level, industry-ID, and instance order.
        /// </summary>
        /// <param name="settings">The data-driven placement rules.</param>
        /// <param name="seed">The deterministic placement seed.</param>
        /// <returns>Exactly the configured industry instances.</returns>
        /// <exception cref="InvalidOperationException">
        /// The seeded candidate order cannot satisfy the configured placement constraints.
        /// </exception>
        public static PlacedIndustry[] Generate(IndustryPlacementSettings settings, ulong seed)
        {
            settings.Validate();

            int sideLength = (int)settings.MapSize;
            int tileCount = checked(sideLength * sideLength);
            int bucketCount = ((sideLength - 1) / settings.MinimumIndustryChebyshevDistance) + 1;
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

            IndustryPlacementRule[] rules = CopyAndOrderRules(settings.IndustryRules);
            var occupiedBuckets = new int[bucketCount, bucketCount];
            var occupiedTileIds = new bool[tileCount];
            foreach (TileCoordinate coordinate in settings.OccupiedTileCoordinates)
            {
                int tileId = (coordinate.Y * sideLength) + coordinate.X;
                occupiedTileIds[tileId] = true;
            }

            for (int y = 0; y < bucketCount; y++)
            {
                for (int x = 0; x < bucketCount; x++)
                {
                    occupiedBuckets[x, y] = -1;
                }
            }

            var producerTileIdsByCargo = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var placements = new List<PlacedIndustry>();
            foreach (IndustryPlacementRule rule in rules)
            {
                var producerPrefixSums = BuildProducerPrefixSums(rule, producerTileIdsByCargo, sideLength);
                int candidateStart = random.NextInt32(0, candidates.Length);
                int candidatesExamined = 0;

                for (int instanceIndex = 0; instanceIndex < rule.InstanceCount; instanceIndex++)
                {
                    bool placed = false;
                    while (candidatesExamined < candidates.Length)
                    {
                        int candidateIndex = (candidateStart + candidatesExamined) % candidates.Length;
                        int tileId = candidates[candidateIndex];
                        candidatesExamined++;
                        if (occupiedTileIds[tileId])
                        {
                            continue;
                        }

                        int x = tileId % sideLength;
                        int y = tileId / sideLength;
                        if (!SiteFootprint.Fits(new TileCoordinate(x, y), sideLength))
                        {
                            continue;
                        }

                        int bucketX = x / settings.MinimumIndustryChebyshevDistance;
                        int bucketY = y / settings.MinimumIndustryChebyshevDistance;
                        if (!IsSpacedFromPlacedIndustries(
                            x,
                            y,
                            bucketX,
                            bucketY,
                            sideLength,
                            settings.MinimumIndustryChebyshevDistance,
                            occupiedBuckets))
                        {
                            continue;
                        }

                        if (!IsSpacedFromOccupiedSites(
                            x,
                            y,
                            settings.OccupiedTileCoordinates))
                        {
                            continue;
                        }

                        if (!HasNearbyProducerForEveryInput(
                            rule,
                            producerPrefixSums,
                            x,
                            y,
                            sideLength))
                        {
                            continue;
                        }

                        occupiedBuckets[bucketX, bucketY] = tileId;
                        occupiedTileIds[tileId] = true;
                        placements.Add(new PlacedIndustry(rule.IndustryId, instanceIndex, new TileCoordinate(x, y)));
                        placed = true;
                        break;
                    }

                    if (!placed)
                    {
                        throw new InvalidOperationException(
                            $"The seeded candidate order could not place industry '{rule.IndustryId}' instance {instanceIndex} within its configured constraints.");
                    }
                }

                AddProducerTileIds(rule, placements, producerTileIdsByCargo, sideLength);
            }

            return placements.ToArray();
        }

        private static IndustryPlacementRule[] CopyAndOrderRules(IReadOnlyList<IndustryPlacementRule> source)
        {
            var rules = new IndustryPlacementRule[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                rules[index] = source[index];
            }

            Array.Sort(rules, CompareRules);
            return rules;
        }

        private static int CompareRules(IndustryPlacementRule left, IndustryPlacementRule right)
        {
            int levelOrder = left.Level.CompareTo(right.Level);
            return levelOrder != 0
                ? levelOrder
                : StringComparer.Ordinal.Compare(left.IndustryId, right.IndustryId);
        }

        private static Dictionary<string, int[]> BuildProducerPrefixSums(
            IndustryPlacementRule rule,
            Dictionary<string, List<int>> producerTileIdsByCargo,
            int sideLength)
        {
            var prefixSumsByCargo = new Dictionary<string, int[]>(StringComparer.Ordinal);
            foreach (string inputCargoId in rule.InputCargoIds)
            {
                prefixSumsByCargo.Add(
                    inputCargoId,
                    BuildProducerPrefixSum(
                        producerTileIdsByCargo[inputCargoId],
                        sideLength));
            }

            return prefixSumsByCargo;
        }

        private static int[] BuildProducerPrefixSum(List<int> producerTileIds, int sideLength)
        {
            int prefixWidth = sideLength + 1;
            var producerAtTile = new bool[checked(sideLength * sideLength)];
            foreach (int tileId in producerTileIds)
            {
                producerAtTile[tileId] = true;
            }

            var prefixSum = new int[checked(prefixWidth * prefixWidth)];
            for (int y = 1; y <= sideLength; y++)
            {
                int rowSum = 0;
                for (int x = 1; x <= sideLength; x++)
                {
                    if (producerAtTile[((y - 1) * sideLength) + (x - 1)])
                    {
                        rowSum++;
                    }

                    prefixSum[(y * prefixWidth) + x] =
                        prefixSum[((y - 1) * prefixWidth) + x] + rowSum;
                }
            }

            return prefixSum;
        }

        private static bool IsSpacedFromOccupiedSites(
            int x,
            int y,
            IReadOnlyList<TileCoordinate> occupiedTileCoordinates)
        {
            var candidate = new TileCoordinate(x, y);
            for (int index = 0; index < occupiedTileCoordinates.Count; index++)
            {
                if (!StationAdjacency.SatisfiesMinimumSiteSeparation(
                        candidate,
                        occupiedTileCoordinates[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSpacedFromPlacedIndustries(
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
                    if (existingTileId < 0)
                    {
                        continue;
                    }

                    int existingX = existingTileId % sideLength;
                    int existingY = existingTileId / sideLength;
                    if (Math.Max(Math.Abs(x - existingX), Math.Abs(y - existingY)) <
                        minimumDistance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasNearbyProducerForEveryInput(
            IndustryPlacementRule rule,
            Dictionary<string, int[]> producerPrefixSums,
            int x,
            int y,
            int sideLength)
        {
            foreach (string inputCargoId in rule.InputCargoIds)
            {
                if (!HasProducerWithinDistance(
                    producerPrefixSums[inputCargoId],
                    x,
                    y,
                    sideLength,
                    rule.MaximumProducerChebyshevDistance))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasProducerWithinDistance(
            int[] prefixSum,
            int x,
            int y,
            int sideLength,
            int maximumDistance)
        {
            int minX = maximumDistance >= x ? 0 : x - maximumDistance;
            int maxX = maximumDistance >= sideLength - 1 - x ? sideLength - 1 : x + maximumDistance;
            int minY = maximumDistance >= y ? 0 : y - maximumDistance;
            int maxY = maximumDistance >= sideLength - 1 - y ? sideLength - 1 : y + maximumDistance;
            int prefixWidth = sideLength + 1;
            int producerCount =
                prefixSum[((maxY + 1) * prefixWidth) + maxX + 1] -
                prefixSum[(minY * prefixWidth) + maxX + 1] -
                prefixSum[((maxY + 1) * prefixWidth) + minX] +
                prefixSum[(minY * prefixWidth) + minX];

            return producerCount > 0;
        }

        private static void AddProducerTileIds(
            IndustryPlacementRule rule,
            List<PlacedIndustry> placements,
            Dictionary<string, List<int>> producerTileIdsByCargo,
            int sideLength)
        {
            int firstIndustryPlacement = placements.Count - rule.InstanceCount;
            for (int placementIndex = firstIndustryPlacement; placementIndex < placements.Count; placementIndex++)
            {
                PlacedIndustry placement = placements[placementIndex];
                int tileId = (placement.Coordinate.Y * sideLength) + placement.Coordinate.X;
                foreach (string outputCargoId in rule.OutputCargoIds)
                {
                    if (!producerTileIdsByCargo.TryGetValue(outputCargoId, out List<int>? producerTileIds))
                    {
                        producerTileIds = new List<int>();
                        producerTileIdsByCargo.Add(outputCargoId, producerTileIds);
                    }

                    producerTileIds.Add(tileId);
                }
            }
        }
    }
}
