#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Deterministic starting population rolls, town names, and freight-need plans for towns (Issue #57, Issue #193).
    /// </summary>
    public static class TownNeedsAssignment
    {
        /// <summary>
        /// PCG stream selector for starting population rolls ("TOWNPOP").
        /// </summary>
        public const ulong PopulationRandomSequence = 0x544F574E504F5000UL;

        /// <summary>
        /// PCG stream selector for the per-town need shuffles of saves that predate need plans ("TOWNNEED").
        /// </summary>
        public const ulong NeedsRandomSequence = 0x544F574E4E454544UL;

        /// <summary>
        /// PCG stream selector for the map-wide need deal ("TOWNDEAL").
        /// </summary>
        public const ulong DealRandomSequence = 0x544F574E4445414CUL;

        /// <summary>
        /// PCG stream selector for town name picks ("TOWNNAME").
        /// </summary>
        public const ulong NamesRandomSequence = 0x544F574E4E414D45UL;

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
        /// Returns the needs for a population: per level, the prefix of the plan whose length comes from the tier.
        /// Raising population into a higher tier keeps earlier needs.
        /// </summary>
        public static TownNeed[] NeedsForPopulation(int population, TownNeedsSettings settings, TownNeedPlan plan)
        {
#pragma warning disable CA1510
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (plan is null)
            {
                throw new ArgumentNullException(nameof(plan));
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
            AppendPrefix(needs, plan.GetLevel(1), tier.Level1Count);
            AppendPrefix(needs, plan.GetLevel(2), tier.Level2Count);
            AppendPrefix(needs, plan.GetLevel(3), tier.Level3Count);
            return needs.ToArray();
        }

        /// <summary>
        /// Returns the fewest towns for which the deal lets every level 1–3 freight cargo be planned by at least
        /// <see cref="TownNeedsSettings.MinTownsPerNeededCargo"/> towns. Levels that no tier ever needs are skipped.
        /// </summary>
        public static int MinimumTownCount(TownNeedsSettings settings, ContentCatalog catalog)
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
#pragma warning restore CA1510

            TownNeedTier maxTier = settings.GetTierForPopulation(settings.MaxPopulation);
            int minimum = 0;
            for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
            {
                int poolCount = BuildPool(catalog, level).Count;
                int perTown = Math.Min(GetCount(maxTier, level), poolCount);
                if (perTown == 0)
                {
                    continue;
                }

                int required = checked((settings.MinTownsPerNeededCargo * poolCount) + perTown - 1) / perTown;
                minimum = Math.Max(minimum, required);
            }

            return minimum;
        }

        /// <summary>
        /// Deals every town's full need plan from one seeded map-wide deck per level (Issue #193). Each town takes the
        /// next run of distinct cards from a cycle through the shuffled cargo pool, so every cargo is planned by at least
        /// <see cref="TownNeedsSettings.MinTownsPerNeededCargo"/> towns once the town count reaches
        /// <see cref="MinimumTownCount"/>. The towns' places in the deal and each town's card order are shuffled too.
        /// </summary>
        public static TownNeedPlan[] DealNeedPlans(
            int townCount,
            ulong worldSeed,
            TownNeedsSettings settings,
            ContentCatalog catalog)
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
#pragma warning restore CA1510

            if (townCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(townCount), townCount, "Town count cannot be negative.");
            }

            var random = new Pcg32Random(worldSeed, DealRandomSequence);
            var slotTowns = new List<int>(townCount);
            for (int index = 0; index < townCount; index++)
            {
                slotTowns.Add(index);
            }

            Shuffle(slotTowns, random);
            TownNeedTier maxTier = settings.GetTierForPopulation(settings.MaxPopulation);
            var levels = new string[TownNeedPlan.MaxLevel][][];
            for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
            {
                List<string> pool = BuildPool(catalog, level);
                int perTown = Math.Min(GetCount(maxTier, level), pool.Count);
                var townCards = new string[townCount][];
                Shuffle(pool, random);
                for (int slot = 0; slot < townCount; slot++)
                {
                    var cards = new List<string>(perTown);
                    for (int card = 0; card < perTown; card++)
                    {
                        cards.Add(pool[(int)((((long)slot * perTown) + card) % pool.Count)]);
                    }

                    Shuffle(cards, random);
                    townCards[slotTowns[slot]] = cards.ToArray();
                }

                levels[level - 1] = townCards;
            }

            var plans = new TownNeedPlan[townCount];
            for (int index = 0; index < townCount; index++)
            {
                plans[index] = new TownNeedPlan(levels[0][index], levels[1][index], levels[2][index]);
            }

            return plans;
        }

        /// <summary>
        /// Rebuilds the plan of a town from a save that predates plans. It replays that town's own seeded per-level
        /// shuffle, so its saved needs stay a prefix of the plan.
        /// </summary>
        public static TownNeedPlan CreateLegacyPlan(
            int townId,
            ulong worldSeed,
            TownNeedsSettings settings,
            ContentCatalog catalog)
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
#pragma warning restore CA1510

            ulong townSeed = unchecked(worldSeed + (ulong)townId);
            var random = new Pcg32Random(townSeed, NeedsRandomSequence);
            TownNeedTier maxTier = settings.GetTierForPopulation(settings.MaxPopulation);
            var levels = new string[TownNeedPlan.MaxLevel][];
            for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
            {
                int count = GetCount(maxTier, level);
                if (count == 0)
                {
                    levels[level - 1] = Array.Empty<string>();
                    continue;
                }

                List<string> pool = BuildPool(catalog, level);
                Shuffle(pool, random);
                levels[level - 1] = pool.GetRange(0, Math.Min(count, pool.Count)).ToArray();
            }

            return new TownNeedPlan(levels[0], levels[1], levels[2]);
        }

        /// <summary>
        /// Picks <paramref name="townCount"/> distinct names from a seeded shuffle of the name list. Returns empty
        /// names when the list is empty (placeholder content).
        /// </summary>
        public static string[] PickNames(IReadOnlyList<string> names, int townCount, ulong worldSeed)
        {
#pragma warning disable CA1510
            if (names is null)
            {
                throw new ArgumentNullException(nameof(names));
            }
#pragma warning restore CA1510

            if (townCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(townCount), townCount, "Town count cannot be negative.");
            }

            var picked = new string[townCount];
            if (names.Count == 0)
            {
                for (int index = 0; index < picked.Length; index++)
                {
                    picked[index] = string.Empty;
                }

                return picked;
            }

            if (names.Count < townCount)
            {
                throw new InvalidOperationException(
                    $"{townCount} towns need distinct names, but the town name list only has {names.Count}.");
            }

            var shuffled = new List<string>(names);
            Shuffle(shuffled, new Pcg32Random(worldSeed, NamesRandomSequence));
            shuffled.CopyTo(0, picked, 0, townCount);
            return picked;
        }

        /// <summary>
        /// Creates named towns for placed anchors with dealt need plans, using the world seed and content catalog.
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

            TownNeedPlan[] plans = DealNeedPlans(townAnchors.Count, worldSeed, settings, catalog);
            string[] names = PickNames(settings.Names, townAnchors.Count, worldSeed);
            var towns = new Town[townAnchors.Count];
            for (int index = 0; index < townAnchors.Count; index++)
            {
                ulong townSeed = unchecked(worldSeed + (ulong)index);
                var populationRandom = new Pcg32Random(townSeed, PopulationRandomSequence);
                int population = RollStartingPopulation(settings, populationRandom);
                TownNeed[] needs = NeedsForPopulation(population, settings, plans[index]);
                towns[index] = new Town(
                    index,
                    townAnchors[index],
                    population,
                    needs,
                    name: names[index],
                    needPlan: plans[index]);
            }

            return towns;
        }

        /// <summary>
        /// Names and plans towns restored from a save that predates names and need plans (Issue #193). Towns that
        /// already have them are left unchanged.
        /// </summary>
        public static void AssignMissingNamesAndPlans(
            IReadOnlyList<Town> towns,
            ulong worldSeed,
            TownNeedsSettings settings,
            ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (towns is null)
            {
                throw new ArgumentNullException(nameof(towns));
            }
#pragma warning restore CA1510

            string[]? names = null;
            for (int index = 0; index < towns.Count; index++)
            {
                Town town = towns[index];
                if (town.Name.Length > 0 && !town.NeedPlan.IsEmpty)
                {
                    continue;
                }

                if (names == null)
                {
                    names = PickNames(settings.Names, towns.Count, worldSeed);
                }

                string name = town.Name.Length > 0 ? town.Name : names[index];
                TownNeedPlan plan = town.NeedPlan.IsEmpty
                    ? CreateLegacyPlan(town.Id, worldSeed, settings, catalog)
                    : town.NeedPlan;
                town.AssignNameAndPlan(name, plan);
            }
        }

        /// <summary>
        /// Recomputes needs for a new population from the town's need plan.
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

            TownNeedPlan plan = town.NeedPlan.IsEmpty
                ? CreateLegacyPlan(town.Id, worldSeed, settings, catalog)
                : town.NeedPlan;
            town.ReplacePopulationAndNeeds(population, NeedsForPopulation(population, settings, plan));
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

        private static void AppendPrefix(List<TownNeed> needs, IReadOnlyList<string> planned, int count)
        {
            int take = Math.Min(count, planned.Count);
            for (int index = 0; index < take; index++)
            {
                needs.Add(new TownNeed(planned[index]));
            }
        }

        private static int GetCount(TownNeedTier tier, int manufacturingLevel)
        {
            switch (manufacturingLevel)
            {
                case 1:
                    return tier.Level1Count;
                case 2:
                    return tier.Level2Count;
                default:
                    return tier.Level3Count;
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

        private static void Shuffle<T>(List<T> values, Pcg32Random random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.NextInt32(0, index + 1);
                T temporary = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = temporary;
            }
        }
    }
}
