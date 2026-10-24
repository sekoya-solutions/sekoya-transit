#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// One freight need line for a town list row (Issue #60).
    /// </summary>
    public readonly struct TownNeedListLine : IEquatable<TownNeedListLine>
    {
        /// <summary>
        /// Initializes a need line.
        /// </summary>
        public TownNeedListLine(string cargoId, string cargoNameKey, int monthSupply, int demand)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (string.IsNullOrWhiteSpace(cargoNameKey))
            {
                throw new ArgumentException("Cargo name key cannot be null or whitespace.", nameof(cargoNameKey));
            }

            if (monthSupply < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monthSupply), monthSupply, "Month supply cannot be negative.");
            }

            if (demand < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(demand), demand, "Demand must be positive.");
            }

            CargoId = cargoId;
            CargoNameKey = cargoNameKey;
            MonthSupply = monthSupply;
            Demand = demand;
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
        /// Gets units delivered this month for this need.
        /// </summary>
        public int MonthSupply { get; }

        /// <summary>
        /// Gets monthly demand for this need.
        /// </summary>
        public int Demand { get; }

        /// <inheritdoc />
        public bool Equals(TownNeedListLine other)
        {
            return string.Equals(CargoId, other.CargoId, StringComparison.Ordinal)
                && string.Equals(CargoNameKey, other.CargoNameKey, StringComparison.Ordinal)
                && MonthSupply == other.MonthSupply
                && Demand == other.Demand;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TownNeedListLine other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CargoId.GetHashCode();
                hash = (hash * 397) ^ CargoNameKey.GetHashCode();
                hash = (hash * 397) ^ MonthSupply;
                hash = (hash * 397) ^ Demand;
                return hash;
            }
        }
    }
}
