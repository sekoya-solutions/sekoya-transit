#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Immutable cargo quantity in an industry's production recipe.
    /// </summary>
    public sealed class CargoAmountDefinition
    {
        public CargoAmountDefinition(string cargoId, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo ID cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cargo amount must be positive.");
            }

            CargoId = cargoId;
            Amount = amount;
        }

        /// <summary>
        /// Gets the stable ID of the cargo in this recipe entry.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets the amount produced or consumed per cycle.
        /// </summary>
        public int Amount { get; }
    }
}
