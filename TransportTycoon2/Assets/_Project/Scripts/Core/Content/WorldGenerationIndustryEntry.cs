#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// How many instances of one industry a map size places and how close a consumer must be to a producer (Issue #67).
    /// </summary>
    public sealed class WorldGenerationIndustryEntry
    {
        /// <summary>
        /// Initializes an industry entry for one map size.
        /// </summary>
        /// <param name="industryId">The industry content ID from <c>industries.json</c>.</param>
        /// <param name="count">The exact number of instances to place.</param>
        /// <param name="maximumProducerDistance">
        /// The inclusive maximum Chebyshev distance to an input producer; zero for a primary industry.
        /// </param>
        public WorldGenerationIndustryEntry(string industryId, int count, int maximumProducerDistance)
        {
            if (string.IsNullOrWhiteSpace(industryId))
            {
                throw new ArgumentException("Industry ID cannot be null or whitespace.", nameof(industryId));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Industry count must be at least 1.");
            }

            if (maximumProducerDistance < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumProducerDistance),
                    maximumProducerDistance,
                    "Maximum producer distance cannot be negative.");
            }

            IndustryId = industryId;
            Count = count;
            MaximumProducerDistance = maximumProducerDistance;
        }

        /// <summary>
        /// Gets the industry content ID.
        /// </summary>
        public string IndustryId { get; }

        /// <summary>
        /// Gets the exact number of instances to place.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the inclusive maximum Chebyshev distance to an input producer, in tiles.
        /// </summary>
        public int MaximumProducerDistance { get; }
    }
}
