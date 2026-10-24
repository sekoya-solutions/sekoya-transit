#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Builds each vehicle's allowed-load cargo set from stations on its route:
    /// town needs and industry recipe inputs at orthogonally adjacent sites.
    /// </summary>
    public static class VehicleRouteCargoFilter
    {
        /// <summary>
        /// Rebuilds allowed-load cargo ids for every road and rail vehicle.
        /// </summary>
        public static void RefreshAll(GeneratedWorld world, ContentCatalog catalog)
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

            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RefreshRoad(world, world.RoadVehicles[index], catalog);
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RefreshRail(world, world.RailVehicles[index], catalog);
            }
        }

        /// <summary>
        /// Rebuilds <see cref="RoadVehicle.AllowedLoadCargoIds"/> from the vehicle's current orders.
        /// </summary>
        public static void RefreshRoad(GeneratedWorld world, RoadVehicle vehicle, ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            var allowed = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < vehicle.Orders.Count; index++)
            {
                int stationId = vehicle.Orders[index].StationId;
                if (!world.TryGetRoadStation(stationId, out RoadStation? station) || station == null)
                {
                    continue;
                }

                AddAcceptedAtCoordinate(world, station.Coordinate, catalog, allowed);
            }

            AddPassengerCargoIfRouteHasDistinctTowns(world, vehicle, catalog, allowed);
            vehicle.SetAllowedLoadCargoIds(ToSortedArray(allowed));
        }

        /// <summary>
        /// Rebuilds <see cref="RailVehicle.AllowedLoadCargoIds"/> from the vehicle's current orders.
        /// </summary>
        public static void RefreshRail(GeneratedWorld world, RailVehicle vehicle, ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            var allowed = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < vehicle.Orders.Count; index++)
            {
                int stationId = vehicle.Orders[index].StationId;
                if (!world.TryGetRailStation(stationId, out RailStation? station) || station == null)
                {
                    continue;
                }

                AddAcceptedAtCoordinate(world, station.Coordinate, catalog, allowed);
            }

            AddPassengerCargoIfRailRouteHasDistinctTowns(world, vehicle, catalog, allowed);
            vehicle.SetAllowedLoadCargoIds(ToSortedArray(allowed));
        }

        /// <summary>
        /// Ensures a road vehicle's allowed-load list is current (rebuilds when marked dirty).
        /// </summary>
        public static void EnsureRoad(GeneratedWorld world, RoadVehicle vehicle, ContentCatalog catalog)
        {
            if (vehicle.AllowedLoadCargoDirty)
            {
                RefreshRoad(world, vehicle, catalog);
            }
        }

        /// <summary>
        /// Ensures a rail vehicle's allowed-load list is current (rebuilds when marked dirty).
        /// </summary>
        public static void EnsureRail(GeneratedWorld world, RailVehicle vehicle, ContentCatalog catalog)
        {
            if (vehicle.AllowedLoadCargoDirty)
            {
                RefreshRail(world, vehicle, catalog);
            }
        }

        private static void AddAcceptedAtCoordinate(
            GeneratedWorld world,
            TileCoordinate coordinate,
            ContentCatalog catalog,
            HashSet<string> allowed)
        {
            if (StationAdjacency.TryGetAdjacentTown(world, coordinate, out Town? town) && town != null)
            {
                for (int index = 0; index < town.Needs.Count; index++)
                {
                    allowed.Add(town.Needs[index].CargoId);
                }
            }

            if (StationAdjacency.TryGetAdjacentIndustry(world, coordinate, out PlacedIndustry? industry) &&
                industry != null &&
                catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) &&
                definition != null)
            {
                for (int index = 0; index < definition.Inputs.Count; index++)
                {
                    allowed.Add(definition.Inputs[index].CargoId);
                }
            }
        }

        private static void AddPassengerCargoIfRouteHasDistinctTowns(
            GeneratedWorld world,
            RoadVehicle vehicle,
            ContentCatalog catalog,
            HashSet<string> allowed)
        {
            if (PassengerCargo.RouteHasDistinctTownStops(world, vehicle.OrderStationIds))
            {
                PassengerCargo.AddAllPassengerCargoIds(catalog, allowed);
            }
        }

        private static void AddPassengerCargoIfRailRouteHasDistinctTowns(
            GeneratedWorld world,
            RailVehicle vehicle,
            ContentCatalog catalog,
            HashSet<string> allowed)
        {
            if (PassengerCargo.RailRouteHasDistinctTownStops(world, vehicle.OrderStationIds))
            {
                PassengerCargo.AddAllPassengerCargoIds(catalog, allowed);
            }
        }

        private static string[] ToSortedArray(HashSet<string> allowed)
        {
            var list = new List<string>(allowed.Count);
            foreach (string cargoId in allowed)
            {
                list.Add(cargoId);
            }

            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }
    }
}
