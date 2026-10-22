#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A player-built 1x1 road station anchored on a single map tile, with a cargo bag.
    /// </summary>
    public sealed class RoadStation : IEquatable<RoadStation>
    {
        /// <summary>
        /// Initializes a road station entity with an empty cargo inventory.
        /// </summary>
        /// <param name="id">Stable non-negative station id.</param>
        /// <param name="coordinate">The tile the station occupies.</param>
        public RoadStation(int id, TileCoordinate coordinate)
            : this(id, coordinate, null)
        {
        }

        /// <summary>
        /// Initializes a road station with optional cargo stacks (save restore / tests).
        /// </summary>
        public RoadStation(int id, TileCoordinate coordinate, IReadOnlyList<CargoAmount>? cargo)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Station id cannot be negative.");
            }

            Id = id;
            Coordinate = coordinate;
            Cargo = cargo == null ? new CargoInventory() : new CargoInventory(cargo);
        }

        /// <summary>
        /// Gets the stable station id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the tile this station occupies (also its road connection point).
        /// </summary>
        public TileCoordinate Coordinate { get; }

        /// <summary>
        /// Gets the station cargo bag (no cap or decay in Issue #46).
        /// </summary>
        public CargoInventory Cargo { get; }

        /// <inheritdoc />
        public bool Equals(RoadStation? other)
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
            return Equals(obj as RoadStation);
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
