#nullable enable
namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// The delivery income paid at one station during one simulation tick (Issue #284).
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
        /// Gets the income paid at the station during the tick, in whole money; always positive.
        /// </summary>
        public long Income { get; }
    }
}
