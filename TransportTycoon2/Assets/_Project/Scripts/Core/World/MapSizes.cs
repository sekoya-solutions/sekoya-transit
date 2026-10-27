#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Helpers for the closed set of supported <see cref="MapSize"/> values.
    /// </summary>
    public static class MapSizes
    {
        private static readonly MapSize[] _all = { MapSize.Small, MapSize.Medium, MapSize.Large };

        /// <summary>
        /// Gets a copy of every supported map size, smallest first.
        /// </summary>
        public static MapSize[] All()
        {
            return (MapSize[])_all.Clone();
        }

        /// <summary>
        /// Returns whether the value is one of the supported map sizes.
        /// </summary>
        public static bool IsSupported(MapSize mapSize)
        {
            return mapSize == MapSize.Small || mapSize == MapSize.Medium || mapSize == MapSize.Large;
        }
    }
}
