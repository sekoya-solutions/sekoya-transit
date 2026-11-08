#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven delivery pay rule (Issues #73 and #342): a delivery pays the cargo price per unit scaled by the paid
    /// distance, relative to a reference distance. The paid distance counts the tiles between the station where the load
    /// started and the delivery station in full up to a limit, and only a share of each tile beyond it.
    /// </summary>
    public sealed class DeliveryPaySettings
    {
        /// <summary>
        /// Flat pay (price per unit, whatever the distance): the rule before Issue #73, kept for focused tests.
        /// </summary>
        public static readonly DeliveryPaySettings Flat = new DeliveryPaySettings(0, 1, 1, 100);

        /// <summary>
        /// Initializes a delivery pay rule.
        /// </summary>
        /// <param name="referenceDistance">Paid distance in tiles at which a unit pays exactly its price; 0 means flat pay.</param>
        /// <param name="minimumDistance">Shortest distance paid for, also used when the load origin is unknown.</param>
        /// <param name="fullPayDistance">Tiles paid in full; not below the minimum distance.</param>
        /// <param name="beyondFullPayPercent">Percent of each tile beyond the full pay distance that still counts, from 1 to 100.</param>
        public DeliveryPaySettings(int referenceDistance, int minimumDistance, int fullPayDistance, int beyondFullPayPercent)
        {
            if (referenceDistance < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(referenceDistance),
                    referenceDistance,
                    "Reference distance cannot be negative.");
            }

            if (minimumDistance < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDistance),
                    minimumDistance,
                    "Minimum distance must be at least 1 tile.");
            }

            if (fullPayDistance < minimumDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullPayDistance),
                    fullPayDistance,
                    "Full pay distance cannot be below the minimum distance.");
            }

            if (beyondFullPayPercent < 1 || beyondFullPayPercent > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(beyondFullPayPercent),
                    beyondFullPayPercent,
                    "Percent paid beyond the full pay distance must be between 1 and 100.");
            }

            ReferenceDistance = referenceDistance;
            MinimumDistance = minimumDistance;
            FullPayDistance = fullPayDistance;
            BeyondFullPayPercent = beyondFullPayPercent;
        }

        /// <summary>
        /// Gets the paid distance in tiles at which a unit pays exactly its price; 0 means flat pay.
        /// </summary>
        public int ReferenceDistance { get; }

        /// <summary>
        /// Gets the shortest distance in tiles paid for.
        /// </summary>
        public int MinimumDistance { get; }

        /// <summary>
        /// Gets the tiles paid in full.
        /// </summary>
        public int FullPayDistance { get; }

        /// <summary>
        /// Gets the percent of each tile beyond the full pay distance that still counts as paid distance.
        /// </summary>
        public int BeyondFullPayPercent { get; }

        /// <summary>
        /// Gets whether pay ignores distance.
        /// </summary>
        public bool IsFlat
        {
            get
            {
                return ReferenceDistance == 0;
            }
        }
    }
}
