#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A deterministically placed industry instance with a capped output stock, a hidden input leftover,
    /// and calendar-month production / transport counters (Issue #52, Issue #193).
    /// </summary>
    public sealed class PlacedIndustry : IEquatable<PlacedIndustry>
    {
        /// <summary>
        /// Placeholder output stock capacity in cargo units (all output types; the input leftover does not count).
        /// </summary>
        public const int PlaceholderCargoCapacity = 40;

        /// <summary>
        /// Initializes a placed industry with an empty cargo inventory and unset month stats.
        /// </summary>
        /// <param name="industryId">The stable industry definition ID.</param>
        /// <param name="instanceIndex">The zero-based instance index within that definition.</param>
        /// <param name="coordinate">The industry anchor tile.</param>
        public PlacedIndustry(string industryId, int instanceIndex, TileCoordinate coordinate)
            : this(industryId, instanceIndex, coordinate, null, 0L, 0L, 0, 0, 0)
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
            : this(industryId, instanceIndex, coordinate, cargo, 0L, 0L, 0, 0, 0)
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
            : this(
                industryId,
                instanceIndex,
                coordinate,
                cargo,
                monthProduced,
                monthTransported,
                statsYear,
                statsMonth,
                0)
        {
        }

        /// <summary>
        /// Initializes a placed industry with cargo, month stats, and load round-robin cursor (save restore).
        /// </summary>
        public PlacedIndustry(
            string industryId,
            int instanceIndex,
            TileCoordinate coordinate,
            IReadOnlyList<CargoAmount>? cargo,
            long monthProduced,
            long monthTransported,
            int statsYear,
            int statsMonth,
            int loadRoundRobinIndex)
            : this(
                industryId,
                instanceIndex,
                coordinate,
                cargo,
                monthProduced,
                monthTransported,
                statsYear,
                statsMonth,
                loadRoundRobinIndex,
                null)
        {
        }

        /// <summary>
        /// Initializes a placed industry with output stock, month stats, load cursor, and the hidden input leftover
        /// (save restore, Issue #193).
        /// </summary>
        public PlacedIndustry(
            string industryId,
            int instanceIndex,
            TileCoordinate coordinate,
            IReadOnlyList<CargoAmount>? cargo,
            long monthProduced,
            long monthTransported,
            int statsYear,
            int statsMonth,
            int loadRoundRobinIndex,
            IReadOnlyList<CargoAmount>? inputLeftover,
            int productionLevel = 0,
            int servedMonths = 0)
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

            if (productionLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(productionLevel),
                    productionLevel,
                    "Production level cannot be negative.");
            }

            if (servedMonths < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(servedMonths),
                    servedMonths,
                    "Served months cannot be negative.");
            }

            if (loadRoundRobinIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(loadRoundRobinIndex),
                    loadRoundRobinIndex,
                    "Load round-robin index cannot be negative.");
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
            LoadRoundRobinIndex = loadRoundRobinIndex;
            InputLeftover = inputLeftover == null ? new CargoInventory() : new CargoInventory(inputLeftover);
            ProductionLevel = productionLevel;
            ServedMonths = servedMonths;
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
        /// Gets the output stock waiting to be loaded. Delivered inputs are never stored here (Issue #193).
        /// </summary>
        public CargoInventory Cargo { get; }

        /// <summary>
        /// Gets delivered input units that do not yet complete a recipe. Hidden from the player and not counted
        /// toward <see cref="PlaceholderCargoCapacity"/> (Issue #193).
        /// </summary>
        public CargoInventory InputLeftover { get; }

        /// <summary>
        /// Gets cargo units produced in the current calendar month (recipe outputs created).
        /// </summary>
        public long MonthProduced { get; private set; }

        /// <summary>
        /// Gets cargo units loaded onto vehicles from this industry in the current calendar month.
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
        /// Gets the next vehicle index cursor for per-industry load round-robin (Issue #132).
        /// </summary>
        public int LoadRoundRobinIndex { get; private set; }

        /// <summary>
        /// Gets the production level of a primary industry (0 = base output); see <see cref="IndustryGrowth"/> (Issue #73).
        /// </summary>
        public int ProductionLevel { get; private set; }

        /// <summary>
        /// Gets the consecutive well-served months counted toward the next production level.
        /// </summary>
        public int ServedMonths { get; private set; }

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
        /// Returns an independent copy that keeps cargo, the calendar-month counters and the load cursor.
        /// </summary>
        public PlacedIndustry Copy()
        {
            return new PlacedIndustry(
                IndustryId,
                InstanceIndex,
                Coordinate,
                Cargo.Stacks,
                MonthProduced,
                MonthTransported,
                StatsYear,
                StatsMonth,
                LoadRoundRobinIndex,
                InputLeftover.Stacks,
                ProductionLevel,
                ServedMonths);
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
        /// Sets the production level and the well-served month streak.
        /// </summary>
        internal void SetGrowth(int productionLevel, int servedMonths)
        {
            if (productionLevel < 0 || servedMonths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productionLevel), "Growth values cannot be negative.");
            }

            ProductionLevel = productionLevel;
            ServedMonths = servedMonths;
        }

        /// <summary>
        /// Sets the load round-robin cursor (non-negative).
        /// </summary>
        internal void SetLoadRoundRobinIndex(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Load round-robin index cannot be negative.");
            }

            LoadRoundRobinIndex = index;
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
