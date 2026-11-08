#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven delivery pay rule (Issues #73 and #342): every unit delivered pays a start share of the cargo price
    /// plus the same further share for each tile between the station where the load started and the delivery station.
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
        public static readonly DeliveryPaySettings Flat = new DeliveryPaySettings(WholePrice, 0, 1);

        /// <summary>
        /// Initializes a delivery pay rule.
        /// </summary>
        /// <param name="startPercentHundredths">
        /// Share of the price every delivery earns before any tile is counted, in hundredths of a percent (700 is 7 %).
        /// </param>
        /// <param name="percentPerTileHundredths">
        /// Share of the price each tile adds, in hundredths of a percent (300 is 3 %).
        /// </param>
        /// <param name="minimumDistance">Shortest distance paid for, also used when the load origin is unknown.</param>
        public DeliveryPaySettings(int startPercentHundredths, int percentPerTileHundredths, int minimumDistance)
        {
            if (startPercentHundredths < 0 || startPercentHundredths > WholePrice)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startPercentHundredths),
                    startPercentHundredths,
                    "Start percent must be between 0 and 100.");
            }

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

            StartPercentHundredths = startPercentHundredths;
            PercentPerTileHundredths = percentPerTileHundredths;
            MinimumDistance = minimumDistance;
        }

        /// <summary>
        /// Gets the share of the price every delivery earns before any tile is counted, in hundredths of a percent.
        /// </summary>
        public int StartPercentHundredths { get; }

        /// <summary>
        /// Gets the share of the price each tile adds, in hundredths of a percent.
        /// </summary>
        public int PercentPerTileHundredths { get; }

        /// <summary>
        /// Gets the shortest distance in tiles paid for.
        /// </summary>
        public int MinimumDistance { get; }

        /// <summary>
        /// Gets whether every delivery pays exactly the cargo price, whatever the distance.
        /// </summary>
        public bool IsFlat
        {
            get
            {
                return StartPercentHundredths == WholePrice && PercentPerTileHundredths == 0;
            }
        }
    }
}
