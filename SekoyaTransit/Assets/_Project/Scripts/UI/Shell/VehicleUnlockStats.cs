#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Stats of one vehicle type shown on its unlock card (Issue #286). For a train, <see cref="CargoCapacity"/> is the
    /// capacity of one wagon and <see cref="MaxWagons"/> the wagons its locomotive can pull; for a truck
    /// <see cref="MaxWagons"/> is 0.
    /// </summary>
    public readonly struct VehicleUnlockStats : IEquatable<VehicleUnlockStats>
    {
        /// <summary>
        /// Initializes the card stats.
        /// </summary>
        public VehicleUnlockStats(
            int cargoCapacity,
            int maxWagons,
            int speedTenthsTilesPerSecond,
            long purchase,
            long minUpkeepPerMonth,
            long maxUpkeepPerMonth)
        {
            CargoCapacity = cargoCapacity;
            MaxWagons = maxWagons;
            SpeedTenthsTilesPerSecond = speedTenthsTilesPerSecond;
            Purchase = purchase;
            MinUpkeepPerMonth = minUpkeepPerMonth;
            MaxUpkeepPerMonth = maxUpkeepPerMonth;
        }

        /// <summary>
        /// Gets the cargo capacity (per wagon for a train).
        /// </summary>
        public int CargoCapacity { get; }

        /// <summary>
        /// Gets the maximum wagons of a locomotive (0 for a truck).
        /// </summary>
        public int MaxWagons { get; }

        /// <summary>
        /// Gets the travel speed in tenths of a tile per second.
        /// </summary>
        public int SpeedTenthsTilesPerSecond { get; }

        /// <summary>
        /// Gets the purchase price.
        /// </summary>
        public long Purchase { get; }

        /// <summary>
        /// Gets the lowest monthly upkeep of one vehicle of this type: a truck's upkeep, or a train with 1 wagon
        /// (Issue #314). The card shows upkeep per vehicle only, never per unit of capacity.
        /// </summary>
        public long MinUpkeepPerMonth { get; }

        /// <summary>
        /// Gets the highest monthly upkeep of one vehicle of this type: a truck's upkeep (equal to
        /// <see cref="MinUpkeepPerMonth"/>), or a train with its maximum wagons (Issue #314).
        /// </summary>
        public long MaxUpkeepPerMonth { get; }

        /// <inheritdoc />
        public bool Equals(VehicleUnlockStats other)
        {
            return CargoCapacity == other.CargoCapacity
                && MaxWagons == other.MaxWagons
                && SpeedTenthsTilesPerSecond == other.SpeedTenthsTilesPerSecond
                && Purchase == other.Purchase
                && MinUpkeepPerMonth == other.MinUpkeepPerMonth
                && MaxUpkeepPerMonth == other.MaxUpkeepPerMonth;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is VehicleUnlockStats other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CargoCapacity;
                hash = (hash * 397) ^ MaxWagons;
                hash = (hash * 397) ^ SpeedTenthsTilesPerSecond;
                hash = (hash * 397) ^ Purchase.GetHashCode();
                hash = (hash * 397) ^ MinUpkeepPerMonth.GetHashCode();
                hash = (hash * 397) ^ MaxUpkeepPerMonth.GetHashCode();
                return hash;
            }
        }
    }
}
