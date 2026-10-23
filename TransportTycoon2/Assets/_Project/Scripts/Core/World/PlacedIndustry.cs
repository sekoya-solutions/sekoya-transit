#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A deterministically placed industry instance with a capped cargo inventory
    /// and calendar-month production / transport counters (Issue #52).
    /// </summary>
    public sealed class PlacedIndustry : IEquatable<PlacedIndustry>
    {
        /// <summary>
        /// Placeholder shared cargo capacity in cargo units (all types).
        /// </summary>
        public const int PlaceholderCargoCapacity = 40;

        /// <summary>
        /// Initializes a placed industry with an empty cargo inventory and unset month stats.
        /// </summary>
        /// <param name="industryId">The stable industry definition ID.</param>
        /// <param name="instanceIndex">The zero-based instance index within that definition.</param>
        /// <param name="coordinate">The industry anchor tile.</param>
        public PlacedIndustry(string industryId, int instanceIndex, TileCoordinate coordinate)
            : this(industryId, instanceIndex, coordinate, null, 0L, 0L, 0, 0)
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
            : this(industryId, instanceIndex, coordinate, cargo, 0L, 0L, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a placed industry with cargo and calendar-month stats (save restore).
        /// </summary>
        public PlacedIndustry(
            string industryId,
            int instanceIndex,
            TileCoordinate coordinate,
            IReadOnlyList<CargoAmount>? cargo,
            long monthProduced,
            long monthTransported,
            int statsYear,
            int statsMonth)
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

            if (monthProduced < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthProduced),
                    monthProduced,
                    "Month produced cannot be negative.");
            }

            if (monthTransported < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthTransported),
                    monthTransported,
                    "Month transported cannot be negative.");
            }

            if (statsYear < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(statsYear),
                    statsYear,
                    "Stats year cannot be negative.");
            }

            if (statsMonth < 0 || statsMonth > 12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(statsMonth),
                    statsMonth,
                    "Stats month must be 0 (unset) or 1–12.");
            }

            if (statsYear == 0 && statsMonth != 0)
            {
                throw new ArgumentException(
                    "Stats month requires a non-zero stats year when set.",
                    nameof(statsMonth));
            }

            if (statsYear > 0 && statsMonth == 0)
            {
                throw new ArgumentException(
                    "Stats year requires a stats month between 1 and 12.",
                    nameof(statsYear));
            }

            IndustryId = industryId;
            InstanceIndex = instanceIndex;
            Coordinate = coordinate;
            Cargo = cargo == null ? new CargoInventory() : new CargoInventory(cargo);
            Cargo.TrimToMaxTotal(PlaceholderCargoCapacity);
            MonthProduced = monthProduced;
            MonthTransported = monthTransported;
            StatsYear = statsYear;
            StatsMonth = statsMonth;
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
        /// Gets cargo units produced in the current calendar month (recipe outputs created).
        /// </summary>
        public long MonthProduced { get; private set; }

        /// <summary>
        /// Gets cargo units of recipe outputs pushed to adjacent stations in the current calendar month.
        /// </summary>
        public long MonthTransported { get; private set; }

        /// <summary>
        /// Gets the calendar year the month counters apply to, or 0 when unset.
        /// </summary>
        public int StatsYear { get; private set; }

        /// <summary>
        /// Gets the calendar month (1–12) the counters apply to, or 0 when unset.
        /// </summary>
        public int StatsMonth { get; private set; }

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
        /// Resets month counters when <paramref name="date"/> is in a different calendar month.
        /// </summary>
        public void EnsureMonthStats(GameDate date)
        {
            if (StatsYear == date.Year && StatsMonth == date.Month)
            {
                return;
            }

            MonthProduced = 0L;
            MonthTransported = 0L;
            StatsYear = date.Year;
            StatsMonth = date.Month;
        }

        /// <summary>
        /// Adds cargo units to the current month's produced total.
        /// </summary>
        internal void CreditMonthProduced(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            MonthProduced = checked(MonthProduced + amount);
        }

        /// <summary>
        /// Adds cargo units to the current month's transported total.
        /// </summary>
        internal void CreditMonthTransported(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            MonthTransported = checked(MonthTransported + amount);
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
