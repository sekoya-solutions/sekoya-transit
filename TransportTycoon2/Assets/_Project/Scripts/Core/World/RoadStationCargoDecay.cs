#nullable enable
using System;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic periodic decay of cargo waiting at road stations.
    /// </summary>
    public static class RoadStationCargoDecay
    {
        /// <summary>
        /// Simulation ticks between decay pulses.
        /// </summary>
        public const int DecayIntervalTicks = 60;

        /// <summary>
        /// Cargo units removed from each non-empty station on a decay pulse.
        /// </summary>
        public const int DecayAmount = 1;

        /// <summary>
        /// Applies decay to every road station when <paramref name="tick"/> hits the interval.
        /// </summary>
        public static void AdvanceAll(GeneratedWorld world, Tick tick)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            if (tick.Value <= 0 || tick.Value % DecayIntervalTicks != 0)
            {
                return;
            }

            for (int index = 0; index < world.RoadStations.Count; index++)
            {
                RoadStation station = world.RoadStations[index];
                if (station.Cargo.IsEmpty)
                {
                    continue;
                }

                CargoInventory cargo = station.Cargo;
                int remaining = DecayAmount;
                while (remaining > 0 && !cargo.IsEmpty)
                {
                    CargoAmount stack = cargo.Stacks[0];
                    int remove = stack.Amount < remaining ? stack.Amount : remaining;
                    cargo.TryRemove(stack.CargoId, remove);
                    remaining -= remove;
                }
            }
        }
    }
}
