#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing copy of the town growth values from game data, shown as the rules text of the towns panel
    /// (Issue #242). Index 0 of each array is level 1 (raw goods), index 2 level 3 (finished goods).
    /// </summary>
    public sealed class TownGrowthRulesInfo
    {
        private readonly int[] _partlyMet;
        private readonly int[] _fullyMet;

        /// <summary>
        /// Initializes the rules from the per-level partly and fully met growth and the all-needs multiplier.
        /// </summary>
        public TownGrowthRulesInfo(
            int partlyMetLevel1,
            int partlyMetLevel2,
            int partlyMetLevel3,
            int fullyMetLevel1,
            int fullyMetLevel2,
            int fullyMetLevel3,
            int allNeedsMetMultiplier)
        {
            _partlyMet = new[] { partlyMetLevel1, partlyMetLevel2, partlyMetLevel3 };
            _fullyMet = new[] { fullyMetLevel1, fullyMetLevel2, fullyMetLevel3 };
            for (int index = 0; index < TownNeedListLine.MaxLevel; index++)
            {
                if (_partlyMet[index] < 0 || _fullyMet[index] < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(partlyMetLevel1),
                        "Growth amounts cannot be negative.");
                }
            }

            if (allNeedsMetMultiplier < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allNeedsMetMultiplier),
                    allNeedsMetMultiplier,
                    "All needs met multiplier must be at least 1.");
            }

            AllNeedsMetMultiplier = allNeedsMetMultiplier;
        }

        /// <summary>
        /// Gets the multiplier applied to a month's growth when every need is fully met.
        /// </summary>
        public int AllNeedsMetMultiplier { get; }

        /// <summary>
        /// Returns the monthly growth of a partly met need of a level from 1 to 3.
        /// </summary>
        public int GetPartlyMet(int level)
        {
            return _partlyMet[ToIndex(level)];
        }

        /// <summary>
        /// Returns the monthly growth of a fully met need of a level from 1 to 3.
        /// </summary>
        public int GetFullyMet(int level)
        {
            return _fullyMet[ToIndex(level)];
        }

        private static int ToIndex(int level)
        {
            if (level < TownNeedListLine.MinLevel || level > TownNeedListLine.MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Need level must be 1, 2 or 3.");
            }

            return level - 1;
        }
    }
}
