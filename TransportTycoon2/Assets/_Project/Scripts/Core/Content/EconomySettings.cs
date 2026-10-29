#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Data-driven company starting cash, delivery pay and primary industry growth (Issue #73). Vehicle purchase and upkeep live on <see cref="VehicleDefinition"/>.
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>
        /// Initializes economy settings. Omitted delivery pay is flat and omitted industry growth is disabled.
        /// </summary>
        public EconomySettings(
            long startingCash,
            DeliveryPaySettings? deliveryPay = null,
            IndustryGrowthSettings? industryGrowth = null)
        {
            if (startingCash < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingCash),
                    startingCash,
                    "Starting cash cannot be negative.");
            }

            StartingCash = startingCash;
            DeliveryPay = deliveryPay ?? DeliveryPaySettings.Flat;
            IndustryGrowth = industryGrowth ?? IndustryGrowthSettings.Disabled;
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
        /// Gets the delivery pay rule.
        /// </summary>
        public DeliveryPaySettings DeliveryPay { get; }

        /// <summary>
        /// Gets the primary industry growth rule.
        /// </summary>
        public IndustryGrowthSettings IndustryGrowth { get; }

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
