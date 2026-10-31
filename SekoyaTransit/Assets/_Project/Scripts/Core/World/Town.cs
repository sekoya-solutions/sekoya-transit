#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
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
            int loadRoundRobinIndex = 0,
            long monthPassengersGenerated = 0L,
            long monthPassengersTransported = 0L,
            long monthMailGenerated = 0L,
            long monthMailTransported = 0L,
            string name = "",
            TownNeedPlan? needPlan = null,
            int lastMonthGrowth = 0)
        {
#pragma warning disable CA1510
            if (needs is null)
            {
                throw new ArgumentNullException(nameof(needs));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
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

            if (monthPassengersGenerated < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthPassengersGenerated),
                    monthPassengersGenerated,
                    "Month passengers generated cannot be negative.");
            }

            if (monthPassengersTransported < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthPassengersTransported),
                    monthPassengersTransported,
                    "Month passengers transported cannot be negative.");
            }

            if (monthMailGenerated < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthMailGenerated),
                    monthMailGenerated,
                    "Month mail generated cannot be negative.");
            }

            if (monthMailTransported < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthMailTransported),
                    monthMailTransported,
                    "Month mail transported cannot be negative.");
            }

            if (lastMonthGrowth < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lastMonthGrowth),
                    lastMonthGrowth,
                    "Last month growth cannot be negative.");
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
            MonthPassengersGenerated = monthPassengersGenerated;
            MonthPassengersTransported = monthPassengersTransported;
            MonthMailGenerated = monthMailGenerated;
            MonthMailTransported = monthMailTransported;
            Name = name;
            NeedPlan = needPlan ?? TownNeedPlan.Empty;
            LastMonthGrowth = lastMonthGrowth;
        }

        /// <summary>
        /// Gets the town object id (placement-order index).
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the town's display name, a real-world capital chosen at generation (Issue #193). Empty only for a town
        /// built without content (tests); restore names towns from older saves.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the full ordered need list fixed at creation; <see cref="Needs"/> is its prefix for the current
        /// population tier (Issue #193). Empty only for a town built without content.
        /// </summary>
        public TownNeedPlan NeedPlan { get; private set; }

        /// <summary>
        /// Gets the town anchor tile.
        /// </summary>
        public TileCoordinate Coordinate { get; }

        /// <summary>
        /// Gets the current population.
        /// </summary>
        public int Population { get; private set; }

        /// <summary>
        /// Gets the inhabitants gained from needs at the end of the previous stats month, after the population cap
        /// (Issue #242). Contract rewards are not included.
        /// </summary>
        public int LastMonthGrowth { get; private set; }

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
        /// Gets passengers generated this calendar month (pulse total, including wasted excess).
        /// </summary>
        public long MonthPassengersGenerated { get; private set; }

        /// <summary>
        /// Gets passengers loaded onto vehicles this calendar month.
        /// </summary>
        public long MonthPassengersTransported { get; private set; }

        /// <summary>
        /// Gets mail generated this calendar month (pulse total, including wasted excess).
        /// </summary>
        public long MonthMailGenerated { get; private set; }

        /// <summary>
        /// Gets mail loaded onto vehicles this calendar month.
        /// </summary>
        public long MonthMailTransported { get; private set; }

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
        /// Rolls the month counters when <paramref name="date"/> is in a different calendar month.
        /// Does not apply population growth (see <see cref="TownGrowth"/>), so last month's growth becomes 0.
        /// </summary>
        public void EnsureMonthStats(Simulation.GameDate date)
        {
            if (RollToMonth(date))
            {
                LastMonthGrowth = 0;
            }
        }

        /// <summary>
        /// When <paramref name="date"/> is in a different calendar month, moves each need's month supply to last
        /// month's, clears the passenger and mail counters, and starts the new stats month. Returns whether the month
        /// changed. Growth is applied by <see cref="TownGrowth"/> from the rolled supply.
        /// </summary>
        internal bool RollToMonth(Simulation.GameDate date)
        {
            if (StatsYear == date.Year && StatsMonth == date.Month)
            {
                return false;
            }

            RollMonthCounters();
            StatsYear = date.Year;
            StatsMonth = date.Month;
            return true;
        }

        /// <summary>
        /// Records the inhabitants gained from needs at the end of the previous month (Issue #242).
        /// </summary>
        internal void SetLastMonthGrowth(int growth)
        {
            if (growth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(growth), growth, "Last month growth cannot be negative.");
            }

            LastMonthGrowth = growth;
        }

        /// <summary>
        /// Credits passengers generated this month (full pulse amount, including wasted excess).
        /// </summary>
        public void CreditMonthPassengersGenerated(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            MonthPassengersGenerated = checked(MonthPassengersGenerated + amount);
        }

        /// <summary>
        /// Credits passengers loaded onto vehicles this month.
        /// </summary>
        public void CreditMonthPassengersTransported(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            MonthPassengersTransported = checked(MonthPassengersTransported + amount);
        }

        /// <summary>
        /// Credits mail generated this month (full pulse amount, including wasted excess).
        /// </summary>
        public void CreditMonthMailGenerated(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            MonthMailGenerated = checked(MonthMailGenerated + amount);
        }

        /// <summary>
        /// Credits mail loaded onto vehicles this month.
        /// </summary>
        public void CreditMonthMailTransported(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            MonthMailTransported = checked(MonthMailTransported + amount);
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
        /// Returns an independent copy that keeps the name, need plan, needs, waiting cargo and the calendar-month
        /// counters.
        /// </summary>
        public Town Copy()
        {
            return new Town(
                Id,
                Coordinate,
                Population,
                Needs,
                StatsYear,
                StatsMonth,
                WaitingCargo.Stacks,
                PlaceholderPassengerWaitingCapacity,
                LoadRoundRobinIndex,
                MonthPassengersGenerated,
                MonthPassengersTransported,
                MonthMailGenerated,
                MonthMailTransported,
                Name,
                NeedPlan,
                LastMonthGrowth);
        }

        /// <summary>
        /// Sets the name and need plan of a town restored from a save that predates them (Issue #193).
        /// </summary>
        internal void AssignNameAndPlan(string name, TownNeedPlan needPlan)
        {
#pragma warning disable CA1510
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (needPlan is null)
            {
                throw new ArgumentNullException(nameof(needPlan));
            }
#pragma warning restore CA1510

            Name = name;
            NeedPlan = needPlan;
        }

        /// <summary>
        /// Replaces population and needs (generation, save restore, and growth).
        /// Preserves stats month markers and waiting cargo. A need that stays keeps its month and last month supply
        /// (Issue #242); a newly unlocked need starts from the supply given in <paramref name="needs"/>.
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
            for (int index = 0; index < copied.Count; index++)
            {
                TownNeed? kept = FindNeed(copied[index].CargoId);
                if (kept != null)
                {
                    copied[index] = new TownNeed(kept.CargoId, kept.MonthSupply, kept.LastMonthSupply);
                }
            }

            _needs.Clear();
            _needs.AddRange(copied);
            Population = population;
        }

        private TownNeed? FindNeed(string cargoId)
        {
            for (int index = 0; index < _needs.Count; index++)
            {
                if (string.Equals(_needs[index].CargoId, cargoId, StringComparison.Ordinal))
                {
                    return _needs[index];
                }
            }

            return null;
        }

        private void RollMonthCounters()
        {
            for (int index = 0; index < _needs.Count; index++)
            {
                _needs[index].RollMonthSupply();
            }

            MonthPassengersGenerated = 0L;
            MonthPassengersTransported = 0L;
            MonthMailGenerated = 0L;
            MonthMailTransported = 0L;
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

                needCopy.Add(new TownNeed(need.CargoId, need.MonthSupply, need.LastMonthSupply));
            }

            return needCopy;
        }
    }
}
