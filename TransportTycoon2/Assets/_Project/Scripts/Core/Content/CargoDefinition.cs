#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Immutable definition of a cargo type.
    /// </summary>
    public sealed class CargoDefinition
    {
        /// <summary>
        /// Initializes a cargo definition.
        /// </summary>
        /// <param name="id">Stable content ID.</param>
        /// <param name="nameKey">Localization key for the display name.</param>
        /// <param name="cargoClass">Passengers or freight cargo.</param>
        /// <param name="pricePerUnit">Delivery price per unit.</param>
        /// <param name="manufacturingLevel">
        /// 0 for passengers; 1–3 for freight (matches the producer industry chain level).
        /// </param>
        public CargoDefinition(
            string id,
            string nameKey,
            CargoClass cargoClass,
            int pricePerUnit,
            int manufacturingLevel)
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

            if (cargoClass == CargoClass.Passengers)
            {
                if (manufacturingLevel != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(manufacturingLevel),
                        manufacturingLevel,
                        "Passenger cargo must have manufacturing level 0.");
                }
            }
            else if (manufacturingLevel < 1 || manufacturingLevel > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(manufacturingLevel),
                    manufacturingLevel,
                    "Freight manufacturing level must be 1, 2, or 3.");
            }

            Id = id;
            NameKey = nameKey;
            Class = cargoClass;
            PricePerUnit = pricePerUnit;
            ManufacturingLevel = manufacturingLevel;
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

        /// <summary>
        /// Gets the manufacturing level (0 for passengers; 1–3 for freight).
        /// </summary>
        public int ManufacturingLevel { get; }
    }
}
