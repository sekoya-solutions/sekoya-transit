#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A placed town with population, freight needs, monthly supply tracking,
    /// and a waiting bag for passenger-class cargo.
    /// </summary>
    public sealed class Town
    {
        /// <summary>
        /// Placeholder max passenger-class units waiting when settings are unavailable.
        /// </summary>
        public const int PlaceholderPassengerWaitingCapacity = 50;

        private readonly List<TownNeed> _needs;
        private readonly ReadOnlyCollection<TownNeed> _needsView;

        /// <summary>
        /// Initializes a town at an anchor tile.
        /// </summary>
        public Town(
            int id,
            TileCoordinate coordinate,
            int population,
            IReadOnlyList<TownNeed> needs,
            int statsYear = 0,
            int statsMonth = 0,
            IReadOnlyList<CargoAmount>? waitingCargo = null,
            int waitingCapacity = PlaceholderPassengerWaitingCapacity,
            int loadRoundRobinIndex = 0)
        {
#pragma warning disable CA1510
            if (needs is null)
            {
                throw new ArgumentNullException(nameof(needs));
            }
#pragma warning restore CA1510

            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Town id cannot be negative.");
            }

            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Population cannot be negative.");
            }

            if (statsYear < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(statsYear), statsYear, "Stats year cannot be negative.");
            }

            if (statsMonth < 0 || statsMonth > 12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(statsMonth),
                    statsMonth,
                    "Stats month must be 0 (unset) or 1–12.");
            }

            if ((statsYear == 0) != (statsMonth == 0))
            {
                throw new ArgumentException("Stats year and month must both be unset (0) or both set.");
            }

            if (waitingCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waitingCapacity),
                    waitingCapacity,
                    "Waiting capacity must be positive.");
            }

            if (loadRoundRobinIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(loadRoundRobinIndex),
                    loadRoundRobinIndex,
                    "Load round-robin index cannot be negative.");
            }

            _needs = CopyNeeds(needs);
            _needsView = _needs.AsReadOnly();
            Id = id;
            Coordinate = coordinate;
            Population = population;
            StatsYear = statsYear;
            StatsMonth = statsMonth;
            WaitingCargo = waitingCargo == null ? new CargoInventory() : new CargoInventory(waitingCargo);
            WaitingCargo.TrimToMaxTotal(waitingCapacity);
            LoadRoundRobinIndex = loadRoundRobinIndex;
        }

        /// <summary>
        /// Gets the town object id (placement-order index).
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the town anchor tile.
        /// </summary>
        public TileCoordinate Coordinate { get; }

        /// <summary>
        /// Gets the current population.
        /// </summary>
        public int Population { get; private set; }

        /// <summary>
        /// Gets the calendar year for <see cref="TownNeed.MonthSupply"/> counters, or 0 when unset.
        /// </summary>
        public int StatsYear { get; private set; }

        /// <summary>
        /// Gets the calendar month (1–12) for supply counters, or 0 when unset.
        /// </summary>
        public int StatsMonth { get; private set; }

        /// <summary>
        /// Gets the ordered freight needs for the current population tier.
        /// </summary>
        public IReadOnlyList<TownNeed> Needs
        {
            get
            {
                return _needsView;
            }
        }

        /// <summary>
        /// Gets passenger-class cargo waiting to be loaded at this town.
        /// </summary>
        public CargoInventory WaitingCargo { get; }

        /// <summary>
        /// Gets the next vehicle index cursor for town passenger load round-robin.
        /// </summary>
        public int LoadRoundRobinIndex { get; private set; }

        /// <summary>
        /// Gets remaining free waiting units before <paramref name="capacity"/>.
        /// </summary>
        public int GetFreeWaitingCapacity(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            }

            int free = capacity - WaitingCargo.TotalAmount;
            return free < 0 ? 0 : free;
        }

        /// <summary>
        /// Returns whether this town currently needs <paramref name="cargoId"/>.
        /// </summary>
        public bool HasNeed(string cargoId)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                return false;
            }

            for (int index = 0; index < _needs.Count; index++)
            {
                if (string.Equals(_needs[index].CargoId, cargoId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Counts needs whose monthly supply meets or exceeds <paramref name="demandPerNeed"/>.
        /// </summary>
        public int CountSatisfiedNeeds(int demandPerNeed)
        {
            if (demandPerNeed < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(demandPerNeed),
                    demandPerNeed,
                    "Demand per need must be positive.");
            }

            int satisfied = 0;
            for (int index = 0; index < _needs.Count; index++)
            {
                if (_needs[index].MonthSupply >= demandPerNeed)
                {
                    satisfied++;
                }
            }

            return satisfied;
        }

        /// <summary>
        /// Credits monthly supply for a matching need. Returns the amount credited (0 if not a need).
        /// </summary>
        public int CreditNeedSupply(string cargoId, int amount)
        {
            if (string.IsNullOrWhiteSpace(cargoId) || amount <= 0)
            {
                return 0;
            }

            for (int index = 0; index < _needs.Count; index++)
            {
                TownNeed need = _needs[index];
                if (!string.Equals(need.CargoId, cargoId, StringComparison.Ordinal))
                {
                    continue;
                }

                need.CreditMonthSupply(amount);
                return amount;
            }

            return 0;
        }

        /// <summary>
        /// Resets month supply counters when <paramref name="date"/> is in a different calendar month.
        /// Does not apply population growth (see <see cref="TownGrowth"/>).
        /// </summary>
        public void EnsureMonthStats(Simulation.GameDate date)
        {
            if (StatsYear == date.Year && StatsMonth == date.Month)
            {
                return;
            }

            ResetAllNeedSupplies();
            StatsYear = date.Year;
            StatsMonth = date.Month;
        }

        /// <summary>
        /// Applies completed-month growth bookkeeping before resetting counters for <paramref name="date"/>.
        /// Returns the population delta to apply (caller clamps and refreshes needs).
        /// </summary>
        internal int BeginNewMonth(Simulation.GameDate date, int demandPerNeed, int populationPerSatisfiedNeed)
        {
            if (StatsYear == date.Year && StatsMonth == date.Month)
            {
                return 0;
            }

            int growth = 0;
            if (StatsYear != 0 && StatsMonth != 0)
            {
                int satisfied = CountSatisfiedNeeds(demandPerNeed);
                growth = checked(satisfied * populationPerSatisfiedNeed);
            }

            ResetAllNeedSupplies();
            StatsYear = date.Year;
            StatsMonth = date.Month;
            return growth;
        }

        /// <summary>
        /// Adds waiting cargo up to free capacity. Returns the amount actually added.
        /// </summary>
        public int TryAddWaitingCargo(string cargoId, int amount, int capacity)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cargo amount must be positive.");
            }

            int free = GetFreeWaitingCapacity(capacity);
            if (free <= 0)
            {
                return 0;
            }

            int toAdd = amount < free ? amount : free;
            WaitingCargo.Add(cargoId, toAdd);
            return toAdd;
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
        /// Replaces population and needs (generation, save restore, and growth).
        /// Preserves stats month markers and waiting cargo; clears supply on the new need list.
        /// </summary>
        internal void ReplacePopulationAndNeeds(int population, IReadOnlyList<TownNeed> needs)
        {
#pragma warning disable CA1510
            if (needs is null)
            {
                throw new ArgumentNullException(nameof(needs));
            }
#pragma warning restore CA1510

            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Population cannot be negative.");
            }

            List<TownNeed> copied = CopyNeeds(needs);
            _needs.Clear();
            _needs.AddRange(copied);
            Population = population;
        }

        private void ResetAllNeedSupplies()
        {
            for (int index = 0; index < _needs.Count; index++)
            {
                _needs[index].ResetMonthSupply();
            }
        }

        private static List<TownNeed> CopyNeeds(IReadOnlyList<TownNeed> needs)
        {
            var needCopy = new List<TownNeed>(needs.Count);
            var seenCargo = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < needs.Count; index++)
            {
                TownNeed need = needs[index];
#pragma warning disable CA1510
                if (need is null)
                {
                    throw new ArgumentNullException(nameof(needs), $"Town need at index {index} is null.");
                }
#pragma warning restore CA1510

                if (!seenCargo.Add(need.CargoId))
                {
                    throw new ArgumentException($"Duplicate town need cargo '{need.CargoId}'.", nameof(needs));
                }

                needCopy.Add(new TownNeed(need.CargoId, need.MonthSupply));
            }

            return needCopy;
        }
    }
}
