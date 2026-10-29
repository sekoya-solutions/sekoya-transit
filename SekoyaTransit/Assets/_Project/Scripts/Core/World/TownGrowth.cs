#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Applies calendar-month town need rollover and population growth from satisfied needs.
    /// </summary>
    public static class TownGrowth
    {
        /// <summary>
        /// For each town: if the calendar month changed, grow from the completed month's
        /// satisfied needs, then reset supply counters for the new month.
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
                int growth = town.BeginNewMonth(
                    date,
                    settings.DemandPerNeedPerMonth,
                    settings.PopulationPerSatisfiedNeed);
                if (growth <= 0)
                {
                    continue;
                }

                int newPopulation = town.Population + growth;
                if (newPopulation > settings.MaxPopulation)
                {
                    newPopulation = settings.MaxPopulation;
                }

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
    }
}
