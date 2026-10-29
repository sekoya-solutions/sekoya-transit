#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Grows primary (level 1) industries that are well served (Issue #73). When a new calendar month starts, each
    /// primary industry looks at the month that just ended: if it transported at least the configured share of what it
    /// produced, its well-served streak grows, otherwise the streak resets. A full streak raises the production level
    /// by one (up to the maximum) and starts a new streak. Production never shrinks. Processing industries keep
    /// producing only from delivered input.
    /// </summary>
    public static class IndustryGrowth
    {
        /// <summary>
        /// Evaluates the finished month of every primary industry whose stats still belong to an earlier month, then
        /// starts its stats for <paramref name="date"/>. Must run before anything else touches industry stats in the
        /// tick, so the finished month is read before it resets.
        /// </summary>
        public static void AdvanceAll(GeneratedWorld world, ContentCatalog catalog, GameDate date)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            IndustryGrowthSettings growth = catalog.Economy.IndustryGrowth;
            if (growth.MaxLevel <= 0)
            {
                return;
            }

            for (int index = 0; index < world.Industries.Count; index++)
            {
                PlacedIndustry industry = world.Industries[index];
                if (industry.StatsYear == 0 ||
                    (industry.StatsYear == date.Year && industry.StatsMonth == date.Month))
                {
                    continue;
                }

                if (!catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) ||
                    definition == null ||
                    definition.Inputs.Count > 0)
                {
                    continue;
                }

                CloseMonth(industry, growth);
                industry.EnsureMonthStats(date);
            }
        }

        /// <summary>
        /// Returns whether a month with these stats counts as well served.
        /// </summary>
        public static bool IsWellServed(long produced, long transported, IndustryGrowthSettings growth)
        {
#pragma warning disable CA1510
            if (growth == null)
            {
                throw new ArgumentNullException(nameof(growth));
            }
#pragma warning restore CA1510

            return produced > 0L && transported * 100L >= produced * growth.ServedPercent;
        }

        /// <summary>
        /// Applies the growth rule to a primary industry whose month stats are complete: a well-served month extends the
        /// streak (a full streak gains a level and restarts), any other month resets it. The stats are left unchanged.
        /// </summary>
        public static void CloseMonth(PlacedIndustry industry, IndustryGrowthSettings growth)
        {
#pragma warning disable CA1510
            if (industry == null)
            {
                throw new ArgumentNullException(nameof(industry));
            }

            if (growth == null)
            {
                throw new ArgumentNullException(nameof(growth));
            }
#pragma warning restore CA1510

            if (!IsWellServed(industry.MonthProduced, industry.MonthTransported, growth))
            {
                industry.SetGrowth(industry.ProductionLevel, 0);
                return;
            }

            if (industry.ProductionLevel >= growth.MaxLevel)
            {
                industry.SetGrowth(industry.ProductionLevel, 0);
                return;
            }

            int streak = industry.ServedMonths + 1;
            if (streak >= growth.MonthsToGrow)
            {
                industry.SetGrowth(industry.ProductionLevel + 1, 0);
                return;
            }

            industry.SetGrowth(industry.ProductionLevel, streak);
        }
    }
}
