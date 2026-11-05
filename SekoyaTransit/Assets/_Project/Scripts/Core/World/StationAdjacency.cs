#nullable enable
using System;

namespace SekoyaTransit.Core.World
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
        /// Minimum Chebyshev distance between the anchors of two sites. Sites are blocks of up to 3x3 tiles
        /// (<see cref="SiteFootprint"/>), so a gap of at least two tiles keeps every tile orthogonally adjacent to at
        /// most one site.
        /// </summary>
        public const int MinimumSiteSeparationChebyshev = SiteFootprint.TownSize + 2;

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
            if (!TryGetAdjacentIndustryIndex(world, coordinate, out int industryIndex))
            {
                return false;
            }

            industry = world.Industries[industryIndex];
            return true;
        }

        /// <summary>
        /// Tries to resolve the index in <see cref="GeneratedWorld.Industries"/> of the industry orthogonally adjacent
        /// to <paramref name="coordinate"/>. When multiple are present (invalid map), returns the lowest index.
        /// </summary>
        public static bool TryGetAdjacentIndustryIndex(
            GeneratedWorld world,
            TileCoordinate coordinate,
            out int industryIndex)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

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

                int candidate = objectId - townCount;
                if (candidate >= 0 && candidate < world.Industries.Count && candidate < bestIndex)
                {
                    bestIndex = candidate;
                }
            }

            industryIndex = bestIndex;
            return bestIndex != int.MaxValue;
        }

        /// <summary>
        /// Tries to resolve the town orthogonally adjacent to <paramref name="station"/>.
        /// </summary>
        public static bool TryGetAdjacentTown(
            GeneratedWorld world,
            RoadStation station,
            out Town? town)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return TryGetAdjacentTown(world, station.Coordinate, out town);
        }

        /// <summary>
        /// Tries to resolve the town orthogonally adjacent to <paramref name="station"/>.
        /// </summary>
        public static bool TryGetAdjacentTown(
            GeneratedWorld world,
            RailStation station,
            out Town? town)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return TryGetAdjacentTown(world, station.Coordinate, out town);
        }

        /// <summary>
        /// Tries to resolve the town orthogonally adjacent to <paramref name="coordinate"/>.
        /// When multiple are present (invalid map), returns the lowest town index deterministically.
        /// </summary>
        public static bool TryGetAdjacentTown(
            GeneratedWorld world,
            TileCoordinate coordinate,
            out Town? town)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            town = null;
            if (!TryGetAdjacentTownIndex(world, coordinate, out int townIndex))
            {
                return false;
            }

            town = world.Towns[townIndex];
            return true;
        }

        /// <summary>
        /// Tries to resolve the index in <see cref="GeneratedWorld.Towns"/> of the town orthogonally adjacent to
        /// <paramref name="coordinate"/>. When multiple are present (invalid map), returns the lowest index.
        /// </summary>
        public static bool TryGetAdjacentTownIndex(
            GeneratedWorld world,
            TileCoordinate coordinate,
            out int townIndex)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            int townCount = world.Towns.Count;
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
                if (objectId >= 0 && objectId < townCount && objectId < bestIndex)
                {
                    bestIndex = objectId;
                }
            }

            townIndex = bestIndex;
            return bestIndex != int.MaxValue;
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

        /// <summary>
        /// Returns whether the Chebyshev distance between two site anchors is at least <paramref name="minimumDistance"/>.
        /// </summary>
        public static bool SatisfiesSeparation(TileCoordinate left, TileCoordinate right, int minimumDistance)
        {
            int dx = Math.Abs(left.X - right.X);
            int dy = Math.Abs(left.Y - right.Y);
            int chebyshev = dx > dy ? dx : dy;
            return chebyshev >= minimumDistance;
        }
    }
}
