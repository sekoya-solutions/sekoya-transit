#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven growth of primary (level 1) industries (Issue #73): an industry that ships at least
    /// <see cref="ServedPercent"/> of its monthly production for <see cref="MonthsToGrow"/> months in a row gains one
    /// production level, up to <see cref="MaxLevel"/>. Each level adds <see cref="LevelStepPercent"/> of the base output.
    /// </summary>
    public sealed class IndustryGrowthSettings
    {
        /// <summary>
        /// No growth: production stays at the base level (the rule before Issue #73, kept for focused tests).
        /// </summary>
        public static readonly IndustryGrowthSettings Disabled = new IndustryGrowthSettings(100, 1, 0, 0);

        /// <summary>
        /// Initializes a growth rule.
        /// </summary>
        /// <param name="servedPercent">Share of a month's production that must be transported, 1–100.</param>
        /// <param name="monthsToGrow">Consecutive well-served months needed for the next level.</param>
        /// <param name="maxLevel">Highest production level; 0 disables growth.</param>
        /// <param name="levelStepPercent">Output added per level, in percent of the base output.</param>
        public IndustryGrowthSettings(int servedPercent, int monthsToGrow, int maxLevel, int levelStepPercent)
        {
            if (servedPercent < 1 || servedPercent > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(servedPercent),
                    servedPercent,
                    "Served percent must be between 1 and 100.");
            }

            if (monthsToGrow < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthsToGrow),
                    monthsToGrow,
                    "Months to grow must be at least 1.");
            }

            if (maxLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLevel), maxLevel, "Max level cannot be negative.");
            }

            if (levelStepPercent < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelStepPercent),
                    levelStepPercent,
                    "Level step percent cannot be negative.");
            }

            ServedPercent = servedPercent;
            MonthsToGrow = monthsToGrow;
            MaxLevel = maxLevel;
            LevelStepPercent = levelStepPercent;
        }

        /// <summary>
        /// Gets the share of a month's production that must be transported, in percent.
        /// </summary>
        public int ServedPercent { get; }

        /// <summary>
        /// Gets the consecutive well-served months needed for the next level.
        /// </summary>
        public int MonthsToGrow { get; }

        /// <summary>
        /// Gets the highest production level; 0 disables growth.
        /// </summary>
        public int MaxLevel { get; }

        /// <summary>
        /// Gets the output added per level, in percent of the base output.
        /// </summary>
        public int LevelStepPercent { get; }

        /// <summary>
        /// Returns the output per production pulse at a level: base × (100 + level × step) / 100, rounded down,
        /// never below the base amount.
        /// </summary>
        public int ScaleOutput(int baseAmount, int level)
        {
            if (baseAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseAmount), baseAmount, "Base amount cannot be negative.");
            }

            if (level <= 0)
            {
                return baseAmount;
            }

            long scaled = (long)baseAmount * (100L + ((long)level * LevelStepPercent)) / 100L;
            return checked((int)scaled);
        }
    }
}
