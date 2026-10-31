#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A town freight need identified by cargo content ID, with monthly supply tracking.
    /// </summary>
    public sealed class TownNeed
    {
        /// <summary>
        /// Initializes a town need.
        /// </summary>
        /// <param name="cargoId">Freight cargo content ID.</param>
        /// <param name="monthSupply">Units delivered toward this need in the current stats month.</param>
        /// <param name="lastMonthSupply">Units delivered toward this need in the previous stats month.</param>
        public TownNeed(string cargoId, int monthSupply = 0, int lastMonthSupply = 0)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo ID cannot be null or whitespace.", nameof(cargoId));
            }

            if (monthSupply < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthSupply),
                    monthSupply,
                    "Month supply cannot be negative.");
            }

            if (lastMonthSupply < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lastMonthSupply),
                    lastMonthSupply,
                    "Last month supply cannot be negative.");
            }

            CargoId = cargoId;
            MonthSupply = monthSupply;
            LastMonthSupply = lastMonthSupply;
        }

        /// <summary>
        /// Gets the needed cargo content ID.
        /// </summary>
        public string CargoId { get; }

        /// <summary>
        /// Gets units delivered toward this need in the current stats month.
        /// </summary>
        public int MonthSupply { get; private set; }

        /// <summary>
        /// Gets units delivered toward this need in the previous stats month, which month-end growth was computed from
        /// (Issue #242). Zero for a need unlocked since then.
        /// </summary>
        public int LastMonthSupply { get; private set; }

        /// <summary>
        /// Adds delivered units to this month's supply.
        /// </summary>
        internal void CreditMonthSupply(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MonthSupply = checked(MonthSupply + amount);
        }

        /// <summary>
        /// Moves this month's supply to last month's and clears this month's counter.
        /// </summary>
        internal void RollMonthSupply()
        {
            LastMonthSupply = MonthSupply;
            MonthSupply = 0;
        }
    }
}
