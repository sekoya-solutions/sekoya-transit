#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Lifetime units of one cargo delivered (accepted and paid) to one destination kind.
    /// </summary>
    public readonly struct CargoDeliveryCounter : IEquatable<CargoDeliveryCounter>
    {
        /// <summary>
        /// Initializes a counter. <paramref name="destinationKind"/> must be town or industry.
        /// </summary>
        public CargoDeliveryCounter(string cargoId, DeliveryDestinationKind destinationKind, long amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (destinationKind != DeliveryDestinationKind.Town && destinationKind != DeliveryDestinationKind.Industry)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destinationKind),
                    destinationKind,
                    "A recorded delivery destination must be town or industry.");
            }

            if (amount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Delivered amount cannot be negative.");
            }

            CargoId = cargoId;
            DestinationKind = destinationKind;
            Amount = amount;
        }

        /// <summary>
        /// Gets the cargo id.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets the destination kind (town or industry).
        /// </summary>
        public DeliveryDestinationKind DestinationKind { get; }

        /// <summary>
        /// Gets the lifetime delivered units.
        /// </summary>
        public long Amount { get; }

        /// <inheritdoc />
        public bool Equals(CargoDeliveryCounter other)
        {
            return string.Equals(CargoId, other.CargoId, StringComparison.Ordinal) &&
                DestinationKind == other.DestinationKind &&
                Amount == other.Amount;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is CargoDeliveryCounter other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(CargoId), DestinationKind, Amount);
        }

        /// <summary>
        /// Returns whether two counters are equal.
        /// </summary>
        public static bool operator ==(CargoDeliveryCounter left, CargoDeliveryCounter right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether two counters differ.
        /// </summary>
        public static bool operator !=(CargoDeliveryCounter left, CargoDeliveryCounter right)
        {
            return !left.Equals(right);
        }
    }
}
