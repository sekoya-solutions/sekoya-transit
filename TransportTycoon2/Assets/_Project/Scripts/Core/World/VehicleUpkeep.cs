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
        public static long AdvanceAll(GeneratedWorld world, EconomySettings economy, GameDate date)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }
#pragma warning restore CA1510

            if (world.LastUpkeepYear == date.Year && world.LastUpkeepMonth == date.Month)
            {
                return 0L;
            }

            long charged = 0L;
            if (world.LastUpkeepYear != 0 && world.LastUpkeepMonth != 0)
            {
                long total = ComputeMonthlyUpkeep(world, economy);
                charged = world.DebitCompanyCashClamped(total);
            }

            world.SetLastUpkeepMonth(date.Year, date.Month);
            return charged;
        }

        /// <summary>
        /// Sums monthly upkeep for all owned road and rail vehicles.
        /// </summary>
        public static long ComputeMonthlyUpkeep(GeneratedWorld world, EconomySettings economy)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }
#pragma warning restore CA1510

            long total = checked(economy.RoadVehicleUpkeepPerMonth * world.RoadVehicles.Count);
            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                total = checked(total + economy.MonthlyUpkeepForRail(world.RailVehicles[index].WagonCount));
            }

            return total;
        }
    }
}
