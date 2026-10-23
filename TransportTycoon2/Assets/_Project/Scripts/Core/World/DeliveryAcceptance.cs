#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Determines whether unloading a cargo type at a station earns delivery income,
    /// and whether a station is town-adjacent (freight is consumed, not stocked).
    /// </summary>
    public static class DeliveryAcceptance
    {
        private static readonly TileCoordinate[] _neighborOffsets =
        {
            new TileCoordinate(0, 1),
            new TileCoordinate(1, 0),
            new TileCoordinate(0, -1),
            new TileCoordinate(-1, 0)
        };

        /// <summary>
        /// Returns whether <paramref name="station"/> is orthogonally adjacent to a town tile.
        /// </summary>
        public static bool IsAdjacentToTown(GeneratedWorld world, RoadStation station)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return IsAdjacentToTown(world, station.Coordinate);
        }

        /// <summary>
        /// Returns whether <paramref name="station"/> is orthogonally adjacent to a town tile.
        /// </summary>
        public static bool IsAdjacentToTown(GeneratedWorld world, RailStation station)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return IsAdjacentToTown(world, station.Coordinate);
        }

        /// <summary>
        /// Returns whether freight unloaded at a town-adjacent station should be consumed
        /// immediately rather than stocked. Passengers/mail (when implemented) remain stocked.
        /// </summary>
        public static bool ConsumesFreightOnUnload(
            GeneratedWorld world,
            RoadStation station,
            string cargoId,
            ContentCatalog? catalog)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return ConsumesFreightOnUnload(world, station.Coordinate, cargoId, catalog);
        }

        /// <summary>
        /// Returns whether freight unloaded at a town-adjacent station should be consumed
        /// immediately rather than stocked. Passengers/mail (when implemented) remain stocked.
        /// </summary>
        public static bool ConsumesFreightOnUnload(
            GeneratedWorld world,
            RailStation station,
            string cargoId,
            ContentCatalog? catalog)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return ConsumesFreightOnUnload(world, station.Coordinate, cargoId, catalog);
        }

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
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return Accepts(world, station.Coordinate, cargoId, catalog);
        }

        /// <summary>
        /// Returns whether <paramref name="station"/> is adjacent to a town or to an industry
        /// that lists <paramref name="cargoId"/> as a recipe input.
        /// </summary>
        public static bool Accepts(
            GeneratedWorld world,
            RailStation station,
            string cargoId,
            ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            return Accepts(world, station.Coordinate, cargoId, catalog);
        }

        private static bool IsAdjacentToTown(GeneratedWorld world, TileCoordinate coordinate)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            int townCount = world.TownAnchors.Count;
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
                if (objectId >= 0 && objectId < townCount)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ConsumesFreightOnUnload(
            GeneratedWorld world,
            TileCoordinate coordinate,
            string cargoId,
            ContentCatalog? catalog)
        {
            if (!IsAdjacentToTown(world, coordinate))
            {
                return false;
            }

            if (catalog == null)
            {
                // Without catalog, treat all deliveries as freight consumption at towns.
                return true;
            }

            if (!catalog.TryGetCargo(cargoId, out CargoDefinition? definition) || definition == null)
            {
                return true;
            }

            return definition.Class == CargoClass.Cargo;
        }

        private static bool Accepts(
            GeneratedWorld world,
            TileCoordinate coordinate,
            string cargoId,
            ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
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

            if (IsAdjacentToTown(world, coordinate))
            {
                return true;
            }

            int townCount = world.TownAnchors.Count;
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
