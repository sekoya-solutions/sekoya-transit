#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven growth of primary (level 1) industries (Issue #73, Issue #282): an industry that delivers at least
    /// <see cref="ServedPercent"/> of its monthly production for <see cref="MonthsToGrow"/> months in a row gains one
    /// production tier, up to <see cref="MaxLevel"/>. Each tier multiplies the output and the stock capacity of the
    /// previous tier by <see cref="LevelMultiplier"/>. Tiers are zero-based here and shown to the player from 1.
    /// </summary>
    public sealed class IndustryGrowthSettings
    {
        /// <summary>
        /// No growth: production stays at the base level (the rule before Issue #73, kept for focused tests).
        /// </summary>
        public static readonly IndustryGrowthSettings Disabled = new IndustryGrowthSettings(100, 1, 0, 1);

        /// <summary>
        /// Initializes a growth rule.
        /// </summary>
        /// <param name="servedPercent">Share of a month's production that must be transported, 1–100.</param>
        /// <param name="monthsToGrow">Consecutive well-served months needed for the next level.</param>
        /// <param name="maxLevel">Highest zero-based production tier; 0 disables growth.</param>
        /// <param name="levelMultiplier">Factor applied to the output and stock capacity per tier, at least 1.</param>
        public IndustryGrowthSettings(int servedPercent, int monthsToGrow, int maxLevel, int levelMultiplier)
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

            if (levelMultiplier < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelMultiplier),
                    levelMultiplier,
                    "Level multiplier must be at least 1.");
            }

            ServedPercent = servedPercent;
            MonthsToGrow = monthsToGrow;
            MaxLevel = maxLevel;
            LevelMultiplier = levelMultiplier;
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
        /// Gets the highest zero-based production tier; 0 disables growth.
        /// </summary>
        public int MaxLevel { get; }

        /// <summary>
        /// Gets the factor applied to the output and the stock capacity for each tier above the first.
        /// </summary>
        public int LevelMultiplier { get; }

        /// <summary>
        /// Returns the output per production pulse at a tier: base × multiplier^tier. The tier is capped at
        /// <see cref="MaxLevel"/>.
        /// </summary>
        public int ScaleOutput(int baseAmount, int level)
        {
            if (baseAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseAmount), baseAmount, "Base amount cannot be negative.");
            }

            return checked(baseAmount * GetFactor(level));
        }

        /// <summary>
        /// Returns the factor (multiplier^tier) that scales output and stock capacity at a tier, capped at
        /// <see cref="MaxLevel"/>; 1 at the first tier.
        /// </summary>
        public int GetFactor(int level)
        {
            int capped = level > MaxLevel ? MaxLevel : level;
            int factor = 1;
            for (int tier = 0; tier < capped; tier++)
            {
                factor = checked(factor * LevelMultiplier);
            }

            return factor;
        }
    }
}
