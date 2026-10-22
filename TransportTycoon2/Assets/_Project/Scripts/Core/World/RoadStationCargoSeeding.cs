#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Seeds demo cargo into road stations adjacent to industries (MapPlay / tests).
    /// </summary>
    public static class RoadStationCargoSeeding
    {
        /// <summary>
        /// Default demo cargo id used until production chains feed stations (#48).
        /// </summary>
        public const string DemoCargoId = "wood";

        /// <summary>
        /// Default units placed at each industry-adjacent station.
        /// </summary>
        public const int DemoAmountPerStation = 20;

        /// <summary>
        /// Adds <paramref name="amount"/> of <paramref name="cargoId"/> to every station that is
        /// orthogonally adjacent to at least one industry tile. Existing cargo of that id is increased.
        /// </summary>
        public static void SeedAtIndustryAdjacentStations(
            GeneratedWorld world,
            string cargoId = DemoCargoId,
            int amount = DemoAmountPerStation)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");
            }

            int townCount = world.TownAnchors.Count;
            for (int index = 0; index < world.RoadStations.Count; index++)
            {
                RoadStation station = world.RoadStations[index];
                if (!HasOrthogonalIndustryNeighbor(world, station.Coordinate, townCount))
                {
                    continue;
                }

                station.Cargo.Add(cargoId, amount);
            }
        }

        private static bool HasOrthogonalIndustryNeighbor(GeneratedWorld world, TileCoordinate coordinate, int townCount)
        {
            var neighbors = new[]
            {
                new TileCoordinate(coordinate.X, coordinate.Y + 1),
                new TileCoordinate(coordinate.X + 1, coordinate.Y),
                new TileCoordinate(coordinate.X, coordinate.Y - 1),
                new TileCoordinate(coordinate.X - 1, coordinate.Y)
            };

            for (int index = 0; index < neighbors.Length; index++)
            {
                TileCoordinate neighbor = neighbors[index];
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                int objectId = world.Map.GetObjectId(neighbor);
                if (objectId >= townCount)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
