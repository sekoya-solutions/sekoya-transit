#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Charges vehicle upkeep day by day to the route each vehicle runs on, and closes the route finance month
    /// (Issue #229). Unassigned vehicles are off the map and cost nothing.
    /// </summary>
    public static class VehicleUpkeep
    {
        /// <summary>
        /// On the first tick of each calendar day: closes every route's month when the month changed, then charges
        /// each vehicle on the map its share of monthly upkeep for that day. A day's share is
        /// <c>floor(U × d / D) − floor(U × (d − 1) / D)</c> for monthly upkeep U on day d of a D-day month, so a full
        /// month adds up exactly to U. Cash is clamped at zero when upkeep exceeds the balance.
        /// </summary>
        /// <returns>The amount charged on this tick.</returns>
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

            if (world.LastUpkeepYear == date.Year && world.LastUpkeepMonth == date.Month && world.LastUpkeepDay == date.Day)
            {
                return 0L;
            }

            if (world.LastUpkeepYear != 0 && (world.LastUpkeepYear != date.Year || world.LastUpkeepMonth != date.Month))
            {
                for (int index = 0; index < world.Routes.Count; index++)
                {
                    world.Routes[index].CloseMonth();
                }
            }

            long charged = 0L;
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                charged = checked(charged + Charge(world, vehicle.RouteId, GetMonthlyUpkeep(catalog, vehicle), date));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                charged = checked(charged + Charge(world, vehicle.RouteId, GetMonthlyUpkeep(catalog, vehicle), date));
            }

            world.SetLastUpkeepDate(date.Year, date.Month, date.Day);
            return charged;
        }

        /// <summary>
        /// Returns the share of <paramref name="monthlyUpkeep"/> due on <paramref name="date"/>'s day.
        /// </summary>
        public static long GetDailyShare(long monthlyUpkeep, GameDate date)
        {
            if (monthlyUpkeep < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(monthlyUpkeep), monthlyUpkeep, "Upkeep cannot be negative.");
            }

            long daysInMonth = date.DaysInMonth;
            return (checked(monthlyUpkeep * date.Day) / daysInMonth) - (checked(monthlyUpkeep * (date.Day - 1)) / daysInMonth);
        }

        /// <summary>
        /// Returns the catalog monthly upkeep of a truck.
        /// </summary>
        public static long GetMonthlyUpkeep(ContentCatalog catalog, RoadVehicle vehicle)
        {
#pragma warning disable CA1510
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            if (!catalog.TryGetVehicle(vehicle.TypeId, out VehicleDefinition? definition) ||
                definition == null ||
                definition.Mode != VehicleMode.Road)
            {
                throw new InvalidOperationException($"Road vehicle {vehicle.Id} has unknown type '{vehicle.TypeId}'.");
            }

            return definition.UpkeepPerMonth;
        }

        /// <summary>
        /// Returns the catalog monthly upkeep of a train: its locomotive plus its wagons.
        /// </summary>
        public static long GetMonthlyUpkeep(ContentCatalog catalog, RailVehicle vehicle)
        {
#pragma warning disable CA1510
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            return catalog.MonthlyUpkeepForRail(vehicle.TypeId, vehicle.WagonCount);
        }

        private static long Charge(GeneratedWorld world, int routeId, long monthlyUpkeep, GameDate date)
        {
            long charged = world.DebitCompanyCashClamped(GetDailyShare(monthlyUpkeep, date), FinanceCategory.RunningCosts);
            if (charged > 0L && world.TryGetRoute(routeId, out Route? route) && route != null)
            {
                route.AddRunningCost(charged);
            }

            return charged;
        }
    }
}
