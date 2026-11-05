#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
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
                int townIndex = loaders[first].SiteIndex;
                int end = first + 1;
                while (end < loaders.Count && loaders[end].SiteIndex == townIndex)
                {
                    end++;
                }

                Town town = world.Towns[townIndex];
                int start = town.LoadRoundRobinIndex % (end - first);
                LoaderSlot chosen = loaders[first + start];
                if (TryLoadOneUnit(world, town, chosen, catalog))
                {
                    town.SetLoadRoundRobinIndex(start + 1);
                }

                first = end;
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
                if (!TryGetRoadLoadingTown(world, vehicle, out int townIndex))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRoad(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRoad(vehicle, townIndex));
            }

            for (int index = 0; index < world.RailVehicles.Count; index++)
            {
                RailVehicle vehicle = world.RailVehicles[index];
                if (!TryGetRailLoadingTown(world, vehicle, out int townIndex))
                {
                    continue;
                }

                if (catalog != null)
                {
                    VehicleRouteCargoFilter.EnsureRail(world, vehicle, catalog);
                }

                (loaders ??= new List<LoaderSlot>()).Add(LoaderSlot.ForRail(vehicle, townIndex));
            }
        }

        private static bool TryGetRoadLoadingTown(GeneratedWorld world, RoadVehicle vehicle, out int townIndex)
        {
            townIndex = -1;
            return vehicle.IsServicingStop &&
                   vehicle.IsUnloadComplete &&
                   vehicle.FreeCargoCapacity > 0 &&
                   vehicle.TryGetCurrentOrder(out RoadVehicleOrder order) &&
                   world.TryGetRoadStation(order.StationId, out RoadStation? station) &&
                   station != null &&
                   StationAdjacency.TryGetAdjacentTownIndex(world, station.Coordinate, out townIndex);
        }

        private static bool TryGetRailLoadingTown(GeneratedWorld world, RailVehicle vehicle, out int townIndex)
        {
            townIndex = -1;
            return vehicle.IsServicingStop &&
                   vehicle.IsUnloadComplete &&
                   vehicle.FreeCargoCapacity > 0 &&
                   vehicle.TryGetCurrentOrder(out RailVehicleOrder order) &&
                   world.TryGetRailStation(order.StationId, out RailStation? station) &&
                   station != null &&
                   StationAdjacency.TryGetAdjacentTownIndex(world, station.Coordinate, out townIndex);
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

            private LoaderSlot(byte mode, int id, int siteIndex, RoadVehicle? road, RailVehicle? rail)
            {
                Mode = mode;
                Id = id;
                SiteIndex = siteIndex;
                _road = road;
                _rail = rail;
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

            public static LoaderSlot ForRoad(RoadVehicle vehicle, int siteIndex)
            {
                return new LoaderSlot(0, vehicle.Id, siteIndex, vehicle, null);
            }

            public static LoaderSlot ForRail(RailVehicle vehicle, int siteIndex)
            {
                return new LoaderSlot(1, vehicle.Id, siteIndex, null, vehicle);
            }
        }
    }
}
