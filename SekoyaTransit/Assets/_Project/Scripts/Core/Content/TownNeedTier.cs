#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Need counts by manufacturing level that apply once population reaches <see cref="MinPopulation"/>.
    /// </summary>
    public readonly struct TownNeedTier
    {
        /// <summary>
        /// Initializes a growth-tier row.
        /// </summary>
        public TownNeedTier(int minPopulation, int level1Count, int level2Count, int level3Count)
        {
            if (minPopulation < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minPopulation),
                    minPopulation,
                    "Minimum population cannot be negative.");
            }

            if (level1Count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level1Count), level1Count, "Need counts cannot be negative.");
            }

            if (level2Count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level2Count), level2Count, "Need counts cannot be negative.");
            }

            if (level3Count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level3Count), level3Count, "Need counts cannot be negative.");
            }

            MinPopulation = minPopulation;
            Level1Count = level1Count;
            Level2Count = level2Count;
            Level3Count = level3Count;
        }

        /// <summary>
        /// Gets the inclusive population threshold for this tier.
        /// </summary>
        public int MinPopulation { get; }

        /// <summary>
        /// Gets the number of manufacturing-level-1 needs.
        /// </summary>
        public int Level1Count { get; }

        /// <summary>
        /// Gets the number of manufacturing-level-2 needs.
        /// </summary>
        public int Level2Count { get; }

        /// <summary>
        /// Gets the number of manufacturing-level-3 needs.
        /// </summary>
        public int Level3Count { get; }
    }
}
