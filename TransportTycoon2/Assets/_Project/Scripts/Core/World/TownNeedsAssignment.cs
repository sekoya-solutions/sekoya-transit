#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic starting population rolls and freight-need assignment for towns.
    /// </summary>
    public static class TownNeedsAssignment
    {
        /// <summary>
        /// PCG stream selector for starting population rolls ("TOWNPOP").
        /// </summary>
        public const ulong PopulationRandomSequence = 0x544F574E504F5000UL;

        /// <summary>
        /// PCG stream selector for need shuffles ("TOWNNEED").
        /// </summary>
        public const ulong NeedsRandomSequence = 0x544F574E4E454544UL;

        /// <summary>
        /// Rolls a starting population in the configured inclusive range.
        /// </summary>
        public static int RollStartingPopulation(TownNeedsSettings settings, Pcg32Random random)
        {
#pragma warning disable CA1510
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (random is null)
            {
                throw new ArgumentNullException(nameof(random));
            }
#pragma warning restore CA1510

            return random.NextInt32(settings.MinStartingPopulation, settings.MaxStartingPopulation + 1);
        }

        /// <summary>
        /// Builds freight needs for a population using a stable per-level shuffle prefix.
        /// Raising population into a higher tier keeps earlier needs when counts only increase.
        /// </summary>
        public static TownNeed[] AssignNeeds(
            int population,
            TownNeedsSettings settings,
            ContentCatalog catalog,
            Pcg32Random random)
        {
#pragma warning disable CA1510
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (random is null)
            {
                throw new ArgumentNullException(nameof(random));
            }
#pragma warning restore CA1510

            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Population cannot be negative.");
            }

            if (population > settings.MaxPopulation)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(population),
                    population,
                    "Population exceeds the configured maximum.");
            }

            TownNeedTier tier = settings.GetTierForPopulation(population);
            var needs = new List<TownNeed>(tier.Level1Count + tier.Level2Count + tier.Level3Count);
            AppendLevelNeeds(needs, 1, tier.Level1Count, catalog, random);
            AppendLevelNeeds(needs, 2, tier.Level2Count, catalog, random);
            AppendLevelNeeds(needs, 3, tier.Level3Count, catalog, random);
            return needs.ToArray();
        }

        /// <summary>
        /// Creates towns for placed anchors using the world seed and content catalog.
        /// </summary>
        public static Town[] CreateTowns(
            IReadOnlyList<TileCoordinate> townAnchors,
            ulong worldSeed,
            TownNeedsSettings settings,
            ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (townAnchors is null)
            {
                throw new ArgumentNullException(nameof(townAnchors));
            }

            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            var towns = new Town[townAnchors.Count];
            for (int index = 0; index < townAnchors.Count; index++)
            {
                ulong townSeed = unchecked(worldSeed + (ulong)index);
                var populationRandom = new Pcg32Random(townSeed, PopulationRandomSequence);
                int population = RollStartingPopulation(settings, populationRandom);
                var needsRandom = new Pcg32Random(townSeed, NeedsRandomSequence);
                TownNeed[] needs = AssignNeeds(population, settings, catalog, needsRandom);
                towns[index] = new Town(index, townAnchors[index], population, needs);
            }

            return towns;
        }

        /// <summary>
        /// Recomputes needs for a new population using the town's deterministic needs stream.
        /// </summary>
        public static void SetPopulation(
            Town town,
            int population,
            TownNeedsSettings settings,
            ContentCatalog catalog,
            ulong worldSeed)
        {
#pragma warning disable CA1510
            if (town is null)
            {
                throw new ArgumentNullException(nameof(town));
            }
#pragma warning restore CA1510

            ulong townSeed = unchecked(worldSeed + (ulong)town.Id);
            var needsRandom = new Pcg32Random(townSeed, NeedsRandomSequence);
            TownNeed[] needs = AssignNeeds(population, settings, catalog, needsRandom);
            town.ReplacePopulationAndNeeds(population, needs);
        }

        /// <summary>
        /// Creates towns with empty needs and the configured minimum starting population (tests / migration).
        /// </summary>
        public static Town[] CreateTownsWithoutNeeds(
            IReadOnlyList<TileCoordinate> townAnchors,
            int population)
        {
#pragma warning disable CA1510
            if (townAnchors is null)
            {
                throw new ArgumentNullException(nameof(townAnchors));
            }
#pragma warning restore CA1510

            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Population cannot be negative.");
            }

            var towns = new Town[townAnchors.Count];
            for (int index = 0; index < townAnchors.Count; index++)
            {
                towns[index] = new Town(index, townAnchors[index], population, Array.Empty<TownNeed>());
            }

            return towns;
        }

        private static void AppendLevelNeeds(
            List<TownNeed> needs,
            int manufacturingLevel,
            int count,
            ContentCatalog catalog,
            Pcg32Random random)
        {
            if (count == 0)
            {
                return;
            }

            List<string> pool = BuildPool(catalog, manufacturingLevel);
            if (pool.Count < count)
            {
                throw new InvalidOperationException(
                    $"Need assignment requires {count} manufacturing-level-{manufacturingLevel} cargo types, but only {pool.Count} exist.");
            }

            Shuffle(pool, random);
            for (int index = 0; index < count; index++)
            {
                needs.Add(new TownNeed(pool[index]));
            }
        }

        private static List<string> BuildPool(ContentCatalog catalog, int manufacturingLevel)
        {
            var pool = new List<string>();
            for (int index = 0; index < catalog.Cargo.Count; index++)
            {
                CargoDefinition cargo = catalog.Cargo[index];
                if (cargo.Class != CargoClass.Cargo)
                {
                    continue;
                }

                if (cargo.ManufacturingLevel == manufacturingLevel)
                {
                    pool.Add(cargo.Id);
                }
            }

            pool.Sort(StringComparer.Ordinal);
            return pool;
        }

        private static void Shuffle(List<string> values, Pcg32Random random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.NextInt32(0, index + 1);
                string temporary = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = temporary;
            }
        }
    }
}
