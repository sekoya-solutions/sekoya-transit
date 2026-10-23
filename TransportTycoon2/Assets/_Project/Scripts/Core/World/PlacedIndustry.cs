#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A deterministically placed industry instance with a capped cargo inventory.
    /// </summary>
    public sealed class PlacedIndustry : IEquatable<PlacedIndustry>
    {
        /// <summary>
        /// Placeholder shared cargo capacity in cargo units (all types).
        /// </summary>
        public const int PlaceholderCargoCapacity = 40;

        /// <summary>
        /// Initializes a placed industry with an empty cargo inventory.
        /// </summary>
        /// <param name="industryId">The stable industry definition ID.</param>
        /// <param name="instanceIndex">The zero-based instance index within that definition.</param>
        /// <param name="coordinate">The industry anchor tile.</param>
        public PlacedIndustry(string industryId, int instanceIndex, TileCoordinate coordinate)
            : this(industryId, instanceIndex, coordinate, null)
        {
        }

        /// <summary>
        /// Initializes a placed industry with optional cargo stacks (save restore / tests).
        /// Over-capacity cargo is trimmed to <see cref="PlaceholderCargoCapacity"/>.
        /// </summary>
        public PlacedIndustry(
            string industryId,
            int instanceIndex,
            TileCoordinate coordinate,
            IReadOnlyList<CargoAmount>? cargo)
        {
            if (string.IsNullOrWhiteSpace(industryId))
            {
                throw new ArgumentException("Industry ID cannot be null or whitespace.", nameof(industryId));
            }

            if (instanceIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(instanceIndex),
                    instanceIndex,
                    "Industry instance indices cannot be negative.");
            }

            IndustryId = industryId;
            InstanceIndex = instanceIndex;
            Coordinate = coordinate;
            Cargo = cargo == null ? new CargoInventory() : new CargoInventory(cargo);
            Cargo.TrimToMaxTotal(PlaceholderCargoCapacity);
        }

        /// <summary>
        /// Gets the stable industry definition ID.
        /// </summary>
        public string IndustryId { get; }

        /// <summary>
        /// Gets the zero-based instance index within the definition.
        /// </summary>
        public int InstanceIndex { get; }

        /// <summary>
        /// Gets the industry anchor tile.
        /// </summary>
        public TileCoordinate Coordinate { get; }

        /// <summary>
        /// Gets the industry cargo bag (inputs waiting and outputs waiting).
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
        public bool Equals(PlacedIndustry? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(IndustryId, other.IndustryId, StringComparison.Ordinal)
                && InstanceIndex == other.InstanceIndex
                && Coordinate.Equals(other.Coordinate);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as PlacedIndustry);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IndustryId.GetHashCode();
                hash = (hash * 397) ^ InstanceIndex;
                hash = (hash * 397) ^ Coordinate.GetHashCode();
                return hash;
            }
        }
    }
}
