#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// One purchasable vehicle type offered at the selected station.
    /// </summary>
    public readonly struct VehiclePurchaseOption : IEquatable<VehiclePurchaseOption>
    {
        /// <summary>
        /// Initializes a buy option.
        /// </summary>
        public VehiclePurchaseOption(string typeId, string nameKey, long purchasePrice, bool canAfford)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Type id cannot be null or whitespace.", nameof(typeId));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (purchasePrice < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(purchasePrice),
                    purchasePrice,
                    "Purchase price must be positive.");
            }

            TypeId = typeId;
            NameKey = nameKey;
            PurchasePrice = purchasePrice;
            CanAfford = canAfford;
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
        /// Gets the purchase price.
        /// </summary>
        public long PurchasePrice { get; }

        /// <summary>
        /// Gets whether company cash covers the purchase.
        /// </summary>
        public bool CanAfford { get; }

        /// <inheritdoc />
        public bool Equals(VehiclePurchaseOption other)
        {
            return TypeId == other.TypeId
                && NameKey == other.NameKey
                && PurchasePrice == other.PurchasePrice
                && CanAfford == other.CanAfford;
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
                hash = (hash * 397) ^ PurchasePrice.GetHashCode();
                hash = (hash * 397) ^ CanAfford.GetHashCode();
                return hash;
            }
        }
    }
}
