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
        /// demand delivered) adds its level's full amount, a partly met need (at least one unit) its partial amount,
        /// and when every need is fully met the total is multiplied by the all-needs bonus. Needs whose cargo is not a
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
                TownNeedStatus status = GetStatus(need.LastMonthSupply, settings.DemandPerNeedPerMonth);
                if (status != TownNeedStatus.FullyMet)
                {
                    allFullyMet = false;
                }

                if (status == TownNeedStatus.Unmet ||
                    !catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo) ||
                    cargo == null ||
                    cargo.ManufacturingLevel < 1 ||
                    cargo.ManufacturingLevel > TownNeedPlan.MaxLevel)
                {
                    continue;
                }

                TownNeedLevelAmounts amounts = status == TownNeedStatus.FullyMet
                    ? settings.PopulationPerFullyMetNeed
                    : settings.PopulationPerPartlyMetNeed;
                growth = checked(growth + amounts.Get(cargo.ManufacturingLevel));
            }

            return allFullyMet ? checked(growth * settings.AllNeedsMetGrowthMultiplier) : growth;
        }

        /// <summary>
        /// Classifies a month's supply against the monthly demand (Issue #242).
        /// </summary>
        public static TownNeedStatus GetStatus(int supply, int demand)
        {
            if (demand < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(demand), demand, "Demand must be positive.");
            }

            if (supply >= demand)
            {
                return TownNeedStatus.FullyMet;
            }

            return supply > 0 ? TownNeedStatus.PartlyMet : TownNeedStatus.Unmet;
        }
    }
}
