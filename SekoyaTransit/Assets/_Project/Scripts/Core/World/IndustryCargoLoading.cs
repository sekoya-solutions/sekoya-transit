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
        /// Distributes one cargo unit from each industry to the next loading vehicle (round-robin
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
            List<LoaderSlot>? loaders = null;
            for (int industryIndex = 0; industryIndex < world.Industries.Count; industryIndex++)
            {
                PlacedIndustry industry = world.Industries[industryIndex];
                loaders?.Clear();
                CollectLoadingVehicles(world, industry, catalog, ref loaders);
                if (loaders == null || loaders.Count == 0)
                {
                    continue;
                }

                loaders.Sort(LoaderComparer.Instance);
                int start = industry.LoadRoundRobinIndex % loaders.Count;
                LoaderSlot chosen = loaders[start];
                if (calendarDate.HasValue)
                {
                    industry.EnsureMonthStats(calendarDate.Value);
                }

                // Advance the cursor only when a unit actually moves so empty-industry
                // wait-for-full ticks do not scramble fairness among loaders.
                if (TryLoadOneUnit(industry, chosen))
                {
                    industry.SetLoadRoundRobinIndex(start + 1);
                }
            }
        }

        private static void CollectLoadingVehicles(
            GeneratedWorld world,
            PlacedIndustry industry,
            ContentCatalog? catalog,
            ref List<LoaderSlot>? loaders)
        {
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                if (!IsRoadLoadingAtIndustry(world, vehicle, industry))
                {
                    continue;
                }

                if (!vehicle.TryGetCurrentOrder(out RoadVehicleOrder order))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRoad(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRoad(vehicle, order.StationId, world, catalog));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                if (!IsRailLoadingAtIndustry(world, vehicle, industry))
                {
                    continue;
                }

                if (!vehicle.TryGetCurrentOrder(out RailVehicleOrder order))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRail(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRail(vehicle, order.StationId, world, catalog));
            }
        }

        private static bool IsRoadLoadingAtIndustry(
            GeneratedWorld world,
            RoadVehicle vehicle,
            PlacedIndustry industry)
        {
            if (!vehicle.IsServicingStop || !vehicle.IsUnloadComplete)
            {
                return false;
            }

            if (vehicle.FreeCargoCapacity <= 0)
            {
                return false;
            }

            if (!vehicle.TryGetCurrentOrder(out RoadVehicleOrder order))
            {
                return false;
            }

            if (!world.TryGetRoadStation(order.StationId, out RoadStation? station) || station == null)
            {
                return false;
            }

            if (!StationAdjacency.TryGetAdjacentIndustry(world, station, out PlacedIndustry? adjacent) ||
                adjacent == null)
            {
                return false;
            }

            return SameIndustry(adjacent, industry);
        }

        private static bool IsRailLoadingAtIndustry(
            GeneratedWorld world,
            RailVehicle vehicle,
            PlacedIndustry industry)
        {
            if (!vehicle.IsServicingStop || !vehicle.IsUnloadComplete)
            {
                return false;
            }

            if (vehicle.FreeCargoCapacity <= 0)
            {
                return false;
            }

            if (!vehicle.TryGetCurrentOrder(out RailVehicleOrder order))
            {
                return false;
            }

            if (!world.TryGetRailStation(order.StationId, out RailStation? station) || station == null)
            {
                return false;
            }

            if (!StationAdjacency.TryGetAdjacentIndustry(world, station, out PlacedIndustry? adjacent) ||
                adjacent == null)
            {
                return false;
            }

            return SameIndustry(adjacent, industry);
        }

        private static bool SameIndustry(PlacedIndustry left, PlacedIndustry right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return string.Equals(left.IndustryId, right.IndustryId, StringComparison.Ordinal) &&
                   left.InstanceIndex == right.InstanceIndex &&
                   left.Coordinate.Equals(right.Coordinate);
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
                if (!loader.AllowsLoadCargo(stack.CargoId))
                {
                    continue;
                }

                int excluded = unloadedThisVisit.GetAmount(stack.CargoId);
                if (stack.Amount > excluded)
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
                RoadVehicle? road,
                RailVehicle? rail,
                int currentStationId,
                GeneratedWorld? world,
                ContentCatalog? catalog)
            {
                Mode = mode;
                Id = id;
                _road = road;
                _rail = rail;
                _currentStationId = currentStationId;
                _world = world;
                _catalog = catalog;
            }

            public byte Mode { get; }

            public int Id { get; }

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
                int currentStationId,
                GeneratedWorld world,
                ContentCatalog? catalog)
            {
                return new LoaderSlot(0, vehicle.Id, vehicle, null, currentStationId, world, catalog);
            }

            public static LoaderSlot ForRail(
                RailVehicle vehicle,
                int currentStationId,
                GeneratedWorld world,
                ContentCatalog? catalog)
            {
                return new LoaderSlot(1, vehicle.Id, null, vehicle, currentStationId, world, catalog);
            }
        }
    }
}
