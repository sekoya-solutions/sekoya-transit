#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A zero-based coordinate in a tile grid.
    /// </summary>
    public readonly struct TileCoordinate : IEquatable<TileCoordinate>
    {
        /// <summary>
        /// Initializes a zero-based tile coordinate.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        public TileCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Gets the zero-based horizontal coordinate.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the zero-based vertical coordinate.
        /// </summary>
        public int Y { get; }

        /// <inheritdoc />
        public bool Equals(TileCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TileCoordinate other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(TileCoordinate left, TileCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TileCoordinate left, TileCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
