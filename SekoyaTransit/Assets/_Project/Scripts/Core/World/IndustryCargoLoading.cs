#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Shared once-per-tick industry load distribution across road and rail vehicles
    /// that are waiting to load at stops adjacent to the same industry (Issue #54 / #132).
    /// </summary>
    public static class IndustryCargoLoading
    {
        /// <summary>
        /// Distributes one cargo unit from each industry to the next loading vehicle that can take it (round-robin
        /// across both road and rail loaders, ordered by mode then id).
        /// Only loads cargo ids accepted somewhere on the vehicle's route when a catalog is provided.
        /// </summary>
        public static void Distribute(
            GeneratedWorld world,
            ContentCatalog? catalog = null,
            GameDate? calendarDate = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            // One buffer per call, created only when a vehicle is loading, so idle ticks allocate nothing (Issue #70).
            // One pass over the vehicles, grouped by site afterwards: scanning every vehicle per site was 90 % of a
            // long game's CPU time (Issue #308).
            List<LoaderSlot>? loaders = null;
            CollectLoadingVehicles(world, catalog, ref loaders);
            if (loaders == null)
            {
                return;
            }

            // Ascending site, then mode, then id: the same order as visiting each site in turn.
            loaders.Sort(LoaderComparer.Instance);
            int first = 0;
            while (first < loaders.Count)
            {
                int industryIndex = loaders[first].SiteIndex;
                int end = first + 1;
                while (end < loaders.Count && loaders[end].SiteIndex == industryIndex)
                {
                    end++;
                }

                PlacedIndustry industry = world.Industries[industryIndex];
                if (calendarDate.HasValue)
                {
                    industry.EnsureMonthStats(calendarDate.Value);
                }

                if (!industry.Cargo.IsEmpty)
                {
                    LoadOneUnitInTurn(industry, loaders, first, end - first);
                }

                first = end;
            }
        }

        /// <summary>
        /// Gives one unit to the first loader, in turn from the industry's cursor, that can take one, and moves the
        /// cursor just past it. A loader that cannot take any unit (no other stop on its route accepts the cargo) is
        /// skipped instead of holding the turn, which froze every other loader at the site (Issue #322). The cursor
        /// moves only when a unit moves, so ticks at an empty industry do not scramble fairness among loaders.
        /// </summary>
        private static void LoadOneUnitInTurn(PlacedIndustry industry, List<LoaderSlot> loaders, int first, int count)
        {
            int start = industry.LoadRoundRobinIndex % count;
            for (int offset = 0; offset < count; offset++)
            {
                int turn = (start + offset) % count;
                if (TryLoadOneUnit(industry, loaders[first + turn]))
                {
                    industry.SetLoadRoundRobinIndex(turn + 1);
                    return;
                }
            }
        }

        private static void CollectLoadingVehicles(
            GeneratedWorld world,
            ContentCatalog? catalog,
            ref List<LoaderSlot>? loaders)
        {
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                if (!TryGetRoadLoadingIndustry(world, vehicle, out RoadVehicleOrder order, out int industryIndex))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRoad(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRoad(vehicle, industryIndex, order.StationId, world, catalog));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                if (!TryGetRailLoadingIndustry(world, vehicle, out RailVehicleOrder order, out int industryIndex))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRail(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRail(vehicle, industryIndex, order.StationId, world, catalog));
            }
        }

        private static bool TryGetRoadLoadingIndustry(
            GeneratedWorld world,
            RoadVehicle vehicle,
            out RoadVehicleOrder order,
            out int industryIndex)
        {
            order = default;
            industryIndex = -1;
            return vehicle.IsServicingStop &&
                   vehicle.IsUnloadComplete &&
                   vehicle.FreeCargoCapacity > 0 &&
                   vehicle.TryGetCurrentOrder(out order) &&
                   world.TryGetRoadStation(order.StationId, out RoadStation? station) &&
                   station != null &&
                   StationAdjacency.TryGetAdjacentIndustryIndex(world, station.Coordinate, out industryIndex);
        }

        private static bool TryGetRailLoadingIndustry(
            GeneratedWorld world,
            RailVehicle vehicle,
            out RailVehicleOrder order,
            out int industryIndex)
        {
            order = default;
            industryIndex = -1;
            return vehicle.IsServicingStop &&
                   vehicle.IsUnloadComplete &&
                   vehicle.FreeCargoCapacity > 0 &&
                   vehicle.TryGetCurrentOrder(out order) &&
                   world.TryGetRailStation(order.StationId, out RailStation? station) &&
                   station != null &&
                   StationAdjacency.TryGetAdjacentIndustryIndex(world, station.Coordinate, out industryIndex);
        }

        private static bool TryLoadOneUnit(PlacedIndustry industry, LoaderSlot loader)
        {
            CargoInventory cargo = loader.Cargo;
            CargoInventory unloaded = loader.CargoUnloadedThisVisit;
            int freeCapacity = loader.FreeCargoCapacity;
            int stackIndex = FindLoadableStackIndex(industry.Cargo, unloaded, loader);
            if (stackIndex < 0 || freeCapacity <= 0)
            {
                return false;
            }

            CargoAmount stack = industry.Cargo.Stacks[stackIndex];
            int excluded = unloaded.GetAmount(stack.CargoId);
            int loadable = stack.Amount - excluded;
            if (loadable <= 0)
            {
                return false;
            }

            int removed = industry.Cargo.TryRemove(stack.CargoId, 1);
            if (removed <= 0)
            {
                return false;
            }

            loader.NoteLoadStart();
            cargo.Add(stack.CargoId, removed);
            industry.CreditMonthTransported(removed);
            return true;
        }

        private static int FindLoadableStackIndex(
            CargoInventory industryCargo,
            CargoInventory unloadedThisVisit,
            LoaderSlot loader)
        {
            for (int index = 0; index < industryCargo.Stacks.Count; index++)
            {
                CargoAmount stack = industryCargo.Stacks[index];

                // The cheap stock check runs before the route scan, which now runs for every loader skipped in turn.
                int excluded = unloadedThisVisit.GetAmount(stack.CargoId);
                if (stack.Amount > excluded && loader.AllowsLoadCargo(stack.CargoId))
                {
                    return index;
                }
            }

            return -1;
        }

        // A cached comparer: sorting with a method group would allocate a delegate on every call (Issue #70).
        private sealed class LoaderComparer : IComparer<LoaderSlot>
        {
            public static readonly LoaderComparer Instance = new LoaderComparer();

            public int Compare(LoaderSlot left, LoaderSlot right)
            {
                int siteCompare = left.SiteIndex.CompareTo(right.SiteIndex);
                if (siteCompare != 0)
                {
                    return siteCompare;
                }

                int modeCompare = left.Mode.CompareTo(right.Mode);
                if (modeCompare != 0)
                {
                    return modeCompare;
                }

                return left.Id.CompareTo(right.Id);
            }
        }

        private readonly struct LoaderSlot
        {
            private readonly RoadVehicle? _road;
            private readonly RailVehicle? _rail;
            private readonly GeneratedWorld? _world;
            private readonly ContentCatalog? _catalog;
            private readonly int _currentStationId;

            private LoaderSlot(
                byte mode,
                int id,
                int siteIndex,
                RoadVehicle? road,
                RailVehicle? rail,
                int currentStationId,
                GeneratedWorld? world,
                ContentCatalog? catalog)
            {
                Mode = mode;
                Id = id;
                SiteIndex = siteIndex;
                _road = road;
                _rail = rail;
                _currentStationId = currentStationId;
                _world = world;
                _catalog = catalog;
            }

            public byte Mode { get; }

            public int Id { get; }

            public int SiteIndex { get; }

            public CargoInventory Cargo
            {
                get
                {
                    return _road != null ? _road.Cargo : _rail!.Cargo;
                }
            }

            public CargoInventory CargoUnloadedThisVisit
            {
                get
                {
                    return _road != null ? _road.CargoUnloadedThisVisit : _rail!.CargoUnloadedThisVisit;
                }
            }

            public int FreeCargoCapacity
            {
                get
                {
                    return _road != null ? _road.FreeCargoCapacity : _rail!.FreeCargoCapacity;
                }
            }

            public bool AllowsLoadCargo(string cargoId)
            {
                // Catalog-less paths (legacy tests): allow any cargo.
                if (_catalog == null || _world == null)
                {
                    return true;
                }

                if (_road != null)
                {
                    return VehicleRouteCargoFilter.IsFreightAcceptedAtOtherRoadStop(
                        _world,
                        _road,
                        _catalog,
                        _currentStationId,
                        cargoId);
                }

                return VehicleRouteCargoFilter.IsFreightAcceptedAtOtherRailStop(
                    _world,
                    _rail!,
                    _catalog,
                    _currentStationId,
                    cargoId);
            }

            public void NoteLoadStart()
            {
                if (_world == null)
                {
                    return;
                }

                if (_road != null)
                {
                    DeliveryPay.NoteLoadStart(_world, _road);
                    return;
                }

                DeliveryPay.NoteLoadStart(_world, _rail!);
            }

            public static LoaderSlot ForRoad(
                RoadVehicle vehicle,
                int siteIndex,
                int currentStationId,
                GeneratedWorld world,
                ContentCatalog? catalog)
            {
                return new LoaderSlot(0, vehicle.Id, siteIndex, vehicle, null, currentStationId, world, catalog);
            }

            public static LoaderSlot ForRail(
                RailVehicle vehicle,
                int siteIndex,
                int currentStationId,
                GeneratedWorld world,
                ContentCatalog? catalog)
            {
                return new LoaderSlot(1, vehicle.Id, siteIndex, null, vehicle, currentStationId, world, catalog);
            }
        }
    }
}
