#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Resolves the unique orthogonally adjacent industry or town for a road station.
    /// World generation enforces Chebyshev site separation so a station cannot touch two sites.
    /// </summary>
    public static class StationAdjacency
    {
        private static readonly TileCoordinate[] _neighborOffsets =
        {
            new TileCoordinate(0, 1),
            new TileCoordinate(1, 0),
            new TileCoordinate(0, -1),
            new TileCoordinate(-1, 0)
        };

        /// <summary>
        /// Minimum Chebyshev distance between any industry tile and any other industry or town tile
        /// so no single tile is orthogonally adjacent to two sites.
        /// </summary>
        public const int MinimumSiteSeparationChebyshev = 3;

        /// <summary>
        /// Tries to resolve the industry orthogonally adjacent to <paramref name="station"/>.
        /// When multiple are present (invalid map), returns the lowest industry index deterministically.
        /// </summary>
        public static bool TryGetAdjacentIndustry(
            GeneratedWorld world,
            RoadStation station,
            out PlacedIndustry? industry)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return TryGetAdjacentIndustry(world, station.Coordinate, out industry);
        }

        /// <summary>
        /// Tries to resolve the industry orthogonally adjacent to <paramref name="station"/>.
        /// When multiple are present (invalid map), returns the lowest industry index deterministically.
        /// </summary>
        public static bool TryGetAdjacentIndustry(
            GeneratedWorld world,
            RailStation station,
            out PlacedIndustry? industry)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return TryGetAdjacentIndustry(world, station.Coordinate, out industry);
        }

        /// <summary>
        /// Tries to resolve the industry orthogonally adjacent to <paramref name="coordinate"/>.
        /// When multiple are present (invalid map), returns the lowest industry index deterministically.
        /// </summary>
        public static bool TryGetAdjacentIndustry(
            GeneratedWorld world,
            TileCoordinate coordinate,
            out PlacedIndustry? industry)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            industry = null;
            int townCount = world.TownAnchors.Count;
            int bestIndex = int.MaxValue;
            for (int index = 0; index < _neighborOffsets.Length; index++)
            {
                TileCoordinate offset = _neighborOffsets[index];
                var neighbor = new TileCoordinate(
                    coordinate.X + offset.X,
                    coordinate.Y + offset.Y);
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                int objectId = world.Map.GetObjectId(neighbor);
                if (objectId < townCount)
                {
                    continue;
                }

                int industryIndex = objectId - townCount;
                if (industryIndex < 0 || industryIndex >= world.Industries.Count)
                {
                    continue;
                }

                if (industryIndex < bestIndex)
                {
                    bestIndex = industryIndex;
                    industry = world.Industries[industryIndex];
                }
            }

            return industry != null;
        }

        /// <summary>
        /// Returns whether Chebyshev distance between two tiles is at least
        /// <see cref="MinimumSiteSeparationChebyshev"/>.
        /// </summary>
        public static bool SatisfiesMinimumSiteSeparation(TileCoordinate left, TileCoordinate right)
        {
            int dx = Math.Abs(left.X - right.X);
            int dy = Math.Abs(left.Y - right.Y);
            int chebyshev = dx > dy ? dx : dy;
            return chebyshev >= MinimumSiteSeparationChebyshev;
        }
    }
}
