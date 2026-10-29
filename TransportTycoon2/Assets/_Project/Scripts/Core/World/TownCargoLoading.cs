#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Shared once-per-tick town passenger/mail load distribution across road and rail vehicles
    /// waiting at stops adjacent to the same town (Issue #59).
    /// </summary>
    public static class TownCargoLoading
    {
        /// <summary>
        /// Distributes one cargo unit from each town to the next loading vehicle (round-robin).
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

            _ = calendarDate;

            // One buffer per call, created only when a vehicle is loading, so idle ticks allocate nothing (Issue #70).
            List<LoaderSlot>? loaders = null;
            for (int townIndex = 0; townIndex < world.Towns.Count; townIndex++)
            {
                Town town = world.Towns[townIndex];
                loaders?.Clear();
                CollectLoadingVehicles(world, town, catalog, ref loaders);
                if (loaders == null || loaders.Count == 0)
                {
                    continue;
                }

                loaders.Sort(LoaderComparer.Instance);
                int start = town.LoadRoundRobinIndex % loaders.Count;
                LoaderSlot chosen = loaders[start];
                if (TryLoadOneUnit(world, town, chosen, catalog))
                {
                    town.SetLoadRoundRobinIndex(start + 1);
                }
            }
        }

        private static void CollectLoadingVehicles(
            GeneratedWorld world,
            Town town,
            ContentCatalog? catalog,
            ref List<LoaderSlot>? loaders)
        {
            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle vehicle = world.RoadVehicles[index];
                if (!IsRoadLoadingAtTown(world, vehicle, town))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRoad(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRoad(vehicle));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                if (!IsRailLoadingAtTown(world, vehicle, town))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRail(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRail(vehicle));
            }
        }

        private static bool IsRoadLoadingAtTown(GeneratedWorld world, RoadVehicle vehicle, Town town)
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

            if (!StationAdjacency.TryGetAdjacentTown(world, station, out Town? adjacent) ||
                adjacent == null)
            {
                return false;
            }

            return adjacent.Id == town.Id;
        }

        private static bool IsRailLoadingAtTown(GeneratedWorld world, RailVehicle vehicle, Town town)
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

            if (!StationAdjacency.TryGetAdjacentTown(world, station, out Town? adjacent) ||
                adjacent == null)
            {
                return false;
            }

            return adjacent.Id == town.Id;
        }

        private static bool TryLoadOneUnit(GeneratedWorld world, Town town, LoaderSlot loader, ContentCatalog? catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            if (loader.PassengerCargoOriginTownId != PassengerCargo.NoOriginTownId &&
                loader.PassengerCargoOriginTownId != town.Id)
            {
                return false;
            }

            int stackIndex = FindLoadableStackIndex(town.WaitingCargo, loader, catalog);
            if (stackIndex < 0 || loader.FreeCargoCapacity <= 0)
            {
                return false;
            }

            CargoAmount stack = town.WaitingCargo.Stacks[stackIndex];
            string cargoId = stack.CargoId;
            int removed = town.WaitingCargo.TryRemove(cargoId, 1);
            if (removed <= 0)
            {
                return false;
            }

            loader.NoteLoadStart(world);
            loader.Cargo.Add(cargoId, removed);
            loader.SetPassengerCargoOriginTownId(town.Id);
            if (string.Equals(cargoId, TownPassengerGeneration.PassengersCargoId, StringComparison.Ordinal))
            {
                town.CreditMonthPassengersTransported(removed);
            }
            else if (string.Equals(cargoId, TownPassengerGeneration.MailCargoId, StringComparison.Ordinal))
            {
                town.CreditMonthMailTransported(removed);
            }

            return true;
        }

        private static int FindLoadableStackIndex(
            CargoInventory waitingCargo,
            LoaderSlot loader,
            ContentCatalog catalog)
        {
            for (int index = 0; index < waitingCargo.Stacks.Count; index++)
            {
                CargoAmount stack = waitingCargo.Stacks[index];
                if (!PassengerCargo.IsPassengerClass(catalog, stack.CargoId))
                {
                    continue;
                }

                if (!loader.AllowsLoadCargo(stack.CargoId))
                {
                    continue;
                }

                if (stack.Amount > 0)
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

            public int FreeCargoCapacity
            {
                get
                {
                    return _road != null ? _road.FreeCargoCapacity : _rail!.FreeCargoCapacity;
                }
            }

            public int PassengerCargoOriginTownId
            {
                get
                {
                    return _road != null
                        ? _road.PassengerCargoOriginTownId
                        : _rail!.PassengerCargoOriginTownId;
                }
            }

            public bool AllowsLoadCargo(string cargoId)
            {
                if (_road != null)
                {
                    if (_road.AllowedLoadCargoDirty)
                    {
                        return true;
                    }

                    return _road.AllowsLoadCargo(cargoId);
                }

                if (_rail!.AllowedLoadCargoDirty)
                {
                    return true;
                }

                return _rail.AllowsLoadCargo(cargoId);
            }

            public void NoteLoadStart(GeneratedWorld world)
            {
                if (_road != null)
                {
                    DeliveryPay.NoteLoadStart(world, _road);
                    return;
                }

                DeliveryPay.NoteLoadStart(world, _rail!);
            }

            public void SetPassengerCargoOriginTownId(int townId)
            {
                if (_road != null)
                {
                    _road.SetPassengerCargoOriginTownId(townId);
                    return;
                }

                _rail!.SetPassengerCargoOriginTownId(townId);
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
