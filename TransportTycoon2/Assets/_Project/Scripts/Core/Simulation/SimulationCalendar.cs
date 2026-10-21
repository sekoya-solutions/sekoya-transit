#nullable enable
using System;

namespace TransportTycoon.Core.Simulation
{
    /// <summary>
    /// Derives a deterministic calendar date from simulation ticks at a configured tick rate.
    /// </summary>
    public sealed class SimulationCalendar
    {
        private const long _realTimeTicksPerGameYear = 1800L * TimeSpan.TicksPerSecond;
        private readonly long _simulationTickDurationTicks;

        /// <summary>
        /// Initializes the calendar from the simulation's normal-speed tick duration.
        /// </summary>
        /// <param name="simulationTickDuration">The real-time duration of one simulation tick at normal speed.</param>
        public SimulationCalendar(TimeSpan simulationTickDuration)
        {
            if (simulationTickDuration.Ticks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTickDuration),
                    simulationTickDuration,
                    "Simulation tick duration must be positive.");
            }

            _simulationTickDurationTicks = simulationTickDuration.Ticks;
        }

        /// <summary>
        /// Initializes the calendar using the configured duration of a simulation loop tick.
        /// </summary>
        /// <param name="simulationLoop">The simulation loop whose tick duration defines calendar speed.</param>
        public SimulationCalendar(SimulationLoop simulationLoop)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (simulationLoop == null)
            {
                throw new ArgumentNullException(nameof(simulationLoop));
            }
#pragma warning restore CA1510

            _simulationTickDurationTicks = simulationLoop.TickDuration.Ticks;
        }

        /// <summary>
        /// Gets the date corresponding to a simulation tick. Tick zero is 1900-01-01.
        /// </summary>
        /// <param name="tick">The simulation tick whose date is requested.</param>
        /// <returns>The whole-day calendar date at that tick.</returns>
        public GameDate GetDate(Tick tick)
        {
            decimal elapsedRealTimeTicks = (decimal)tick.Value * _simulationTickDurationTicks;
            decimal elapsedGameDays = decimal.Floor(
                (elapsedRealTimeTicks * GameDate.DaysPerYear) / _realTimeTicksPerGameYear);

            long wholeDays = checked((long)elapsedGameDays);
            return GameDate.FromElapsedDays(wholeDays);
        }
    }
}
