#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Data-driven map and spacing constraints for deterministic industry placement.
    /// </summary>
    public readonly struct IndustryPlacementSettings
    {
        private readonly ReadOnlyCollection<IndustryPlacementRule>? _industryRules;
        private readonly ReadOnlyCollection<TileCoordinate>? _occupiedTileCoordinates;

        /// <summary>
        /// Initializes settings for a supported square map.
        /// </summary>
        /// <param name="mapSize">The number of tiles along each side.</param>
        /// <param name="minimumIndustryChebyshevDistance">The minimum distance between any two industry anchors.</param>
        /// <param name="industryRules">Industry definitions, counts, and producer-distance constraints.</param>
        public IndustryPlacementSettings(
            MapSize mapSize,
            int minimumIndustryChebyshevDistance,
            IReadOnlyList<IndustryPlacementRule> industryRules)
            : this(mapSize, minimumIndustryChebyshevDistance, industryRules, Array.Empty<TileCoordinate>())
        {
        }

        /// <summary>
        /// Initializes settings for a map that may already contain town or other object anchors.
        /// </summary>
        /// <param name="mapSize">The number of tiles along each side.</param>
        /// <param name="minimumIndustryChebyshevDistance">The minimum distance between any two industry anchors.</param>
        /// <param name="industryRules">Industry definitions, counts, and producer-distance constraints.</param>
        /// <param name="occupiedTileCoordinates">Existing occupied tile anchors that industries must not overlap.</param>
        public IndustryPlacementSettings(
            MapSize mapSize,
            int minimumIndustryChebyshevDistance,
            IReadOnlyList<IndustryPlacementRule> industryRules,
            IReadOnlyList<TileCoordinate> occupiedTileCoordinates)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (industryRules is null)
            {
                throw new ArgumentNullException(nameof(industryRules));
            }

            if (occupiedTileCoordinates is null)
            {
                throw new ArgumentNullException(nameof(occupiedTileCoordinates));
            }
#pragma warning restore CA1510

            var copy = new IndustryPlacementRule[industryRules.Count];
            for (int index = 0; index < industryRules.Count; index++)
            {
                copy[index] = industryRules[index] ??
                    throw new ArgumentException("Industry placement rules cannot contain null entries.", nameof(industryRules));
            }

            var occupiedCopy = new TileCoordinate[occupiedTileCoordinates.Count];
            for (int index = 0; index < occupiedTileCoordinates.Count; index++)
            {
                occupiedCopy[index] = occupiedTileCoordinates[index];
            }

            Validate(mapSize, minimumIndustryChebyshevDistance, copy, occupiedCopy);

            MapSize = mapSize;
            MinimumIndustryChebyshevDistance = minimumIndustryChebyshevDistance;
            _industryRules = Array.AsReadOnly(copy);
            _occupiedTileCoordinates = Array.AsReadOnly(occupiedCopy);
        }

        private static void Validate(
            MapSize mapSize,
            int minimumIndustryChebyshevDistance,
            IReadOnlyList<IndustryPlacementRule>? industryRules,
            IReadOnlyList<TileCoordinate>? occupiedTileCoordinates)
        {
            int sideLength = (int)mapSize;
            if (sideLength != 128 && sideLength != 256 && sideLength != 512)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mapSize),
                    mapSize,
                    "Map size must be 128, 256, or 512 tiles per side.");
            }

            if (minimumIndustryChebyshevDistance < StationAdjacency.MinimumSiteSeparationChebyshev)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumIndustryChebyshevDistance),
                    minimumIndustryChebyshevDistance,
                    "Minimum industry distance must be at least " +
                    StationAdjacency.MinimumSiteSeparationChebyshev +
                    " so stations cannot attach to two sites.");
            }

            if (industryRules is null || industryRules.Count == 0)
            {
                throw new ArgumentException("At least one industry placement rule is required.", nameof(industryRules));
            }

            if (occupiedTileCoordinates is null)
            {
                throw new ArgumentException("Occupied tile coordinates must be provided.", nameof(occupiedTileCoordinates));
            }

            var occupiedTileIds = new HashSet<int>();
            foreach (TileCoordinate coordinate in occupiedTileCoordinates)
            {
                if (coordinate.X < 0 || coordinate.X >= sideLength ||
                    coordinate.Y < 0 || coordinate.Y >= sideLength)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(occupiedTileCoordinates),
                        coordinate,
                        "An occupied tile coordinate is outside the map.");
                }

                int tileId = (coordinate.Y * sideLength) + coordinate.X;
                if (!occupiedTileIds.Add(tileId))
                {
                    throw new ArgumentException(
                        $"Occupied tile coordinate ({coordinate.X}, {coordinate.Y}) is duplicated.",
                        nameof(occupiedTileCoordinates));
                }
            }

            int bucketCount = ((sideLength - 1) / minimumIndustryChebyshevDistance) + 1;
            int maximumIndustryCount = checked(bucketCount * bucketCount);
            long requestedIndustryCount = 0;
            var industryIds = new HashSet<string>(StringComparer.Ordinal);
            var producerLevels = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            foreach (IndustryPlacementRule rule in industryRules)
            {
                if (!industryIds.Add(rule.IndustryId))
                {
                    throw new ArgumentException($"Industry ID '{rule.IndustryId}' is duplicated.", nameof(industryRules));
                }

                requestedIndustryCount += rule.InstanceCount;
                if (requestedIndustryCount > maximumIndustryCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(industryRules),
                        "Requested industry count exceeds the theoretical capacity allowed by minimum spacing.");
                }

                if (rule.Level > 1 &&
                    rule.MaximumProducerChebyshevDistance < minimumIndustryChebyshevDistance)
                {
                    throw new ArgumentException(
                        $"Industry '{rule.IndustryId}' cannot reach a producer while respecting minimum spacing.",
                        nameof(industryRules));
                }

                foreach (string cargoId in rule.OutputCargoIds)
                {
                    if (!producerLevels.TryGetValue(cargoId, out List<int>? levels))
                    {
                        levels = new List<int>();
                        producerLevels.Add(cargoId, levels);
                    }

                    levels.Add(rule.Level);
                }
            }

            foreach (IndustryPlacementRule rule in industryRules)
            {
                foreach (string inputCargoId in rule.InputCargoIds)
                {
                    if (!producerLevels.TryGetValue(inputCargoId, out List<int>? levels) ||
                        !HasLowerLevelProducer(levels, rule.Level))
                    {
                        throw new ArgumentException(
                            $"Industry '{rule.IndustryId}' consumes cargo '{inputCargoId}' without a lower-level producer.",
                            nameof(industryRules));
                    }
                }
            }

            if (requestedIndustryCount > ((long)sideLength * sideLength) - occupiedTileIds.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(industryRules),
                    "Requested industry count exceeds the number of unoccupied map tiles.");
            }
        }

        private static bool HasLowerLevelProducer(List<int> producerLevels, int industryLevel)
        {
            foreach (int producerLevel in producerLevels)
            {
                if (producerLevel < industryLevel)
                {
                    return true;
                }
            }

            return false;
        }

        internal void Validate()
        {
            Validate(MapSize, MinimumIndustryChebyshevDistance, _industryRules, _occupiedTileCoordinates);
        }

        /// <summary>
        /// Gets the configured square map size.
        /// </summary>
        public MapSize MapSize { get; }

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

        /// <summary>
        /// Gets the tile anchors already occupied by towns or other map objects.
        /// </summary>
        public IReadOnlyList<TileCoordinate> OccupiedTileCoordinates
        {
            get
            {
                if (_occupiedTileCoordinates is null)
                {
                    return Array.Empty<TileCoordinate>();
                }

                return _occupiedTileCoordinates;
            }
        }
    }
}
