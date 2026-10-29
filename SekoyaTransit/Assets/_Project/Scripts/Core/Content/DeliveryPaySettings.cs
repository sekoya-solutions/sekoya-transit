#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven delivery pay rule (Issue #73): a delivery pays the cargo price per unit scaled by the distance in
    /// tiles between the station where the load started and the delivery station, relative to a reference distance.
    /// </summary>
    public sealed class DeliveryPaySettings
    {
        /// <summary>
        /// Flat pay (price per unit, whatever the distance): the rule before Issue #73, kept for focused tests.
        /// </summary>
        public static readonly DeliveryPaySettings Flat = new DeliveryPaySettings(0, 1);

        /// <summary>
        /// Initializes a delivery pay rule.
        /// </summary>
        /// <param name="referenceDistance">Distance in tiles at which a unit pays exactly its price; 0 means flat pay.</param>
        /// <param name="minimumDistance">Shortest distance paid for, also used when the load origin is unknown.</param>
        public DeliveryPaySettings(int referenceDistance, int minimumDistance)
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

            ReferenceDistance = referenceDistance;
            MinimumDistance = minimumDistance;
        }

        /// <summary>
        /// Gets the distance in tiles at which a unit pays exactly its price; 0 means flat pay.
        /// </summary>
        public int ReferenceDistance { get; }

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
                return ReferenceDistance == 0;
            }
        }
    }
}
