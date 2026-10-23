#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic unload-then-load transfer between a road vehicle and an adjacent industry
    /// (or town consume-on-unload). Stations hold no cargo. Industry loads are 1 unit/tick
    /// round-robin across all loading vehicles; unload is 1 unit/tick per vehicle (Issue #132).
    /// </summary>
    public static class RoadVehicleCargoTransfer
    {
        /// <summary>
        /// Cargo units moved per simulation tick for unload (per vehicle) and for industry load
        /// distribution (one unit total per industry, round-robin).
        /// </summary>
        public const int UnitsPerTick = 1;

        /// <summary>
        /// Performs one tick of unload at the vehicle's current order stop.
        /// Load distribution and leave decisions run via <see cref="DistributeIndustryLoads"/> and
        /// <see cref="TryFinishService"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when unload is complete for this visit;
        /// <see langword="false"/> when the vehicle must keep unloading.
        /// </returns>
        public static bool AdvanceUnloadTick(
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

            if (vehicle.IsUnloadComplete)
            {
                return true;
            }

            int budget = UnitsPerTick;
            bool blockedByIndustryCapacity = UnloadAtStop(world, vehicle, station, ref budget, catalog);
            if (vehicle.Cargo.IsEmpty || blockedByIndustryCapacity)
            {
                vehicle.MarkUnloadComplete();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Distributes one cargo unit from each industry to the next loading vehicle (round-robin).
        /// Delegates to <see cref="IndustryCargoLoading.Distribute"/> so road and rail share one pass.
        /// </summary>
        public static void DistributeIndustryLoads(GeneratedWorld world, GameDate? calendarDate = null)
        {
            IndustryCargoLoading.Distribute(world, calendarDate);
        }

        /// <summary>
        /// Leaves the stop when unload is done and leave rules are satisfied; otherwise stays.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the vehicle should leave and advance to the next order;
        /// <see langword="false"/> when it should remain servicing.
        /// </returns>
        public static bool TryFinishService(GeneratedWorld world, RoadVehicle vehicle)
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
                return false;
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

            if (!vehicle.IsUnloadComplete)
            {
                return false;
            }

            bool isFull = vehicle.FreeCargoCapacity == 0;
            bool industryHasLoadableCargo = false;
            if (StationAdjacency.TryGetAdjacentIndustry(world, station, out PlacedIndustry? industry) &&
                industry != null &&
                vehicle.FreeCargoCapacity > 0)
            {
                industryHasLoadableCargo = HasLoadableCargo(industry, vehicle.CargoUnloadedThisVisit);
            }

            if (order.WaitForFull && !isFull)
            {
                return false;
            }

            if (!order.WaitForFull && industryHasLoadableCargo)
            {
                return false;
            }

            vehicle.FinishServicingStop();
            return true;
        }

        /// <summary>
        /// Legacy single-vehicle service step used by older call sites: unload, then one industry
        /// load pass for all industries, then leave decision. Prefer
        /// <see cref="RoadVehicleMovement.AdvanceAll"/> which batches loads once per tick.
        /// </summary>
        public static bool AdvanceServiceTick(
            GeneratedWorld world,
            RoadVehicle vehicle,
            ContentCatalog? catalog = null,
            GameDate? calendarDate = null)
        {
            if (!AdvanceUnloadTick(world, vehicle, catalog))
            {
                return false;
            }

            DistributeIndustryLoads(world, calendarDate);
            return TryFinishService(world, vehicle);
        }

        /// <summary>
        /// Unloads from the vehicle. Returns <see langword="true"/> when stocking is blocked
        /// by industry capacity (unload phase should complete with cargo left on the vehicle).
        /// </summary>
        private static bool UnloadAtStop(
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

                if (!StationAdjacency.TryGetAdjacentIndustry(world, station, out PlacedIndustry? industry) ||
                    industry == null)
                {
                    // No industry to stock into and not town-consumed: leave cargo on vehicle.
                    return true;
                }

                int free = industry.FreeCargoCapacity;
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

                industry.Cargo.Add(stack.CargoId, stocked);
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

        private static bool HasLoadableCargo(PlacedIndustry industry, CargoInventory unloadedThisVisit)
        {
            return FindLoadableStackIndex(industry.Cargo, unloadedThisVisit) >= 0;
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
    }
}
