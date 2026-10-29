#nullable enable
using System;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Computes the V1 company score on demand: company cash plus the resale value of every owned vehicle.
    /// Roads, rails, stations, vehicle unlocks, cargo, towns, and industries are not counted. The score adds
    /// no simulation state.
    /// </summary>
    public static class CompanyScore
    {
        /// <summary>
        /// Returns company cash plus <see cref="ComputeVehicleResaleValue"/>.
        /// </summary>
        public static long Compute(GeneratedWorld world, ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            return checked(world.CompanyCash + ComputeVehicleResaleValue(world, catalog));
        }

        /// <summary>
        /// Sums what selling every owned road and rail vehicle would refund, using the same refund rules
        /// as the sell commands and the current catalog prices.
        /// </summary>
        public static long ComputeVehicleResaleValue(GeneratedWorld world, ContentCatalog catalog)
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

                total = checked(total + EconomySettings.HalfRefund(definition.Purchase));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                total = checked(total + catalog.SellRefundForRail(vehicle.TypeId, vehicle.WagonCount));
            }

            return total;
        }
    }
}
