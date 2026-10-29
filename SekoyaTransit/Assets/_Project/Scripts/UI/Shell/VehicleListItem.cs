#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one owned vehicle for the vehicles panel.
    /// Presentation builds these from Core; the shell never mutates simulation state.
    /// </summary>
    public readonly struct VehicleListItem : IEquatable<VehicleListItem>
    {
        private readonly CargoStackListLine[] _cargoStacks;

        /// <summary>
        /// Initializes a vehicle list row.
        /// </summary>
        public VehicleListItem(
            VehicleListKind kind,
            int id,
            VehicleListStatusKind status,
            int cargoAmount,
            int cargoCapacity,
            long lifetimeIncome,
            int wagonCount = 0,
            int maxWagons = 0,
            long monthlyUpkeep = 0L,
            IReadOnlyList<CargoStackListLine>? cargoStacks = null)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Vehicle id cannot be negative.");
            }

            if (cargoAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoAmount), cargoAmount, "Cargo amount cannot be negative.");
            }

            if (cargoCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoCapacity), cargoCapacity, "Cargo capacity cannot be negative.");
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

            if (wagonCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wagonCount), wagonCount, "Wagon count cannot be negative.");
            }

            if (maxWagons < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWagons), maxWagons, "Max wagons cannot be negative.");
            }

            if (wagonCount > maxWagons)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count cannot exceed max wagons.");
            }

            if (monthlyUpkeep < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthlyUpkeep),
                    monthlyUpkeep,
                    "Monthly upkeep cannot be negative.");
            }

            if (kind == VehicleListKind.Road && (wagonCount != 0 || maxWagons != 0))
            {
                throw new ArgumentException("Road vehicles do not use wagon counts.", nameof(kind));
            }

            if (cargoStacks == null || cargoStacks.Count == 0)
            {
                _cargoStacks = Array.Empty<CargoStackListLine>();
            }
            else
            {
                _cargoStacks = new CargoStackListLine[cargoStacks.Count];
                for (int index = 0; index < cargoStacks.Count; index++)
                {
                    _cargoStacks[index] = cargoStacks[index];
                }
            }

            Kind = kind;
            Id = id;
            Status = status;
            CargoAmount = cargoAmount;
            CargoCapacity = cargoCapacity;
            LifetimeIncome = lifetimeIncome;
            WagonCount = wagonCount;
            MaxWagons = maxWagons;
            MonthlyUpkeep = monthlyUpkeep;
        }

        /// <summary>
        /// Gets whether this row is a road or rail vehicle.
        /// </summary>
        public VehicleListKind Kind { get; }

        /// <summary>
        /// Gets the stable vehicle id within <see cref="Kind"/>.
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
        /// Gets maximum cargo capacity (0 for a locomotive with no wagons).
        /// </summary>
        public int CargoCapacity { get; }

        /// <summary>
        /// Gets lifetime delivery income attributed to this vehicle.
        /// </summary>
        public long LifetimeIncome { get; }

        /// <summary>
        /// Gets attached wagon count (rail only; 0 for road).
        /// </summary>
        public int WagonCount { get; }

        /// <summary>
        /// Gets the locomotive max wagon count (rail only; 0 for road).
        /// </summary>
        public int MaxWagons { get; }

        /// <summary>
        /// Gets fixed monthly upkeep for this vehicle (rail includes wagons).
        /// </summary>
        public long MonthlyUpkeep { get; }

        /// <summary>
        /// Gets per-cargo stacks currently carried (empty when none).
        /// </summary>
        public IReadOnlyList<CargoStackListLine> CargoStacks => _cargoStacks;

        /// <inheritdoc />
        public bool Equals(VehicleListItem other)
        {
            if (Kind != other.Kind
                || Id != other.Id
                || Status != other.Status
                || CargoAmount != other.CargoAmount
                || CargoCapacity != other.CargoCapacity
                || LifetimeIncome != other.LifetimeIncome
                || WagonCount != other.WagonCount
                || MaxWagons != other.MaxWagons
                || MonthlyUpkeep != other.MonthlyUpkeep
                || _cargoStacks.Length != other._cargoStacks.Length)
            {
                return false;
            }

            for (int index = 0; index < _cargoStacks.Length; index++)
            {
                if (!_cargoStacks[index].Equals(other._cargoStacks[index]))
                {
                    return false;
                }
            }

            return true;
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
                int hash = (int)Kind;
                hash = (hash * 397) ^ Id;
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ CargoAmount;
                hash = (hash * 397) ^ CargoCapacity;
                hash = (hash * 397) ^ LifetimeIncome.GetHashCode();
                hash = (hash * 397) ^ WagonCount;
                hash = (hash * 397) ^ MaxWagons;
                hash = (hash * 397) ^ MonthlyUpkeep.GetHashCode();
                for (int index = 0; index < _cargoStacks.Length; index++)
                {
                    hash = (hash * 397) ^ _cargoStacks[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
