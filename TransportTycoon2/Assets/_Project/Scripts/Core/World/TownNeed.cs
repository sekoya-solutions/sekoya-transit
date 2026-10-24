#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A town freight need identified by cargo content ID.
    /// </summary>
    public readonly struct TownNeed : IEquatable<TownNeed>
    {
        /// <summary>
        /// Initializes a town need.
        /// </summary>
        /// <param name="cargoId">Freight cargo content ID.</param>
        public TownNeed(string cargoId)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo ID cannot be null or whitespace.", nameof(cargoId));
            }

            CargoId = cargoId;
        }

        /// <summary>
        /// Gets the needed cargo content ID.
        /// </summary>
        public string CargoId { get; }

        /// <inheritdoc />
        public bool Equals(TownNeed other)
        {
            return string.Equals(CargoId, other.CargoId, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TownNeed other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(CargoId);
        }

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(TownNeed left, TownNeed right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(TownNeed left, TownNeed right)
        {
            return !left.Equals(right);
        }
    }
}
