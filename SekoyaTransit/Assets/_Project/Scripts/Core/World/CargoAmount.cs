#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A single cargo type and quantity pair stored in an inventory.
    /// </summary>
    public readonly struct CargoAmount : IEquatable<CargoAmount>
    {
        /// <summary>
        /// Initializes a cargo stack.
        /// </summary>
        /// <param name="cargoId">Non-empty cargo definition id.</param>
        /// <param name="amount">Positive quantity.</param>
        public CargoAmount(string cargoId, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cargo amount must be positive.");
            }

            CargoId = cargoId;
            Amount = amount;
        }

        /// <summary>
        /// Gets the cargo definition id.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets the quantity of this cargo.
        /// </summary>
        public int Amount { get; }

        /// <inheritdoc />
        public bool Equals(CargoAmount other)
        {
            return Amount == other.Amount &&
                   string.Equals(CargoId, other.CargoId, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is CargoAmount other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((CargoId != null ? StringComparer.Ordinal.GetHashCode(CargoId) : 0) * 397) ^ Amount;
            }
        }

        /// <summary>
        /// Returns whether two amounts are equal.
        /// </summary>
        public static bool operator ==(CargoAmount left, CargoAmount right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether two amounts are not equal.
        /// </summary>
        public static bool operator !=(CargoAmount left, CargoAmount right)
        {
            return !left.Equals(right);
        }
    }
}
