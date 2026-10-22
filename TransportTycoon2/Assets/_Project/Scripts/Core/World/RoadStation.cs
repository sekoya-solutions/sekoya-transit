#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A player-built 1x1 road station anchored on a single map tile.
    /// </summary>
    public readonly struct RoadStation : IEquatable<RoadStation>
    {
        /// <summary>
        /// Initializes a road station entity.
        /// </summary>
        /// <param name="id">Stable non-negative station id.</param>
        /// <param name="coordinate">The tile the station occupies.</param>
        public RoadStation(int id, TileCoordinate coordinate)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Station id cannot be negative.");
            }

            Id = id;
            Coordinate = coordinate;
        }

        /// <summary>
        /// Gets the stable station id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the tile this station occupies (also its road connection point).
        /// </summary>
        public TileCoordinate Coordinate { get; }

        /// <inheritdoc />
        public bool Equals(RoadStation other)
        {
            return Id == other.Id && Coordinate.Equals(other.Coordinate);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RoadStation other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id * 397) ^ Coordinate.GetHashCode();
            }
        }

        /// <summary>
        /// Returns whether two stations are equal.
        /// </summary>
        public static bool operator ==(RoadStation left, RoadStation right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether two stations are not equal.
        /// </summary>
        public static bool operator !=(RoadStation left, RoadStation right)
        {
            return !left.Equals(right);
        }
    }
}
