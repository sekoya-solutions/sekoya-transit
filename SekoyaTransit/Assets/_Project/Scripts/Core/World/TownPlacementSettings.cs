#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Data-driven constraints for deterministic town anchor placement.
    /// </summary>
    public readonly struct TownPlacementSettings
    {
        /// <summary>
        /// Initializes town placement settings for a supported square map.
        /// </summary>
        /// <param name="mapSize">The number of tiles along each side.</param>
        /// <param name="townCount">The requested number of town anchor tiles.</param>
        /// <param name="minimumChebyshevDistance">The minimum Chebyshev distance between anchors, in tiles.</param>
        public TownPlacementSettings(MapSize mapSize, int townCount, int minimumChebyshevDistance)
        {
            Validate(mapSize, townCount, minimumChebyshevDistance);

            MapSize = mapSize;
            TownCount = townCount;
            MinimumChebyshevDistance = minimumChebyshevDistance;
        }

        private static void Validate(MapSize mapSize, int townCount, int minimumChebyshevDistance)
        {
            int sideLength = (int)mapSize;
            if (sideLength != 128 && sideLength != 256 && sideLength != 512)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mapSize),
                    mapSize,
                    "Map size must be 128, 256, or 512 tiles per side.");
            }

            if (townCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(townCount), townCount, "Town count cannot be negative.");
            }

            if (minimumChebyshevDistance < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumChebyshevDistance),
                    minimumChebyshevDistance,
                    "Minimum town distance must be at least one tile.");
            }

            int bucketCount = ((sideLength - 1) / minimumChebyshevDistance) + 1;
            int maximumTownCount = checked(bucketCount * bucketCount);
            if (townCount > maximumTownCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(townCount),
                    townCount,
                    "Town count exceeds the maximum allowed by the minimum-distance setting.");
            }
        }

        internal void Validate()
        {
            Validate(MapSize, TownCount, MinimumChebyshevDistance);
        }

        /// <summary>
        /// Gets the configured square map size.
        /// </summary>
        public MapSize MapSize { get; }

        /// <summary>
        /// Gets the requested number of town anchors.
        /// </summary>
        public int TownCount { get; }

        /// <summary>
        /// Gets the minimum Chebyshev distance between town anchors, in tiles.
        /// </summary>
        public int MinimumChebyshevDistance { get; }
    }
}
