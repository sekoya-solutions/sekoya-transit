#nullable enable
using System;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Adds explicit rail-vehicle wagon counts when upgrading format version 11 saves to version 12.
    /// Capacity-30 placeholder trains become three wagons; other capacities map as capacity / capacity-per-wagon.
    /// </summary>
    public sealed class SaveFormatV11ToV12RailWagonCountMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 11;
            }
        }

        /// <inheritdoc />
        public SaveSnapshot Migrate(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion != FromVersion)
            {
                throw new SaveFormatException(
                    $"Migration from version {FromVersion} received format version {snapshot.FormatVersion}.");
            }

            var migrated = new RailVehicle[snapshot.RailVehicles.Count];
            for (int index = 0; index < snapshot.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = snapshot.RailVehicles[index];
                int wagonCount = DeriveWagonCount(vehicle.CargoCapacity);
                int capacity = wagonCount * RailVehicle.LegacyCapacityPerWagon;
                var cargo = new CargoInventory(vehicle.Cargo.Stacks);
                cargo.TrimToMaxTotal(capacity);
                var orderCopy = new RailVehicleOrder[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderCopy[orderIndex] = vehicle.Orders[orderIndex];
                }

                migrated[index] = new RailVehicle(
                    vehicle.Id,
                    vehicle.TypeId,
                    orderCopy,
                    vehicle.OrderIndex,
                    vehicle.IsStuck,
                    vehicle.IsOnEdge,
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    vehicle.ProgressAlongEdge,
                    cargo.Stacks,
                    wagonCount,
                    capacity,
                    vehicle.IsServicingStop,
                    vehicle.IsUnloadComplete,
                    vehicle.LifetimeDeliveryIncome);
            }

            return snapshot.WithRailStationsAndVehicles(
                FromVersion + 1,
                snapshot.RailStations,
                migrated);
        }

        private static int DeriveWagonCount(int cargoCapacity)
        {
            if (cargoCapacity <= 0)
            {
                return 0;
            }

            int wagonCount = cargoCapacity / RailVehicle.LegacyCapacityPerWagon;
            if (wagonCount < 0)
            {
                return 0;
            }

            if (wagonCount > RailVehicle.LegacyMaxWagons)
            {
                return RailVehicle.LegacyMaxWagons;
            }

            return wagonCount;
        }
    }
}
