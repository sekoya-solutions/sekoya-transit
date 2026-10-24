#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A placed town with population and freight needs.
    /// </summary>
    public sealed class Town
    {
        private readonly List<TownNeed> _needs;
        private readonly ReadOnlyCollection<TownNeed> _needsView;

        /// <summary>
        /// Initializes a town at an anchor tile.
        /// </summary>
        public Town(int id, TileCoordinate coordinate, int population, IReadOnlyList<TownNeed> needs)
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

            _needs = CopyNeeds(needs);
            _needsView = _needs.AsReadOnly();
            Id = id;
            Coordinate = coordinate;
            Population = population;
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
        /// Replaces population and needs (generation, save restore, and growth).
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

        private static List<TownNeed> CopyNeeds(IReadOnlyList<TownNeed> needs)
        {
            var needCopy = new List<TownNeed>(needs.Count);
            var seenCargo = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < needs.Count; index++)
            {
                TownNeed need = needs[index];
                if (!seenCargo.Add(need.CargoId))
                {
                    throw new ArgumentException($"Duplicate town need cargo '{need.CargoId}'.", nameof(needs));
                }

                needCopy.Add(need);
            }

            return needCopy;
        }
    }
}
