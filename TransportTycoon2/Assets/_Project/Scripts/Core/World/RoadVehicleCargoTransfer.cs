#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic unload-then-load transfer between a road vehicle and a station.
    /// Town freight is consumed on unload (not stocked). Cargo unloaded into a station
    /// bag this visit is not loaded back onto the same vehicle.
    /// </summary>
    public static class RoadVehicleCargoTransfer
    {
        /// <summary>
        /// Cargo units moved per simulation tick while servicing a stop.
        /// </summary>
        public const int UnitsPerTick = 2;

        /// <summary>
        /// Performs one tick of cargo service at the vehicle's current order stop.
        /// When <paramref name="catalog"/> is provided, accepted unloads credit company cash.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the vehicle should leave and advance to the next order;
        /// <see langword="false"/> when it should remain servicing.
        /// </returns>
        public static bool AdvanceServiceTick(
            GeneratedWorld world,
            RoadVehicle vehicle,
            ContentCatalog? catalog = null)
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
#pragma warning restore CA1510

            if (!vehicle.IsServicingStop)
            {
                throw new InvalidOperationException("Vehicle is not servicing a stop.");
            }

            if (!vehicle.TryGetCurrentOrder(out RoadVehicleOrder order))
            {
                vehicle.FinishServicingStop();
                return true;
            }

            if (!world.TryGetRoadStation(order.StationId, out RoadStation? station) || station == null)
            {
                vehicle.FinishServicingStop();
                return true;
            }

            int budget = UnitsPerTick;
            if (!vehicle.IsUnloadComplete)
            {
                bool blockedByStationCapacity = UnloadAtStation(world, vehicle, station, ref budget, catalog);
                if (vehicle.Cargo.IsEmpty || blockedByStationCapacity)
                {
                    vehicle.MarkUnloadComplete();
                }
                else
                {
                    return false;
                }
            }

            if (budget > 0 && vehicle.FreeCargoCapacity > 0)
            {
                LoadFromStation(vehicle, station, ref budget);
            }

            bool isFull = vehicle.FreeCargoCapacity == 0;
            bool stationHasLoadableCargo =
                vehicle.FreeCargoCapacity > 0 && HasLoadableCargo(station, vehicle.CargoUnloadedThisVisit);
            if (stationHasLoadableCargo)
            {
                return false;
            }

            if (order.WaitForFull && !isFull)
            {
                return false;
            }

            vehicle.FinishServicingStop();
            return true;
        }

        /// <summary>
        /// Unloads from the vehicle. Returns <see langword="true"/> when stocking is blocked
        /// by station capacity (unload phase should complete with cargo left on the vehicle).
        /// </summary>
        private static bool UnloadAtStation(
            GeneratedWorld world,
            RoadVehicle vehicle,
            RoadStation station,
            ref int budget,
            ContentCatalog? catalog)
        {
            while (budget > 0 && !vehicle.Cargo.IsEmpty)
            {
                CargoAmount stack = vehicle.Cargo.Stacks[0];
                bool consumeFreight = DeliveryAcceptance.ConsumesFreightOnUnload(
                    world,
                    station,
                    stack.CargoId,
                    catalog);

                if (consumeFreight)
                {
                    int move = stack.Amount < budget ? stack.Amount : budget;
                    int removed = vehicle.Cargo.TryRemove(stack.CargoId, move);
                    if (removed <= 0)
                    {
                        break;
                    }

                    budget -= removed;
                    CreditIfAccepted(world, vehicle, station, stack.CargoId, removed, catalog);
                    continue;
                }

                int free = station.FreeCargoCapacity;
                if (free <= 0)
                {
                    return true;
                }

                int stockMove = stack.Amount;
                if (stockMove > budget)
                {
                    stockMove = budget;
                }

                if (stockMove > free)
                {
                    stockMove = free;
                }

                int stocked = vehicle.Cargo.TryRemove(stack.CargoId, stockMove);
                if (stocked <= 0)
                {
                    break;
                }

                station.Cargo.Add(stack.CargoId, stocked);
                vehicle.CargoUnloadedThisVisit.Add(stack.CargoId, stocked);
                budget -= stocked;
                CreditIfAccepted(world, vehicle, station, stack.CargoId, stocked, catalog);
            }

            return false;
        }

        private static void CreditIfAccepted(
            GeneratedWorld world,
            RoadVehicle vehicle,
            RoadStation station,
            string cargoId,
            int amount,
            ContentCatalog? catalog)
        {
            if (catalog == null || amount <= 0)
            {
                return;
            }

            if (!DeliveryAcceptance.Accepts(world, station, cargoId, catalog))
            {
                return;
            }

            if (!catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) || cargo == null)
            {
                return;
            }

            long income = (long)cargo.PricePerUnit * amount;
            world.CreditCompanyCash(income);
            vehicle.CreditLifetimeDeliveryIncome(income);
        }

        private static void LoadFromStation(RoadVehicle vehicle, RoadStation station, ref int budget)
        {
            while (budget > 0 && vehicle.FreeCargoCapacity > 0)
            {
                int stackIndex = FindLoadableStackIndex(station.Cargo, vehicle.CargoUnloadedThisVisit);
                if (stackIndex < 0)
                {
                    return;
                }

                CargoAmount stack = station.Cargo.Stacks[stackIndex];
                int excluded = vehicle.CargoUnloadedThisVisit.GetAmount(stack.CargoId);
                int loadable = stack.Amount - excluded;
                if (loadable <= 0)
                {
                    return;
                }

                int move = loadable;
                if (move > budget)
                {
                    move = budget;
                }

                if (move > vehicle.FreeCargoCapacity)
                {
                    move = vehicle.FreeCargoCapacity;
                }

                int removed = station.Cargo.TryRemove(stack.CargoId, move);
                if (removed <= 0)
                {
                    return;
                }

                vehicle.Cargo.Add(stack.CargoId, removed);
                budget -= removed;
            }
        }

        private static bool HasLoadableCargo(RoadStation station, CargoInventory unloadedThisVisit)
        {
            return FindLoadableStackIndex(station.Cargo, unloadedThisVisit) >= 0;
        }

        /// <summary>
        /// Returns the index of the first station stack that still has units beyond what this visit unloaded.
        /// </summary>
        private static int FindLoadableStackIndex(CargoInventory stationCargo, CargoInventory unloadedThisVisit)
        {
            for (int index = 0; index < stationCargo.Stacks.Count; index++)
            {
                CargoAmount stack = stationCargo.Stacks[index];
                int excluded = unloadedThisVisit.GetAmount(stack.CargoId);
                if (stack.Amount > excluded)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
