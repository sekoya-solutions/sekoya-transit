#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// What the player picked on the new game screen: a world seed and a map size (Issue #67).
    /// </summary>
    public readonly struct NewGameSelection : IEquatable<NewGameSelection>
    {
        /// <summary>
        /// Initializes a selection.
        /// </summary>
        public NewGameSelection(ulong seed, NewGameMapSize mapSize)
        {
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
        public NewGameMapSize MapSize { get; }

        public static bool operator ==(NewGameSelection left, NewGameSelection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NewGameSelection left, NewGameSelection right)
        {
            return !left.Equals(right);
        }

        public bool Equals(NewGameSelection other)
        {
            return Seed == other.Seed && MapSize == other.MapSize;
        }

        public override bool Equals(object? obj)
        {
            return obj is NewGameSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Seed.GetHashCode() * 397) ^ (int)MapSize;
            }
        }
    }
}
