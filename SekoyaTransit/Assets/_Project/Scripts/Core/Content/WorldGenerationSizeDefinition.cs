#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// World generation counts and spacing for one map size (Issue #67).
    /// </summary>
    public sealed class WorldGenerationSizeDefinition
    {
        private readonly ReadOnlyCollection<WorldGenerationIndustryEntry> _industries;

        /// <summary>
        /// Initializes the generation data of one map size.
        /// </summary>
        public WorldGenerationSizeDefinition(
            MapSize mapSize,
            int townCount,
            int townSpacing,
            int industrySpacing,
            IReadOnlyList<WorldGenerationIndustryEntry> industries)
        {
            if (!MapSizes.IsSupported(mapSize))
            {
                throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size is not supported.");
            }

            if (townCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(townCount), townCount, "Town count cannot be negative.");
            }

            if (townSpacing < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(townSpacing),
                    townSpacing,
                    "Town spacing must be at least 1 tile.");
            }

            if (industrySpacing < StationAdjacency.MinimumSiteSeparationChebyshev)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(industrySpacing),
                    industrySpacing,
                    "Industry spacing must be at least " + StationAdjacency.MinimumSiteSeparationChebyshev + " tiles.");
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (industries is null)
            {
                throw new ArgumentNullException(nameof(industries));
            }
#pragma warning restore CA1510

            if (industries.Count == 0)
            {
                throw new ArgumentException("At least one industry entry is required.", nameof(industries));
            }

            var copy = new WorldGenerationIndustryEntry[industries.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < industries.Count; index++)
            {
                WorldGenerationIndustryEntry entry = industries[index] ??
                    throw new ArgumentException("Industry entries cannot contain null.", nameof(industries));
                if (!ids.Add(entry.IndustryId))
                {
                    throw new ArgumentException(
                        $"Industry ID '{entry.IndustryId}' is duplicated.",
                        nameof(industries));
                }

                copy[index] = entry;
            }

            MapSize = mapSize;
            TownCount = townCount;
            TownSpacing = townSpacing;
            IndustrySpacing = industrySpacing;
            _industries = Array.AsReadOnly(copy);
        }

        /// <summary>
        /// Gets the map size this entry applies to.
        /// </summary>
        public MapSize MapSize { get; }

        /// <summary>
        /// Gets the number of towns to place.
        /// </summary>
        public int TownCount { get; }

        /// <summary>
        /// Gets the minimum Chebyshev distance between town anchors, in tiles.
        /// </summary>
        public int TownSpacing { get; }

        /// <summary>
        /// Gets the minimum Chebyshev distance between industry anchors, in tiles.
        /// </summary>
        public int IndustrySpacing { get; }

        /// <summary>
        /// Gets the industry placement entries in placement order.
        /// </summary>
        public IReadOnlyList<WorldGenerationIndustryEntry> Industries
        {
            get
            {
                return _industries;
            }
        }
    }
}
