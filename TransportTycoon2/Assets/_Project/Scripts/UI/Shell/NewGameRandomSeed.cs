#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Rolls the seeds offered on the new game screen (Issue #67). Presentation-side randomness only: it never
    /// touches a Core random generator, so it cannot influence the deterministic simulation. Seeds are 32-bit
    /// values (up to 4294967295) so they stay short enough to read and share.
    /// </summary>
    public sealed class NewGameRandomSeed
    {
        private readonly Random _random;

        /// <summary>
        /// Initializes a source seeded from the system clock.
        /// </summary>
        public NewGameRandomSeed()
            : this(new Random())
        {
        }

        /// <summary>
        /// Initializes a source over a given generator (tests pass a seeded one).
        /// </summary>
        public NewGameRandomSeed(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Returns the next random seed.
        /// </summary>
        public ulong Next()
        {
            var bytes = new byte[4];
            _random.NextBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}
