#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A player-built 1x1 road station anchored on a single map tile, with a capped cargo bag.
    /// </summary>
    public sealed class RoadStation : IEquatable<RoadStation>
    {
        /// <summary>
        /// Placeholder shared cargo capacity in cargo units (all types).
        /// </summary>
        public const int PlaceholderCargoCapacity = 40;

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
        /// Over-capacity cargo is trimmed to <see cref="PlaceholderCargoCapacity"/>.
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
            Cargo.TrimToMaxTotal(PlaceholderCargoCapacity);
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
        /// Gets the station cargo bag (capped at <see cref="PlaceholderCargoCapacity"/>).
        /// </summary>
        public CargoInventory Cargo { get; }

        /// <summary>
        /// Gets remaining free cargo units before the capacity cap.
        /// </summary>
        public int FreeCargoCapacity
        {
            get
            {
                int free = PlaceholderCargoCapacity - Cargo.TotalAmount;
                return free < 0 ? 0 : free;
            }
        }

        /// <summary>
        /// Adds cargo up to free capacity. Returns the amount actually added.
        /// </summary>
        public int TryAddCargo(string cargoId, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cargo amount must be positive.");
            }

            int free = FreeCargoCapacity;
            if (free <= 0)
            {
                return 0;
            }

            int toAdd = amount < free ? amount : free;
            Cargo.Add(cargoId, toAdd);
            return toAdd;
        }

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
