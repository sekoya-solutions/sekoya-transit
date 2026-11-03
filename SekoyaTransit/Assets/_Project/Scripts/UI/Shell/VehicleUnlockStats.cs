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
            long upkeepPerMonth)
        {
            CargoCapacity = cargoCapacity;
            MaxWagons = maxWagons;
            SpeedTenthsTilesPerSecond = speedTenthsTilesPerSecond;
            Purchase = purchase;
            UpkeepPerMonth = upkeepPerMonth;
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
        /// Gets the upkeep per month.
        /// </summary>
        public long UpkeepPerMonth { get; }

        /// <inheritdoc />
        public bool Equals(VehicleUnlockStats other)
        {
            return CargoCapacity == other.CargoCapacity
                && MaxWagons == other.MaxWagons
                && SpeedTenthsTilesPerSecond == other.SpeedTenthsTilesPerSecond
                && Purchase == other.Purchase
                && UpkeepPerMonth == other.UpkeepPerMonth;
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
                hash = (hash * 397) ^ UpkeepPerMonth.GetHashCode();
                return hash;
            }
        }
    }
}
