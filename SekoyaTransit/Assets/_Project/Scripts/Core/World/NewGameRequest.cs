#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// What the player chooses on the new game screen: a world seed and a map size (Issue #67).
    /// </summary>
    public readonly struct NewGameRequest : IEquatable<NewGameRequest>
    {
        /// <summary>
        /// Initializes a request.
        /// </summary>
        public NewGameRequest(ulong seed, MapSize mapSize)
        {
            if (!MapSizes.IsSupported(mapSize))
            {
                throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size is not supported.");
            }

            Seed = seed;
            MapSize = mapSize;
        }

        /// <summary>
        /// Gets the world seed.
        /// </summary>
        public ulong Seed { get; }

        /// <summary>
        /// Gets the map size.
        /// </summary>
        public MapSize MapSize { get; }

        public static bool operator ==(NewGameRequest left, NewGameRequest right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NewGameRequest left, NewGameRequest right)
        {
            return !left.Equals(right);
        }

        public bool Equals(NewGameRequest other)
        {
            return Seed == other.Seed && MapSize == other.MapSize;
        }

        public override bool Equals(object? obj)
        {
            return obj is NewGameRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Seed, MapSize);
        }
    }
}
