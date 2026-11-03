#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published at most once per simulation tick when road or rail deliveries were paid during that tick
    /// (ADR 0024 queued delivery). One aggregated event per tick keeps the queue small when many vehicles
    /// unload at once (unloading moves one unit per vehicle per tick). <see cref="Payments"/> splits the income per
    /// station so the presentation can show where the money came from (Issue #284).
    /// </summary>
    public readonly struct DeliveryIncomeEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public DeliveryIncomeEvent(long income, GameDate date)
            : this(income, date, Array.Empty<DeliveryPayment>())
        {
        }

        /// <summary>
        /// Initializes the event with the per-station split of the income.
        /// </summary>
        public DeliveryIncomeEvent(long income, GameDate date, IReadOnlyList<DeliveryPayment> payments)
        {
            Income = income;
            Date = date;
            Payments = payments;
        }

        /// <summary>
        /// Gets the delivery income paid during the tick, in whole money; always positive.
        /// </summary>
        public long Income { get; }

        /// <summary>
        /// Gets the date of the tick.
        /// </summary>
        public GameDate Date { get; }

        /// <summary>
        /// Gets the income paid per station during the tick, one entry per station; empty when the event was built
        /// without a split.
        /// </summary>
        public IReadOnlyList<DeliveryPayment> Payments { get; }
    }
}
