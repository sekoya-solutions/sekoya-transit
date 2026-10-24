#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Helpers for passenger-class cargo (passengers and mail) on vehicles and routes.
    /// </summary>
    public static class PassengerCargo
    {
        /// <summary>
        /// Sentinel for <see cref="RoadVehicle.PassengerCargoOriginTownId"/> when unset.
        /// </summary>
        public const int NoOriginTownId = -1;

        /// <summary>
        /// Returns whether <paramref name="cargoId"/> is passenger-class in <paramref name="catalog"/>.
        /// </summary>
        public static bool IsPassengerClass(ContentCatalog catalog, string cargoId)
        {
#pragma warning disable CA1510
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            if (string.IsNullOrWhiteSpace(cargoId))
            {
                return false;
            }

            return catalog.TryGetCargo(cargoId, out CargoDefinition? definition) &&
                   definition != null &&
                   definition.Class == CargoClass.Passengers;
        }

        /// <summary>
        /// Counts passenger-class units in <paramref name="inventory"/>.
        /// </summary>
        public static int CountPassengerClass(CargoInventory inventory, ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            int total = 0;
            for (int index = 0; index < inventory.Stacks.Count; index++)
            {
                CargoAmount stack = inventory.Stacks[index];
                if (IsPassengerClass(catalog, stack.CargoId))
                {
                    total = checked(total + stack.Amount);
                }
            }

            return total;
        }

        /// <summary>
        /// Returns whether the road route visits at least two distinct towns.
        /// </summary>
        public static bool RouteHasDistinctTownStops(
            GeneratedWorld world,
            IReadOnlyList<int> roadStationIds)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (roadStationIds == null)
            {
                throw new ArgumentNullException(nameof(roadStationIds));
            }
#pragma warning restore CA1510

            var townIds = new HashSet<int>();
            for (int index = 0; index < roadStationIds.Count; index++)
            {
                if (!world.TryGetRoadStation(roadStationIds[index], out RoadStation? station) ||
                    station == null)
                {
                    continue;
                }

                if (StationAdjacency.TryGetAdjacentTown(world, station, out Town? town) && town != null)
                {
                    townIds.Add(town.Id);
                    if (townIds.Count >= 2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the rail route visits at least two distinct towns.
        /// </summary>
        public static bool RailRouteHasDistinctTownStops(
            GeneratedWorld world,
            IReadOnlyList<int> railStationIds)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (railStationIds == null)
            {
                throw new ArgumentNullException(nameof(railStationIds));
            }
#pragma warning restore CA1510

            var townIds = new HashSet<int>();
            for (int index = 0; index < railStationIds.Count; index++)
            {
                if (!world.TryGetRailStation(railStationIds[index], out RailStation? station) ||
                    station == null)
                {
                    continue;
                }

                if (StationAdjacency.TryGetAdjacentTown(world, station, out Town? town) && town != null)
                {
                    townIds.Add(town.Id);
                    if (townIds.Count >= 2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Adds every passenger-class cargo id from <paramref name="catalog"/> to <paramref name="allowed"/>.
        /// </summary>
        public static void AddAllPassengerCargoIds(ContentCatalog catalog, HashSet<string> allowed)
        {
#pragma warning disable CA1510
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (allowed == null)
            {
                throw new ArgumentNullException(nameof(allowed));
            }
#pragma warning restore CA1510

            for (int index = 0; index < catalog.Cargo.Count; index++)
            {
                CargoDefinition cargo = catalog.Cargo[index];
                if (cargo.Class == CargoClass.Passengers)
                {
                    allowed.Add(cargo.Id);
                }
            }
        }
    }
}
