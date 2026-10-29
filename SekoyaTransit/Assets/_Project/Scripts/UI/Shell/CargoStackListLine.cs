#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// One cargo stack for vehicle or industry list display.
    /// </summary>
    public readonly struct CargoStackListLine : IEquatable<CargoStackListLine>
    {
        /// <summary>
        /// Initializes a cargo stack line.
        /// </summary>
        public CargoStackListLine(string cargoId, string cargoNameKey, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (string.IsNullOrWhiteSpace(cargoNameKey))
            {
                throw new ArgumentException("Cargo name key cannot be null or whitespace.", nameof(cargoNameKey));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
            }

            CargoId = cargoId;
            CargoNameKey = cargoNameKey;
            Amount = amount;
        }

        /// <summary>
        /// Gets the cargo definition id.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets the localization key for the cargo display name.
        /// </summary>
        public string CargoNameKey { get; }

        /// <summary>
        /// Gets units in this stack.
        /// </summary>
        public int Amount { get; }

        /// <inheritdoc />
        public bool Equals(CargoStackListLine other)
        {
            return string.Equals(CargoId, other.CargoId, StringComparison.Ordinal)
                && string.Equals(CargoNameKey, other.CargoNameKey, StringComparison.Ordinal)
                && Amount == other.Amount;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is CargoStackListLine other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CargoId.GetHashCode();
                hash = (hash * 397) ^ CargoNameKey.GetHashCode();
                hash = (hash * 397) ^ Amount;
                return hash;
            }
        }
    }
}
