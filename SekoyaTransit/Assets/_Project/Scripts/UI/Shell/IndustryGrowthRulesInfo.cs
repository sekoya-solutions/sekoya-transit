#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing copy of the industry growth values from game data, shown as the rules text of the industries panel
    /// (Issue #282).
    /// </summary>
    public sealed class IndustryGrowthRulesInfo
    {
        /// <summary>
        /// Initializes the rules from the served share, the streak length, the per-level multiplier and the number of
        /// growth levels shown to the player (the highest level, counted from 1).
        /// </summary>
        public IndustryGrowthRulesInfo(int servedPercent, int monthsToGrow, int levelMultiplier, int levelCount)
        {
            if (servedPercent < 1 || servedPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(servedPercent), servedPercent, "Served percent must be 1-100.");
            }

            if (monthsToGrow < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsToGrow), monthsToGrow, "Months to grow must be at least 1.");
            }

            if (levelMultiplier < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelMultiplier),
                    levelMultiplier,
                    "Level multiplier must be at least 1.");
            }

            if (levelCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCount), levelCount, "Level count must be at least 1.");
            }

            ServedPercent = servedPercent;
            MonthsToGrow = monthsToGrow;
            LevelMultiplier = levelMultiplier;
            LevelCount = levelCount;
        }

        /// <summary>
        /// Gets the share of a month's production that must be delivered, in percent.
        /// </summary>
        public int ServedPercent { get; }

        /// <summary>
        /// Gets the consecutive months needed for the next growth level.
        /// </summary>
        public int MonthsToGrow { get; }

        /// <summary>
        /// Gets the factor applied to production and stock capacity per growth level.
        /// </summary>
        public int LevelMultiplier { get; }

        /// <summary>
        /// Gets the highest growth level, counted from 1.
        /// </summary>
        public int LevelCount { get; }
    }
}
