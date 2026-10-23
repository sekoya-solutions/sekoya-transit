#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Determines whether unloading a cargo type at a station earns delivery income.
    /// </summary>
    public static class DeliveryAcceptance
    {
        private static readonly TileCoordinate[] NeighborOffsets =
        {
            new TileCoordinate(0, 1),
            new TileCoordinate(1, 0),
            new TileCoordinate(0, -1),
            new TileCoordinate(-1, 0)
        };

        /// <summary>
        /// Returns whether <paramref name="station"/> is adjacent to a town or to an industry
        /// that lists <paramref name="cargoId"/> as a recipe input.
        /// </summary>
        public static bool Accepts(
            GeneratedWorld world,
            RoadStation station,
            string cargoId,
            ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            int townCount = world.TownAnchors.Count;
            for (int index = 0; index < NeighborOffsets.Length; index++)
            {
                TileCoordinate offset = NeighborOffsets[index];
                var neighbor = new TileCoordinate(
                    station.Coordinate.X + offset.X,
                    station.Coordinate.Y + offset.Y);
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                int objectId = world.Map.GetObjectId(neighbor);
                if (objectId < 0)
                {
                    continue;
                }

                if (objectId < townCount)
                {
                    return true;
                }

                int industryIndex = objectId - townCount;
                if (industryIndex < 0 || industryIndex >= world.Industries.Count)
                {
                    continue;
                }

                PlacedIndustry industry = world.Industries[industryIndex];
                if (!catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) ||
                    definition == null)
                {
                    continue;
                }

                for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
                {
                    if (string.Equals(
                            definition.Inputs[inputIndex].CargoId,
                            cargoId,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
