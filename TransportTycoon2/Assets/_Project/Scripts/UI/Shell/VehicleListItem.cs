#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one owned road vehicle for the vehicles panel (Issue #51).
    /// Presentation builds these from Core; the shell never mutates simulation state.
    /// </summary>
    public readonly struct VehicleListItem : IEquatable<VehicleListItem>
    {
        /// <summary>
        /// Initializes a vehicle list row.
        /// </summary>
        public VehicleListItem(
            int id,
            VehicleListStatusKind status,
            int cargoAmount,
            int cargoCapacity,
            long lifetimeIncome)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Vehicle id cannot be negative.");
            }

            if (cargoAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoAmount), cargoAmount, "Cargo amount cannot be negative.");
            }

            if (cargoCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoCapacity), cargoCapacity, "Cargo capacity must be positive.");
            }

            if (cargoAmount > cargoCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cargoAmount),
                    cargoAmount,
                    "Cargo amount cannot exceed capacity.");
            }

            if (lifetimeIncome < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetimeIncome),
                    lifetimeIncome,
                    "Lifetime income cannot be negative.");
            }

            Id = id;
            Status = status;
            CargoAmount = cargoAmount;
            CargoCapacity = cargoCapacity;
            LifetimeIncome = lifetimeIncome;
        }

        /// <summary>
        /// Gets the stable vehicle id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the display status.
        /// </summary>
        public VehicleListStatusKind Status { get; }

        /// <summary>
        /// Gets cargo units currently carried.
        /// </summary>
        public int CargoAmount { get; }

        /// <summary>
        /// Gets maximum cargo capacity.
        /// </summary>
        public int CargoCapacity { get; }

        /// <summary>
        /// Gets lifetime delivery income attributed to this vehicle.
        /// </summary>
        public long LifetimeIncome { get; }

        /// <inheritdoc />
        public bool Equals(VehicleListItem other)
        {
            return Id == other.Id
                && Status == other.Status
                && CargoAmount == other.CargoAmount
                && CargoCapacity == other.CargoCapacity
                && LifetimeIncome == other.LifetimeIncome;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is VehicleListItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id;
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ CargoAmount;
                hash = (hash * 397) ^ CargoCapacity;
                hash = (hash * 397) ^ LifetimeIncome.GetHashCode();
                return hash;
            }
        }
    }
}
