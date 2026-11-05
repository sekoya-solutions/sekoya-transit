#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Applies calendar-month town need rollover and population growth from met needs (Issue #242).
    /// </summary>
    public static class TownGrowth
    {
        /// <summary>
        /// For each town: if the calendar month changed, roll the supply counters, then grow from the completed
        /// month's partly and fully met needs.
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

            TownNeedsSettings settings = catalog.TownNeeds;
            bool anyNeedsChanged = false;
            for (int index = 0; index < world.Towns.Count; index++)
            {
                Town town = world.Towns[index];
                bool hadStatsMonth = town.StatsYear != 0;
                if (!town.RollToMonth(date))
                {
                    continue;
                }

                int growth = hadStatsMonth ? ComputeGrowth(town.Needs, settings, catalog) : 0;
                int newPopulation = town.Population + growth;
                if (newPopulation > settings.MaxPopulation)
                {
                    newPopulation = settings.MaxPopulation;
                }

                if (newPopulation < town.Population)
                {
                    newPopulation = town.Population;
                }

                town.SetLastMonthGrowth(newPopulation - town.Population);
                if (newPopulation == town.Population)
                {
                    continue;
                }

                world.SetTownPopulation(index, newPopulation, settings, catalog);
                anyNeedsChanged = true;
            }

            if (anyNeedsChanged)
            {
                VehicleRouteCargoFilter.RefreshAll(world, catalog);
            }
        }

        /// <summary>
        /// Returns the month-end growth from each need's <see cref="TownNeed.LastMonthSupply"/>: a fully met need (the
        /// demand delivered) adds its level's full amount, a partly met need its fixed partial amount, an unmet need
        /// nothing (see <see cref="GetStatus"/>), and when every need is fully met the total is multiplied by the all-needs bonus. Needs whose cargo is not a
        /// level 1–3 freight cargo in <paramref name="catalog"/> add nothing.
        /// </summary>
        public static int ComputeGrowth(IReadOnlyList<TownNeed> needs, TownNeedsSettings settings, ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (needs == null)
            {
                throw new ArgumentNullException(nameof(needs));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            int growth = 0;
            bool allFullyMet = needs.Count > 0;
            for (int index = 0; index < needs.Count; index++)
            {
                TownNeed need = needs[index];
                bool isFreightLevel = catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo) &&
                    cargo != null &&
                    cargo.ManufacturingLevel >= 1 &&
                    cargo.ManufacturingLevel <= TownNeedPlan.MaxLevel;
                int level = isFreightLevel ? cargo!.ManufacturingLevel : 1;
                TownNeedStatus status = GetStatus(
                    need.LastMonthSupply,
                    settings.DemandPerNeedPerMonth.Get(level),
                    settings.PartlyMetMinDemandPercent);
                if (status != TownNeedStatus.FullyMet)
                {
                    allFullyMet = false;
                }

                if (status == TownNeedStatus.Unmet || !isFreightLevel)
                {
                    continue;
                }

                TownNeedLevelAmounts amounts = status == TownNeedStatus.FullyMet
                    ? settings.PopulationPerFullyMetNeed
                    : settings.PopulationPerPartlyMetNeed;
                growth = checked(growth + amounts.Get(level));
            }

            return allFullyMet ? checked(growth * settings.AllNeedsMetGrowthMultiplier) : growth;
        }

        /// <summary>
        /// Classifies a month's supply against the monthly demand: fully met from the whole demand, partly met from
        /// <paramref name="partlyMetMinDemandPercent"/> percent of it, unmet below that (Issue #242, Issue #310).
        /// </summary>
        public static TownNeedStatus GetStatus(int supply, int demand, int partlyMetMinDemandPercent)
        {
            if (demand < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(demand), demand, "Demand must be positive.");
            }

            if (partlyMetMinDemandPercent < 1 || partlyMetMinDemandPercent > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(partlyMetMinDemandPercent),
                    partlyMetMinDemandPercent,
                    "The partly met share of the demand must be from 1 to 100 percent.");
            }

            if (supply >= demand)
            {
                return TownNeedStatus.FullyMet;
            }

            // Whole numbers only, so the result is the same on every platform; the long keeps large values from overflowing.
            return supply * 100L >= demand * (long)partlyMetMinDemandPercent ? TownNeedStatus.PartlyMet : TownNeedStatus.Unmet;
        }
    }
}
