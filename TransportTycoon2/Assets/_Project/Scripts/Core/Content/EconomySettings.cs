#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Data-driven company starting cash. Vehicle purchase and upkeep live on <see cref="VehicleDefinition"/>.
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>
        /// Initializes economy settings.
        /// </summary>
        public EconomySettings(long startingCash)
        {
            if (startingCash < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingCash),
                    startingCash,
                    "Starting cash cannot be negative.");
            }

            StartingCash = startingCash;
        }

        /// <summary>
        /// Creates the placeholder starting cash used when content is unavailable.
        /// </summary>
        public static EconomySettings CreatePlaceholder()
        {
            return new EconomySettings(100000L);
        }

        /// <summary>
        /// Gets company cash granted to a new world.
        /// </summary>
        public long StartingCash { get; }

        /// <summary>
        /// Returns half of <paramref name="purchasePrice"/> using integer division.
        /// </summary>
        public static long HalfRefund(long purchasePrice)
        {
            if (purchasePrice < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(purchasePrice),
                    purchasePrice,
                    "Purchase price cannot be negative.");
            }

            return purchasePrice / 2L;
        }
    }
}
