#nullable enable
using System;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Immutable definition of a road truck, rail locomotive, or shared rail wagon.
    /// </summary>
    public sealed class VehicleDefinition
    {
        /// <summary>
        /// Initializes a vehicle definition.
        /// </summary>
        public VehicleDefinition(
            string id,
            string nameKey,
            VehicleMode mode,
            long purchase,
            long upkeepPerMonth,
            int availableYear,
            long unlockCost,
            bool starting,
            int cargoCapacity = 0,
            int maxWagons = 0,
            int capacityPerWagon = 0,
            int speedThousandths = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Vehicle ID cannot be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Vehicle name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (purchase < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(purchase), purchase, "Purchase price must be positive.");
            }

            if (upkeepPerMonth < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(upkeepPerMonth),
                    upkeepPerMonth,
                    "Upkeep cannot be negative.");
            }

            if (availableYear < GameDate.FirstYear)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableYear),
                    availableYear,
                    "Available year cannot be before the calendar start.");
            }

            if (unlockCost < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(unlockCost), unlockCost, "Unlock cost cannot be negative.");
            }

            switch (mode)
            {
                case VehicleMode.Road:
                    if (cargoCapacity < 1)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(cargoCapacity),
                            cargoCapacity,
                            "Road vehicles require a positive cargo capacity.");
                    }

                    if (speedThousandths < 1)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(speedThousandths),
                            speedThousandths,
                            "Road vehicles require a positive speed.");
                    }

                    if (maxWagons != 0 || capacityPerWagon != 0)
                    {
                        throw new ArgumentException("Road vehicles must not set rail wagon fields.", nameof(mode));
                    }

                    break;

                case VehicleMode.Rail:
                    if (maxWagons < 1)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(maxWagons),
                            maxWagons,
                            "Locomotives require a positive max wagon count.");
                    }

                    if (capacityPerWagon < 1)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(capacityPerWagon),
                            capacityPerWagon,
                            "Locomotives require a positive capacity per wagon.");
                    }

                    if (speedThousandths < 1)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(speedThousandths),
                            speedThousandths,
                            "Locomotives require a positive speed.");
                    }

                    if (cargoCapacity != 0)
                    {
                        throw new ArgumentException("Locomotives must not set road cargoCapacity.", nameof(mode));
                    }

                    break;

                case VehicleMode.RailWagon:
                    if (cargoCapacity != 0 || maxWagons != 0 || capacityPerWagon != 0 || speedThousandths != 0)
                    {
                        throw new ArgumentException(
                            "Rail wagon entries must not set capacity, wagon, or speed fields.",
                            nameof(mode));
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Vehicle mode is not supported.");
            }

            Id = id;
            NameKey = nameKey;
            Mode = mode;
            Purchase = purchase;
            UpkeepPerMonth = upkeepPerMonth;
            AvailableYear = availableYear;
            UnlockCost = unlockCost;
            Starting = starting;
            CargoCapacity = cargoCapacity;
            MaxWagons = maxWagons;
            CapacityPerWagon = capacityPerWagon;
            SpeedThousandths = speedThousandths;
        }

        /// <summary>
        /// Gets the stable content ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the localization key for the display name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the vehicle mode.
        /// </summary>
        public VehicleMode Mode { get; }

        /// <summary>
        /// Gets the road cargo capacity, or 0 when not a road vehicle.
        /// </summary>
        public int CargoCapacity { get; }

        /// <summary>
        /// Gets the locomotive max wagon count, or 0 when not a locomotive.
        /// </summary>
        public int MaxWagons { get; }

        /// <summary>
        /// Gets capacity contributed by each wagon, or 0 when not a locomotive.
        /// </summary>
        public int CapacityPerWagon { get; }

        /// <summary>
        /// Gets speed in thousandths of a tile per tick, or 0 for rail wagons.
        /// </summary>
        public int SpeedThousandths { get; }

        /// <summary>
        /// Gets the full purchase price (or per-wagon attach price).
        /// </summary>
        public long Purchase { get; }

        /// <summary>
        /// Gets fixed monthly upkeep per owned instance (per wagon for rail wagons).
        /// </summary>
        public long UpkeepPerMonth { get; }

        /// <summary>
        /// Gets the calendar year when the type appears for unlock/purchase (Issue #63).
        /// </summary>
        public int AvailableYear { get; }

        /// <summary>
        /// Gets the one-time permanent unlock cost (Issue #63). Starting types use 0.
        /// </summary>
        public long UnlockCost { get; }

        /// <summary>
        /// Gets whether the type is free to purchase from game start (no unlock entry).
        /// </summary>
        public bool Starting { get; }

        /// <summary>
        /// Converts <see cref="SpeedThousandths"/> to a fixed-point travel speed.
        /// </summary>
        public FixedPoint SpeedPerTick
        {
            get
            {
                if (SpeedThousandths < 1)
                {
                    throw new InvalidOperationException($"Vehicle '{Id}' has no travel speed.");
                }

                return FixedPoint.FromRaw(checked(FixedPoint.OneRawValue * SpeedThousandths / 1000L));
            }
        }
    }
}
