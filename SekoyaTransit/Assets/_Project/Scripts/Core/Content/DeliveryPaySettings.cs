#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven delivery pay rule (Issues #73 and #342): every unit delivered pays the cargo price plus a bonus that
    /// grows by the same share of the price for each tile between the station where the load started and the delivery
    /// station.
    /// </summary>
    public sealed class DeliveryPaySettings
    {
        /// <summary>
        /// The whole cargo price, in hundredths of a percent.
        /// </summary>
        public const int WholePrice = 10000;

        /// <summary>
        /// Flat pay (price per unit, whatever the distance): the rule before Issue #73, kept for focused tests.
        /// </summary>
        public static readonly DeliveryPaySettings Flat = new DeliveryPaySettings(0, 1);

        /// <summary>
        /// Initializes a delivery pay rule.
        /// </summary>
        /// <param name="percentPerTileHundredths">
        /// Bonus per tile in hundredths of a percent of the price (125 is 1.25 %); 0 means flat pay.
        /// </param>
        /// <param name="minimumDistance">Shortest distance paid for, also used when the load origin is unknown.</param>
        public DeliveryPaySettings(int percentPerTileHundredths, int minimumDistance)
        {
            if (percentPerTileHundredths < 0 || percentPerTileHundredths > WholePrice)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentPerTileHundredths),
                    percentPerTileHundredths,
                    "Percent per tile must be between 0 and 100.");
            }

            if (minimumDistance < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDistance),
                    minimumDistance,
                    "Minimum distance must be at least 1 tile.");
            }

            PercentPerTileHundredths = percentPerTileHundredths;
            MinimumDistance = minimumDistance;
        }

        /// <summary>
        /// Gets the bonus per tile in hundredths of a percent of the price; 0 means flat pay.
        /// </summary>
        public int PercentPerTileHundredths { get; }

        /// <summary>
        /// Gets the shortest distance in tiles paid for.
        /// </summary>
        public int MinimumDistance { get; }

        /// <summary>
        /// Gets whether pay ignores distance.
        /// </summary>
        public bool IsFlat
        {
            get
            {
                return PercentPerTileHundredths == 0;
            }
        }
    }
}
