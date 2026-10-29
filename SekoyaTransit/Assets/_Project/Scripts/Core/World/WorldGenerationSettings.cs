#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Data-driven settings for deterministic V1 world generation.
    /// </summary>
    public readonly struct WorldGenerationSettings
    {
        private readonly ReadOnlyCollection<IndustryPlacementRule>? _industryRules;

        /// <summary>
        /// Initializes settings that combine town and industry placement.
        /// </summary>
        /// <param name="townPlacementSettings">Map size, town count, and town spacing.</param>
        /// <param name="minimumIndustryChebyshevDistance">Minimum spacing between industry anchors.</param>
        /// <param name="industryRules">Industry recipes, counts, and producer-distance limits.</param>
        public WorldGenerationSettings(
            TownPlacementSettings townPlacementSettings,
            int minimumIndustryChebyshevDistance,
            IReadOnlyList<IndustryPlacementRule> industryRules)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (industryRules is null)
            {
                throw new ArgumentNullException(nameof(industryRules));
            }
#pragma warning restore CA1510

            var copy = new IndustryPlacementRule[industryRules.Count];
            for (int index = 0; index < industryRules.Count; index++)
            {
                copy[index] = industryRules[index] ??
                    throw new ArgumentException("Industry placement rules cannot contain null entries.", nameof(industryRules));
            }

            townPlacementSettings.Validate();
            _ = new IndustryPlacementSettings(
                townPlacementSettings.MapSize,
                minimumIndustryChebyshevDistance,
                copy);

            TownPlacementSettings = townPlacementSettings;
            MinimumIndustryChebyshevDistance = minimumIndustryChebyshevDistance;
            _industryRules = Array.AsReadOnly(copy);
        }

        internal void Validate()
        {
            TownPlacementSettings.Validate();
            _ = new IndustryPlacementSettings(
                TownPlacementSettings.MapSize,
                MinimumIndustryChebyshevDistance,
                IndustryRules);
        }

        /// <summary>
        /// Gets the town placement configuration and supported map size.
        /// </summary>
        public TownPlacementSettings TownPlacementSettings { get; }

        /// <summary>
        /// Gets the minimum Chebyshev distance between industry anchors, in tiles.
        /// </summary>
        public int MinimumIndustryChebyshevDistance { get; }

        /// <summary>
        /// Gets the immutable industry placement rules.
        /// </summary>
        public IReadOnlyList<IndustryPlacementRule> IndustryRules
        {
            get
            {
                if (_industryRules is null)
                {
                    return Array.Empty<IndustryPlacementRule>();
                }

                return _industryRules;
            }
        }
    }
}
