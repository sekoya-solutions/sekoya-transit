#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Immutable definition of a cargo type.
    /// </summary>
    public sealed class CargoDefinition
    {
        public CargoDefinition(string id, string nameKey, CargoClass cargoClass, int pricePerUnit)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Cargo ID cannot be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Cargo name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (cargoClass != CargoClass.Passengers && cargoClass != CargoClass.Cargo)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoClass), cargoClass, "Cargo class is not supported.");
            }

            if (pricePerUnit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pricePerUnit), pricePerUnit, "Price per unit must be positive.");
            }

            Id = id;
            NameKey = nameKey;
            Class = cargoClass;
            PricePerUnit = pricePerUnit;
        }

        /// <summary>
        /// Gets the stable content ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the localization key for the cargo name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the gameplay class.
        /// </summary>
        public CargoClass Class { get; }

        /// <summary>
        /// Gets the price per unit.
        /// </summary>
        public int PricePerUnit { get; }
    }
}
