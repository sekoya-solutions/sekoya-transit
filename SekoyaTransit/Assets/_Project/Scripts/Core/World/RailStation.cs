#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A player-built 1x1 rail station anchored on a single map tile.
    /// Stations are stops only: they hold no cargo inventory (Issue #54 / #132).
    /// </summary>
    public sealed class RailStation : IEquatable<RailStation>
    {
        /// <summary>
        /// Initializes a rail station entity.
        /// </summary>
        /// <param name="id">Stable non-negative station id.</param>
        /// <param name="coordinate">The tile the station occupies.</param>
        public RailStation(int id, TileCoordinate coordinate)
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
        /// Gets the tile this station occupies (also its rail connection point).
        /// </summary>
        public TileCoordinate Coordinate { get; }

        /// <inheritdoc />
        public bool Equals(RailStation? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id == other.Id && Coordinate.Equals(other.Coordinate);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as RailStation);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id * 397) ^ Coordinate.GetHashCode();
            }
        }
    }
}
