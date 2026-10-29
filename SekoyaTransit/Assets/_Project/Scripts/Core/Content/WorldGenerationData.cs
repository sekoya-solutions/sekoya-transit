#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven world generation settings for every supported map size (Issue #67).
    /// </summary>
    public sealed class WorldGenerationData
    {
        private readonly Dictionary<MapSize, WorldGenerationSizeDefinition> _sizes;

        /// <summary>
        /// Initializes the data from exactly one definition per map size.
        /// </summary>
        public WorldGenerationData(IReadOnlyList<WorldGenerationSizeDefinition> sizes)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (sizes is null)
            {
                throw new ArgumentNullException(nameof(sizes));
            }
#pragma warning restore CA1510

            _sizes = new Dictionary<MapSize, WorldGenerationSizeDefinition>();
            for (int index = 0; index < sizes.Count; index++)
            {
                WorldGenerationSizeDefinition definition = sizes[index] ??
                    throw new ArgumentException("Size definitions cannot contain null.", nameof(sizes));
                if (!_sizes.TryAdd(definition.MapSize, definition))
                {
                    throw new ArgumentException(
                        $"Map size {definition.MapSize} is defined more than once.",
                        nameof(sizes));
                }
            }

            foreach (MapSize size in MapSizes.All())
            {
                if (!_sizes.ContainsKey(size))
                {
                    throw new ArgumentException($"Map size {size} has no world generation entry.", nameof(sizes));
                }
            }
        }

        /// <summary>
        /// Creates the values used when no <c>worldgen.json</c> is supplied (same as the shipped content).
        /// </summary>
        public static WorldGenerationData CreatePlaceholder()
        {
            return new WorldGenerationData(
                new[]
                {
                    CreatePlaceholderSize(MapSize.Small, 15, 20, 3, 2, 1, 60),
                    CreatePlaceholderSize(MapSize.Medium, 20, 32, 6, 4, 2, 80),
                    CreatePlaceholderSize(MapSize.Large, 30, 32, 12, 8, 4, 100),
                });
        }

        // Every V1 chain (Issue #193): each level 1, level 2 and level 3 industry with its own count.
        private static WorldGenerationSizeDefinition CreatePlaceholderSize(
            MapSize mapSize,
            int townCount,
            int townSpacing,
            int level1Count,
            int level2Count,
            int level3Count,
            int producerDistance)
        {
            return new WorldGenerationSizeDefinition(
                mapSize,
                townCount,
                townSpacing,
                4,
                new[]
                {
                    new WorldGenerationIndustryEntry("forest", level1Count, 0),
                    new WorldGenerationIndustryEntry("wheat_farm", level1Count, 0),
                    new WorldGenerationIndustryEntry("iron_mine", level1Count, 0),
                    new WorldGenerationIndustryEntry("livestock_ranch", level1Count, 0),
                    new WorldGenerationIndustryEntry("sheep_farm", level1Count, 0),
                    new WorldGenerationIndustryEntry("sawmill", level2Count, producerDistance),
                    new WorldGenerationIndustryEntry("flour_mill", level2Count, producerDistance),
                    new WorldGenerationIndustryEntry("iron_foundry", level2Count, producerDistance),
                    new WorldGenerationIndustryEntry("butcher", level2Count, producerDistance),
                    new WorldGenerationIndustryEntry("textile_mill", level2Count, producerDistance),
                    new WorldGenerationIndustryEntry("furniture_factory", level3Count, producerDistance),
                    new WorldGenerationIndustryEntry("bakery", level3Count, producerDistance),
                    new WorldGenerationIndustryEntry("machine_shop", level3Count, producerDistance),
                    new WorldGenerationIndustryEntry("cannery", level3Count, producerDistance),
                    new WorldGenerationIndustryEntry("clothing_workshop", level3Count, producerDistance),
                });
        }
        /// <summary>
        /// Gets the definition for a map size.
        /// </summary>
        public WorldGenerationSizeDefinition Get(MapSize mapSize)
        {
            if (!_sizes.TryGetValue(mapSize, out WorldGenerationSizeDefinition? definition))
            {
                throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size is not supported.");
            }

            return definition;
        }

        /// <summary>
        /// Resolves one map size against the industry definitions into generator settings.
        /// </summary>
        /// <exception cref="ArgumentException">An entry names an industry missing from <paramref name="industries"/>, or the values are invalid.</exception>
        public WorldGenerationSettings CreateSettings(MapSize mapSize, IReadOnlyList<IndustryDefinition> industries)
        {
            WorldGenerationSizeDefinition size = Get(mapSize);
            var rules = new IndustryPlacementRule[size.Industries.Count];
            for (int index = 0; index < rules.Length; index++)
            {
                WorldGenerationIndustryEntry entry = size.Industries[index];
                IndustryDefinition? industry = null;
                for (int search = 0; search < industries.Count; search++)
                {
                    if (string.Equals(industries[search].Id, entry.IndustryId, StringComparison.Ordinal))
                    {
                        industry = industries[search];
                        break;
                    }
                }

                if (industry == null)
                {
                    throw new ArgumentException(
                        $"World generation for {mapSize} references unknown industry ID '{entry.IndustryId}'.",
                        nameof(industries));
                }

                rules[index] = new IndustryPlacementRule(
                    industry.Id,
                    industry.Level,
                    CargoIds(industry.Inputs),
                    CargoIds(industry.Outputs),
                    entry.Count,
                    entry.MaximumProducerDistance);
            }

            return new WorldGenerationSettings(
                new TownPlacementSettings(mapSize, size.TownCount, size.TownSpacing),
                size.IndustrySpacing,
                rules);
        }

        private static string[] CargoIds(IReadOnlyList<CargoAmountDefinition> amounts)
        {
            var ids = new string[amounts.Count];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = amounts[index].CargoId;
            }

            return ids;
        }
    }
}
