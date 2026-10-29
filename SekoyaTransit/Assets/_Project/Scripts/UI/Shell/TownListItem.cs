#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one town for the towns panel (Issue #60).
    /// Presentation builds these from Core; the shell never mutates simulation state.
    /// </summary>
    public readonly struct TownListItem : IEquatable<TownListItem>
    {
        private readonly TownNeedListLine[] _needs;

        /// <summary>
        /// Initializes a town list row.
        /// </summary>
        public TownListItem(
            int townId,
            int tileX,
            int tileY,
            int population,
            int satisfiedNeeds,
            int totalNeeds,
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

            if (satisfiedNeeds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(satisfiedNeeds),
                    satisfiedNeeds,
                    "Satisfied needs cannot be negative.");
            }

            if (totalNeeds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalNeeds), totalNeeds, "Total needs cannot be negative.");
            }

            if (satisfiedNeeds > totalNeeds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(satisfiedNeeds),
                    satisfiedNeeds,
                    "Satisfied needs cannot exceed total needs.");
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
            SatisfiedNeeds = satisfiedNeeds;
            TotalNeeds = totalNeeds;
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
        /// Gets needs with supply ≥ demand this month.
        /// </summary>
        public int SatisfiedNeeds { get; }

        /// <summary>
        /// Gets total freight needs.
        /// </summary>
        public int TotalNeeds { get; }

        /// <summary>
        /// Gets freight need lines in assignment order.
        /// </summary>
        public IReadOnlyList<TownNeedListLine> Needs => _needs;

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

        /// <inheritdoc />
        public bool Equals(TownListItem other)
        {
            if (TownId != other.TownId
                || !string.Equals(Name, other.Name, StringComparison.Ordinal)
                || TileX != other.TileX
                || TileY != other.TileY
                || Population != other.Population
                || SatisfiedNeeds != other.SatisfiedNeeds
                || TotalNeeds != other.TotalNeeds
                || MonthPassengersGenerated != other.MonthPassengersGenerated
                || MonthPassengersTransported != other.MonthPassengersTransported
                || MonthMailGenerated != other.MonthMailGenerated
                || MonthMailTransported != other.MonthMailTransported
                || _needs.Length != other._needs.Length)
            {
                return false;
            }

            for (int index = 0; index < _needs.Length; index++)
            {
                if (!_needs[index].Equals(other._needs[index]))
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
                hash = (hash * 397) ^ TileX;
                hash = (hash * 397) ^ TileY;
                hash = (hash * 397) ^ Population;
                hash = (hash * 397) ^ SatisfiedNeeds;
                hash = (hash * 397) ^ TotalNeeds;
                hash = (hash * 397) ^ MonthPassengersGenerated.GetHashCode();
                hash = (hash * 397) ^ MonthPassengersTransported.GetHashCode();
                hash = (hash * 397) ^ MonthMailGenerated.GetHashCode();
                hash = (hash * 397) ^ MonthMailTransported.GetHashCode();
                for (int index = 0; index < _needs.Length; index++)
                {
                    hash = (hash * 397) ^ _needs[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
