#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one town for the towns panel (Issue #60, Issue #242).
    /// Presentation builds these from Core; the shell never mutates simulation state.
    /// </summary>
    public readonly struct TownListItem : IEquatable<TownListItem>
    {
        private readonly TownNeedListLine[] _needs;

        /// <summary>
        /// Initializes a town card. <paramref name="growthLevel"/> is the index of the town's population tier and
        /// <paramref name="maxGrowthLevel"/> the index of the last tier; <paramref name="nextLevelPopulation"/> is the
        /// population that reaches the next tier, or 0 at the last tier.
        /// </summary>
        public TownListItem(
            int townId,
            int tileX,
            int tileY,
            int population,
            int growthLevel,
            int maxGrowthLevel,
            int nextLevelPopulation,
            int lastMonthGrowth,
            IReadOnlyList<TownNeedListLine> needs,
            long monthPassengersGenerated,
            long monthPassengersTransported,
            long monthMailGenerated,
            long monthMailTransported,
            string name = "")
        {
#pragma warning disable CA1510
            if (needs is null)
            {
                throw new ArgumentNullException(nameof(needs));
            }
#pragma warning restore CA1510

            if (townId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(townId), townId, "Town id cannot be negative.");
            }

            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Population cannot be negative.");
            }

            if (maxGrowthLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxGrowthLevel),
                    maxGrowthLevel,
                    "Max growth level cannot be negative.");
            }

            if (growthLevel < 0 || growthLevel > maxGrowthLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(growthLevel),
                    growthLevel,
                    "Growth level must be between 0 and the max growth level.");
            }

            if (growthLevel < maxGrowthLevel ? nextLevelPopulation <= population : nextLevelPopulation != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextLevelPopulation),
                    nextLevelPopulation,
                    "Next level population must be above the population, or 0 at the max growth level.");
            }

            if (lastMonthGrowth < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lastMonthGrowth),
                    lastMonthGrowth,
                    "Last month growth cannot be negative.");
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

            _needs = new TownNeedListLine[needs.Count];
            for (int index = 0; index < needs.Count; index++)
            {
                _needs[index] = needs[index];
            }

            TownId = townId;
            TileX = tileX;
            TileY = tileY;
            Population = population;
            GrowthLevel = growthLevel;
            MaxGrowthLevel = maxGrowthLevel;
            NextLevelPopulation = nextLevelPopulation;
            LastMonthGrowth = lastMonthGrowth;
            MonthPassengersGenerated = monthPassengersGenerated;
            MonthPassengersTransported = monthPassengersTransported;
            MonthMailGenerated = monthMailGenerated;
            MonthMailTransported = monthMailTransported;
            Name = name ?? string.Empty;
        }

        /// <summary>
        /// Gets the town id (placement-order index).
        /// </summary>
        public int TownId { get; }

        /// <summary>
        /// Gets the town name (Issue #193); empty when the world has no names.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the town anchor tile X.
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the town anchor tile Y.
        /// </summary>
        public int TileY { get; }

        /// <summary>
        /// Gets current population.
        /// </summary>
        public int Population { get; }

        /// <summary>
        /// Gets the growth level: the index of the town's population tier, from 0.
        /// </summary>
        public int GrowthLevel { get; }

        /// <summary>
        /// Gets the highest growth level (the index of the last population tier).
        /// </summary>
        public int MaxGrowthLevel { get; }

        /// <summary>
        /// Gets the population that reaches the next growth level, or 0 at the highest level.
        /// </summary>
        public int NextLevelPopulation { get; }

        /// <summary>
        /// Gets the inhabitants gained from needs at the end of last month.
        /// </summary>
        public int LastMonthGrowth { get; }

        /// <summary>
        /// Gets whether the town reached the highest growth level.
        /// </summary>
        public bool IsMaxGrowthLevel => GrowthLevel == MaxGrowthLevel;

        /// <summary>
        /// Gets freight need lines in assignment order (raw, then processed, then finished).
        /// </summary>
        public IReadOnlyList<TownNeedListLine> Needs => _needs ?? Array.Empty<TownNeedListLine>();

        /// <summary>
        /// Gets passengers generated this month.
        /// </summary>
        public long MonthPassengersGenerated { get; }

        /// <summary>
        /// Gets passengers transported this month.
        /// </summary>
        public long MonthPassengersTransported { get; }

        /// <summary>
        /// Gets mail generated this month.
        /// </summary>
        public long MonthMailGenerated { get; }

        /// <summary>
        /// Gets mail transported this month.
        /// </summary>
        public long MonthMailTransported { get; }

        /// <summary>
        /// Counts the needs of one level.
        /// </summary>
        public int CountNeeds(int level)
        {
            int count = 0;
            IReadOnlyList<TownNeedListLine> needs = Needs;
            for (int index = 0; index < needs.Count; index++)
            {
                if (needs[index].Level == level)
                {
                    count++;
                }
            }

            return count;
        }

        /// <inheritdoc />
        public bool Equals(TownListItem other)
        {
            IReadOnlyList<TownNeedListLine> needs = Needs;
            IReadOnlyList<TownNeedListLine> otherNeeds = other.Needs;
            if (TownId != other.TownId
                || !string.Equals(Name, other.Name, StringComparison.Ordinal)
                || TileX != other.TileX
                || TileY != other.TileY
                || Population != other.Population
                || GrowthLevel != other.GrowthLevel
                || MaxGrowthLevel != other.MaxGrowthLevel
                || NextLevelPopulation != other.NextLevelPopulation
                || LastMonthGrowth != other.LastMonthGrowth
                || MonthPassengersGenerated != other.MonthPassengersGenerated
                || MonthPassengersTransported != other.MonthPassengersTransported
                || MonthMailGenerated != other.MonthMailGenerated
                || MonthMailTransported != other.MonthMailTransported
                || needs.Count != otherNeeds.Count)
            {
                return false;
            }

            for (int index = 0; index < needs.Count; index++)
            {
                if (!needs[index].Equals(otherNeeds[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TownListItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TownId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name ?? string.Empty);
                hash = (hash * 397) ^ TileX;
                hash = (hash * 397) ^ TileY;
                hash = (hash * 397) ^ Population;
                hash = (hash * 397) ^ GrowthLevel;
                hash = (hash * 397) ^ MaxGrowthLevel;
                hash = (hash * 397) ^ NextLevelPopulation;
                hash = (hash * 397) ^ LastMonthGrowth;
                hash = (hash * 397) ^ MonthPassengersGenerated.GetHashCode();
                hash = (hash * 397) ^ MonthPassengersTransported.GetHashCode();
                hash = (hash * 397) ^ MonthMailGenerated.GetHashCode();
                hash = (hash * 397) ^ MonthMailTransported.GetHashCode();
                IReadOnlyList<TownNeedListLine> needs = Needs;
                for (int index = 0; index < needs.Count; index++)
                {
                    hash = (hash * 397) ^ needs[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
