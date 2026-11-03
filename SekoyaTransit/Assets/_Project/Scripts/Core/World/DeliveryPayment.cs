#nullable enable
namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// The total delivery income of one vehicle's unload visit at a station (Issue #284).
    /// </summary>
    public readonly struct DeliveryPayment
    {
        /// <summary>
        /// Initializes the payment.
        /// </summary>
        public DeliveryPayment(TileCoordinate station, long income)
        {
            Station = station;
            Income = income;
        }

        /// <summary>
        /// Gets the coordinate of the station that received the delivery.
        /// </summary>
        public TileCoordinate Station { get; }

        /// <summary>
        /// Gets the income a vehicle earned over one whole unload visit at the station, in whole money; always positive.
        /// </summary>
        public long Income { get; }
    }
}
