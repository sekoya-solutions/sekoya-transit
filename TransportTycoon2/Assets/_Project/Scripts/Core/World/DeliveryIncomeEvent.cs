#nullable enable
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Published at most once per simulation tick when road or rail deliveries were paid during that tick
    /// (ADR 0024 queued delivery). One aggregated event per tick keeps the queue small when many vehicles
    /// unload at once (unloading moves one unit per vehicle per tick).
    /// </summary>
    public readonly struct DeliveryIncomeEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public DeliveryIncomeEvent(long income, GameDate date)
        {
            Income = income;
            Date = date;
        }

        /// <summary>
        /// Gets the delivery income paid during the tick, in whole money; always positive.
        /// </summary>
        public long Income { get; }

        /// <summary>
        /// Gets the date of the tick.
        /// </summary>
        public GameDate Date { get; }
    }
}
