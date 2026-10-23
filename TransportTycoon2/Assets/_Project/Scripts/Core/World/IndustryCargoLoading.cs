#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
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
        /// </summary>
        public static void Distribute(GeneratedWorld world, GameDate? calendarDate = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            for (int industryIndex = 0; industryIndex < world.Industries.Count; industryIndex++)
            {
                PlacedIndustry industry = world.Industries[industryIndex];
                List<LoaderSlot> loaders = CollectLoadingVehicles(world, industry);
                if (loaders.Count == 0)
                {
                    continue;
                }

                loaders.Sort(CompareLoaders);
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

        private static List<LoaderSlot> CollectLoadingVehicles(GeneratedWorld world, PlacedIndustry industry)
        {
            var loaders = new List<LoaderSlot>();
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                if (!IsRoadLoadingAtIndustry(world, vehicle, industry))
                {
                    continue;
                }

                loaders.Add(LoaderSlot.ForRoad(vehicle));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                if (!IsRailLoadingAtIndustry(world, vehicle, industry))
                {
                    continue;
                }

                loaders.Add(LoaderSlot.ForRail(vehicle));
            }

            return loaders;
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
            int stackIndex = FindLoadableStackIndex(industry.Cargo, unloaded);
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

            cargo.Add(stack.CargoId, removed);
            industry.CreditMonthTransported(removed);
            return true;
        }

        private static int FindLoadableStackIndex(CargoInventory industryCargo, CargoInventory unloadedThisVisit)
        {
            for (int index = 0; index < industryCargo.Stacks.Count; index++)
            {
                CargoAmount stack = industryCargo.Stacks[index];
                int excluded = unloadedThisVisit.GetAmount(stack.CargoId);
                if (stack.Amount > excluded)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int CompareLoaders(LoaderSlot left, LoaderSlot right)
        {
            int modeCompare = left.Mode.CompareTo(right.Mode);
            if (modeCompare != 0)
            {
                return modeCompare;
            }

            return left.Id.CompareTo(right.Id);
        }

        private readonly struct LoaderSlot
        {
            private readonly RoadVehicle? _road;
            private readonly RailVehicle? _rail;

            private LoaderSlot(byte mode, int id, RoadVehicle? road, RailVehicle? rail)
            {
                Mode = mode;
                Id = id;
                _road = road;
                _rail = rail;
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

            public static LoaderSlot ForRoad(RoadVehicle vehicle)
            {
                return new LoaderSlot(0, vehicle.Id, vehicle, null);
            }

            public static LoaderSlot ForRail(RailVehicle vehicle)
            {
                return new LoaderSlot(1, vehicle.Id, null, vehicle);
            }
        }
    }
}
