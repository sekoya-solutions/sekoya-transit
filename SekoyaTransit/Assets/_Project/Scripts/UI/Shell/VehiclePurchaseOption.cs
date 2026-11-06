#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// One purchasable vehicle type offered in a route's vehicle window. A train type also lets the player choose its
    /// number of wagons, from 1 to <see cref="MaxWagons"/> (Issue #326).
    /// </summary>
    public readonly struct VehiclePurchaseOption : IEquatable<VehiclePurchaseOption>
    {
        private readonly bool _canAffordRoad;

        private VehiclePurchaseOption(
            string typeId,
            string nameKey,
            long basePrice,
            long wagonPrice,
            int maxWagons,
            int maxAffordableWagons,
            bool canAffordRoad)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Type id cannot be null or whitespace.", nameof(typeId));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (basePrice < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice), basePrice, "Base price must be positive.");
            }

            TypeId = typeId;
            NameKey = nameKey;
            BasePrice = basePrice;
            WagonPrice = wagonPrice;
            MaxWagons = maxWagons;
            MaxAffordableWagons = maxAffordableWagons;
            _canAffordRoad = canAffordRoad;
        }

        /// <summary>
        /// Gets the catalog type id to buy.
        /// </summary>
        public string TypeId { get; }

        /// <summary>
        /// Gets the localization key for the type name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets whether the option is a locomotive bought with wagons.
        /// </summary>
        public bool IsTrain
        {
            get
            {
                return MaxWagons > 0;
            }
        }

        /// <summary>
        /// Gets the price without wagons: the road vehicle, or the locomotive alone.
        /// </summary>
        public long BasePrice { get; }

        /// <summary>
        /// Gets the price of one wagon (0 for a road vehicle).
        /// </summary>
        public long WagonPrice { get; }

        /// <summary>
        /// Gets the locomotive's maximum wagon count (0 for a road vehicle).
        /// </summary>
        public int MaxWagons { get; }

        /// <summary>
        /// Gets the largest wagon count the company can pay for, 0 when it cannot pay for one (always 0 for a road
        /// vehicle).
        /// </summary>
        public int MaxAffordableWagons { get; }

        /// <summary>
        /// Gets the smallest wagon count that can be bought: 1 for a train, 0 for a road vehicle.
        /// </summary>
        public int MinWagons
        {
            get
            {
                return IsTrain ? 1 : 0;
            }
        }

        /// <summary>
        /// Creates the option of a road vehicle type.
        /// </summary>
        public static VehiclePurchaseOption Road(string typeId, string nameKey, long price, bool canAfford)
        {
            return new VehiclePurchaseOption(typeId, nameKey, price, 0L, 0, 0, canAfford);
        }

        /// <summary>
        /// Creates the option of a locomotive type bought with 1 to <paramref name="maxWagons"/> wagons;
        /// <paramref name="maxAffordableWagons"/> is the largest count the company can pay for (0 for none).
        /// </summary>
        public static VehiclePurchaseOption Train(
            string typeId,
            string nameKey,
            long locomotivePrice,
            long wagonPrice,
            int maxWagons,
            int maxAffordableWagons)
        {
            if (wagonPrice < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(wagonPrice), wagonPrice, "Wagon price cannot be negative.");
            }

            if (maxWagons < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWagons), maxWagons, "A locomotive must pull at least one wagon.");
            }

            if (maxAffordableWagons < 0 || maxAffordableWagons > maxWagons)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAffordableWagons),
                    maxAffordableWagons,
                    "Affordable wagons must be between 0 and the maximum wagon count.");
            }

            return new VehiclePurchaseOption(
                typeId,
                nameKey,
                locomotivePrice,
                wagonPrice,
                maxWagons,
                maxAffordableWagons,
                false);
        }

        /// <summary>
        /// Returns <paramref name="wagonCount"/> clamped to the counts this option can be bought with.
        /// </summary>
        public int ClampWagons(int wagonCount)
        {
            return Math.Min(Math.Max(wagonCount, MinWagons), MaxWagons);
        }

        /// <summary>
        /// Returns the total price with <paramref name="wagonCount"/> wagons (ignored for a road vehicle).
        /// </summary>
        public long GetPrice(int wagonCount)
        {
            if (!IsTrain)
            {
                return BasePrice;
            }

            if (wagonCount < 1 || wagonCount > MaxWagons)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count must be between 1 and the maximum wagon count.");
            }

            return checked(BasePrice + (WagonPrice * wagonCount));
        }

        /// <summary>
        /// Returns whether the company can pay for this option with <paramref name="wagonCount"/> wagons (ignored for
        /// a road vehicle).
        /// </summary>
        public bool CanAfford(int wagonCount)
        {
            return IsTrain ? wagonCount >= 1 && wagonCount <= MaxAffordableWagons : _canAffordRoad;
        }

        /// <inheritdoc />
        public bool Equals(VehiclePurchaseOption other)
        {
            return TypeId == other.TypeId
                && NameKey == other.NameKey
                && BasePrice == other.BasePrice
                && WagonPrice == other.WagonPrice
                && MaxWagons == other.MaxWagons
                && MaxAffordableWagons == other.MaxAffordableWagons
                && _canAffordRoad == other._canAffordRoad;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is VehiclePurchaseOption other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TypeId.GetHashCode();
                hash = (hash * 397) ^ NameKey.GetHashCode();
                hash = (hash * 397) ^ BasePrice.GetHashCode();
                hash = (hash * 397) ^ WagonPrice.GetHashCode();
                hash = (hash * 397) ^ MaxWagons;
                hash = (hash * 397) ^ MaxAffordableWagons;
                hash = (hash * 397) ^ _canAffordRoad.GetHashCode();
                return hash;
            }
        }
    }
}
