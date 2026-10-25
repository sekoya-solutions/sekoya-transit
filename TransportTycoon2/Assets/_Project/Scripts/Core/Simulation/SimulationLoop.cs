#nullable enable
using System;

namespace TransportTycoon.Core.Simulation
{
    /// <summary>
    /// Advances a simulation in fixed-duration ticks independently of frame rate.
    /// </summary>
    public sealed class SimulationLoop
    {
        private readonly long _tickDurationTicks;
        private long _accumulatedTimeTicks;
        private int _speedMultiplier;

        /// <summary>
        /// Initializes a simulation loop.
        /// </summary>
        /// <param name="tickDuration">The real-time duration of one tick at normal speed.</param>
        /// <param name="initialTick">The simulation's starting tick.</param>
        /// <param name="speedMultiplier">The number of normal-speed ticks per elapsed tick duration.</param>
        public SimulationLoop(TimeSpan tickDuration, Tick initialTick, int speedMultiplier = 1)
        {
            if (tickDuration.Ticks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration), tickDuration, "Tick duration must be positive.");
            }

            if (speedMultiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), speedMultiplier, "Speed multiplier must be positive.");
            }

            _tickDurationTicks = tickDuration.Ticks;
            _speedMultiplier = speedMultiplier;
            CurrentTick = initialTick;
        }

        /// <summary>
        /// Gets the most recently completed simulation tick.
        /// </summary>
        public Tick CurrentTick { get; private set; }

        /// <summary>
        /// Gets the real-time duration of one simulation tick at normal speed.
        /// </summary>
        public TimeSpan TickDuration
        {
            get
            {
                return TimeSpan.FromTicks(_tickDurationTicks);
            }
        }

        /// <summary>
        /// Gets or sets how many ticks are processed per normal-speed tick duration.
        /// </summary>
        public int SpeedMultiplier
        {
            get
            {
                return _speedMultiplier;
            }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Speed multiplier must be positive.");
                }

                _speedMultiplier = value;
            }
        }

        /// <summary>
        /// Gets or sets whether elapsed time is ignored instead of advancing simulation ticks.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Adds elapsed real time and invokes the simulation callback once for each whole tick due.
        /// Partial tick time is retained for the next call. Time passed while paused is discarded.
        /// </summary>
        /// <param name="elapsed">The real time elapsed since the previous call.</param>
        /// <param name="advanceSimulation">The operation that advances simulation state to the supplied tick.</param>
        public void Advance(TimeSpan elapsed, Action<Tick> advanceSimulation)
        {
            if (elapsed.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time cannot be negative.");
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (advanceSimulation == null)
            {
                throw new ArgumentNullException(nameof(advanceSimulation));
            }
#pragma warning restore CA1510

            if (IsPaused)
            {
                return;
            }

            if (elapsed != TimeSpan.Zero)
            {
                long scaledElapsedTicks = checked(elapsed.Ticks * _speedMultiplier);
                _accumulatedTimeTicks = checked(_accumulatedTimeTicks + scaledElapsedTicks);
            }

            while (!IsPaused && _accumulatedTimeTicks >= _tickDurationTicks)
            {
                Tick nextTick = CurrentTick.Next();
                advanceSimulation(nextTick);
                CurrentTick = nextTick;
                _accumulatedTimeTicks -= _tickDurationTicks;
            }
        }
    }
}
