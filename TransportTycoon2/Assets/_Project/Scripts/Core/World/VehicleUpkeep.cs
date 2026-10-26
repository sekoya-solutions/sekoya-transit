#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deducts fixed per-vehicle monthly upkeep when the calendar month changes.
    /// </summary>
    public static class VehicleUpkeep
    {
        /// <summary>
        /// Charges every owned truck, locomotive, and wagon when <paramref name="date"/> enters a
        /// new calendar month. The first observation of a month records it without charging.
        /// Cash is clamped at zero when upkeep exceeds the balance.
        /// </summary>
        public static long AdvanceAll(GeneratedWorld world, ContentCatalog catalog, GameDate date)
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

            if (world.LastUpkeepYear == date.Year && world.LastUpkeepMonth == date.Month)
            {
                return 0L;
            }

            long charged = 0L;
            if (world.LastUpkeepYear != 0 && world.LastUpkeepMonth != 0)
            {
                long total = ComputeMonthlyUpkeep(world, catalog);
                charged = world.DebitCompanyCashClamped(total, FinanceCategory.RunningCosts);
            }

            world.SetLastUpkeepMonth(date.Year, date.Month);
            return charged;
        }

        /// <summary>
        /// Sums monthly upkeep for all owned road and rail vehicles from their catalog types.
        /// </summary>
        public static long ComputeMonthlyUpkeep(GeneratedWorld world, ContentCatalog catalog)
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

            long total = 0L;
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                if (!catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? definition) ||
                    definition == null ||
                    definition.Mode != VehicleMode.Road)
                {
                    throw new InvalidOperationException(
                        $"Road vehicle {vehicle.Id} has unknown type '{vehicle.TypeId}'.");
                }

                total = checked(total + definition.UpkeepPerMonth);
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                total = checked(total + catalog.MonthlyUpkeepForRail(vehicle.TypeId, vehicle.WagonCount));
            }

            return total;
        }
    }
}
