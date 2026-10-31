#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// One freight need line for a town card (Issue #60, Issue #242): the cargo, its need level, the monthly demand,
    /// and the units supplied last month.
    /// </summary>
    public readonly struct TownNeedListLine : IEquatable<TownNeedListLine>
    {
        /// <summary>
        /// Lowest need level (raw goods).
        /// </summary>
        public const int MinLevel = 1;

        /// <summary>
        /// Highest need level (finished goods).
        /// </summary>
        public const int MaxLevel = 3;

        /// <summary>
        /// Initializes a need line.
        /// </summary>
        public TownNeedListLine(string cargoId, string cargoNameKey, int level, int lastMonthSupply, int demand)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (string.IsNullOrWhiteSpace(cargoNameKey))
            {
                throw new ArgumentException("Cargo name key cannot be null or whitespace.", nameof(cargoNameKey));
            }

            if (level < MinLevel || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Need level must be 1, 2 or 3.");
            }

            if (lastMonthSupply < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lastMonthSupply),
                    lastMonthSupply,
                    "Last month supply cannot be negative.");
            }

            if (demand < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(demand), demand, "Demand must be positive.");
            }

            CargoId = cargoId;
            CargoNameKey = cargoNameKey;
            Level = level;
            LastMonthSupply = lastMonthSupply;
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
        /// Gets the need level: 1 raw, 2 processed, 3 finished goods.
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// Gets units delivered last month for this need.
        /// </summary>
        public int LastMonthSupply { get; }

        /// <summary>
        /// Gets monthly demand for this need.
        /// </summary>
        public int Demand { get; }

        /// <summary>
        /// Gets how well the need was supplied last month.
        /// </summary>
        public TownNeedLineStatus Status
        {
            get
            {
                if (LastMonthSupply >= Demand)
                {
                    return TownNeedLineStatus.FullyMet;
                }

                return LastMonthSupply > 0 ? TownNeedLineStatus.PartlyMet : TownNeedLineStatus.Unmet;
            }
        }

        /// <inheritdoc />
        public bool Equals(TownNeedListLine other)
        {
            return string.Equals(CargoId, other.CargoId, StringComparison.Ordinal)
                && string.Equals(CargoNameKey, other.CargoNameKey, StringComparison.Ordinal)
                && Level == other.Level
                && LastMonthSupply == other.LastMonthSupply
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
                int hash = StringComparer.Ordinal.GetHashCode(CargoId ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CargoNameKey ?? string.Empty);
                hash = (hash * 397) ^ Level;
                hash = (hash * 397) ^ LastMonthSupply;
                hash = (hash * 397) ^ Demand;
                return hash;
            }
        }
    }
}
