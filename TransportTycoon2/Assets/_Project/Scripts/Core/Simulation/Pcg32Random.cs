#nullable enable
using System;

namespace TransportTycoon.Core.Simulation
{
    /// <summary>
    /// Captured state for a PCG-XSH-RR 64/32 random number generator.
    /// </summary>
    public readonly struct Pcg32State
    {
        /// <summary>
        /// Initializes a captured state.
        /// </summary>
        /// <param name="state">The 64-bit internal state.</param>
        /// <param name="increment">The odd 64-bit stream increment.</param>
        public Pcg32State(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(increment), "The PCG stream increment must be odd.");
            }

            State = state;
            Increment = increment;
        }

        /// <summary>
        /// Gets the 64-bit internal state.
        /// </summary>
        public ulong State { get; }

        /// <summary>
        /// Gets the odd 64-bit stream increment.
        /// </summary>
        public ulong Increment { get; }
    }

    /// <summary>
    /// A deterministic PCG-XSH-RR 64/32 pseudo-random number generator.
    /// </summary>
    public sealed class Pcg32Random
    {
        private const ulong _multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <summary>
        /// Initializes a generator using the PCG reference seeding procedure.
        /// </summary>
        /// <param name="seed">The 64-bit initial state value.</param>
        /// <param name="sequence">The 64-bit stream selector.</param>
        public Pcg32Random(ulong seed, ulong sequence)
        {
            _state = 0;
            _increment = unchecked((sequence << 1) | 1UL);
            NextUInt32();
            _state = unchecked(_state + seed);
            NextUInt32();
        }

        /// <summary>
        /// Initializes a generator from a previously captured state.
        /// </summary>
        /// <param name="state">The state to restore.</param>
        public Pcg32Random(Pcg32State state)
        {
            if ((state.Increment & 1UL) == 0)
            {
                throw new ArgumentException("The captured PCG stream increment must be odd.", nameof(state));
            }

            _state = state.State;
            _increment = state.Increment;
        }

        /// <summary>
        /// Gets a copy of the state needed to restore the generator at this point.
        /// </summary>
        public Pcg32State CapturedState
        {
            get
            {
                return new Pcg32State(_state, _increment);
            }
        }

        /// <summary>
        /// Returns the next 32-bit value in the deterministic sequence.
        /// </summary>
        /// <returns>A value in the range [0, 2^32).</returns>
        public uint NextUInt32()
        {
            ulong previousState = _state;
            _state = unchecked((previousState * _multiplier) + _increment);
            uint shifted = (uint)(((previousState >> 18) ^ previousState) >> 27);
            int rotation = (int)(previousState >> 59);
            return (shifted >> rotation) | (shifted << ((-rotation) & 31));
        }

        /// <summary>
        /// Returns an integer in the range [<paramref name="minInclusive"/>,
        /// <paramref name="maxExclusive"/>), without modulo bias.
        /// </summary>
        /// <param name="minInclusive">The inclusive lower bound.</param>
        /// <param name="maxExclusive">The exclusive upper bound.</param>
        /// <returns>A value within the requested range.</returns>
        public int NextInt32(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    maxExclusive,
                    "The exclusive upper bound must be greater than the inclusive lower bound.");
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            uint rejectionThreshold = unchecked(0U - range) % range;
            uint value;
            do
            {
                value = NextUInt32();
            }
            while (value < rejectionThreshold);

            return (int)(minInclusive + (long)(value % range));
        }
    }
}
