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
            ContentCatalog? catalog = null,
            GameDate? calendarDate = null)
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
            bool blockedByIndustryCapacity = UnloadAtStop(
                world,
                vehicle,
                station,
                ref budget,
                catalog,
                calendarDate);
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
        public static void DistributeIndustryLoads(
            GeneratedWorld world,
            ContentCatalog? catalog = null,
            GameDate? calendarDate = null)
        {
            IndustryCargoLoading.Distribute(world, catalog, calendarDate);
            TownCargoLoading.Distribute(world, catalog, calendarDate);
        }

        /// <summary>
        /// Leaves the stop when unload is done and leave rules are satisfied; otherwise stays.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the vehicle should leave and advance to the next order;
        /// <see langword="false"/> when it should remain servicing.
        /// </returns>
        public static bool TryFinishService(
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
            bool siteHasLoadableCargo = false;
            if (vehicle.FreeCargoCapacity > 0)
            {
                if (StationAdjacency.TryGetAdjacentIndustry(world, station, out PlacedIndustry? industry) &&
                    industry != null)
                {
                    siteHasLoadableCargo = HasLoadableCargo(
                        world,
                        industry,
                        vehicle.CargoUnloadedThisVisit,
                        vehicle,
                        order.StationId,
                        catalog);
                }

                if (!siteHasLoadableCargo &&
                    StationAdjacency.TryGetAdjacentTown(world, station, out Town? town) &&
                    town != null)
                {
                    siteHasLoadableCargo = HasLoadableTownCargo(town, vehicle);
                }
            }

            if (order.WaitForFull && !isFull)
            {
                return false;
            }

            if (!order.WaitForFull && siteHasLoadableCargo)
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
            if (!AdvanceUnloadTick(world, vehicle, catalog, calendarDate))
            {
                return false;
            }

            DistributeIndustryLoads(world, catalog, calendarDate);
            return TryFinishService(world, vehicle, catalog);
        }

        /// <summary>
        /// Unloads from the vehicle. Returns <see langword="true"/> when the next cargo cannot be delivered here
        /// (unload phase should complete with cargo left on the vehicle).
        /// </summary>
        private static bool UnloadAtStop(
            GeneratedWorld world,
            RoadVehicle vehicle,
            RoadStation station,
            ref int budget,
            ContentCatalog? catalog,
            GameDate? calendarDate)
        {
            while (budget > 0 && !vehicle.Cargo.IsEmpty)
            {
                CargoAmount stack = vehicle.Cargo.Stacks[0];
                bool consumeFreight = DeliveryAcceptance.ConsumesFreightOnUnload(
                    world,
                    station,
                    stack.CargoId,
                    catalog);
                bool consumePassenger = DeliveryAcceptance.ConsumesPassengerOnUnload(
                    world,
                    station,
                    stack.CargoId,
                    catalog,
                    vehicle.PassengerCargoOriginTownId);

                if (consumeFreight || consumePassenger)
                {
                    int move = stack.Amount < budget ? stack.Amount : budget;
                    int removed = vehicle.Cargo.TryRemove(stack.CargoId, move);
                    if (removed <= 0)
                    {
                        break;
                    }

                    budget -= removed;
                    CreditIfAccepted(
                        world,
                        vehicle,
                        station,
                        stack.CargoId,
                        removed,
                        catalog,
                        calendarDate,
                        creditNeedSupply: consumeFreight);
                    if (consumePassenger &&
                        catalog != null &&
                        PassengerCargo.CountPassengerClass(vehicle.Cargo, catalog) == 0)
                    {
                        vehicle.SetPassengerCargoOriginTownId(PassengerCargo.NoOriginTownId);
                    }

                    continue;
                }

                if (!StationAdjacency.TryGetAdjacentIndustry(world, station, out PlacedIndustry? industry) ||
                    industry == null)
                {
                    // No industry to stock into and not town-consumed: leave cargo on vehicle.
                    return true;
                }

                // Only recipe inputs are delivered; they turn into output at once and are never stocked, so a full
                // output stock does not block the delivery (Issue #193). Other cargo stays on the vehicle.
                if (catalog == null ||
                    !catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) ||
                    definition == null ||
                    !IndustryProduction.IsInput(definition, stack.CargoId))
                {
                    return true;
                }

                int deliverMove = stack.Amount < budget ? stack.Amount : budget;
                int stocked = vehicle.Cargo.TryRemove(stack.CargoId, deliverMove);
                if (stocked <= 0)
                {
                    break;
                }

                IndustryProduction.TryDeliverInput(industry, definition, stack.CargoId, stocked, calendarDate);
                vehicle.CargoUnloadedThisVisit.Add(stack.CargoId, stocked);
                budget -= stocked;
                CreditIfAccepted(
                    world,
                    vehicle,
                    station,
                    stack.CargoId,
                    stocked,
                    catalog,
                    calendarDate,
                    creditNeedSupply: true);
            }

            return false;
        }

        private static void CreditIfAccepted(
            GeneratedWorld world,
            RoadVehicle vehicle,
            RoadStation station,
            string cargoId,
            int amount,
            ContentCatalog? catalog,
            GameDate? calendarDate,
            bool creditNeedSupply)
        {
            if (catalog == null || amount <= 0)
            {
                return;
            }

            if (!DeliveryAcceptance.Accepts(world, station, cargoId, catalog))
            {
                return;
            }

            if (creditNeedSupply &&
                StationAdjacency.TryGetAdjacentTown(world, station, out Town? town) &&
                town != null)
            {
                if (calendarDate.HasValue)
                {
                    town.EnsureMonthStats(calendarDate.Value);
                }

                town.CreditNeedSupply(cargoId, amount);
            }

            if (!catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) || cargo == null)
            {
                return;
            }

            DeliveryPaySettings pay = catalog.Economy.DeliveryPay;
            int remainder = vehicle.DeliveryPayRemainder;
            long income = DeliveryPay.Compute(
                cargo.PricePerUnit,
                amount,
                DeliveryPay.ResolveDistance(vehicle.CargoOriginTile, station.Coordinate, pay),
                pay,
                ref remainder);
            vehicle.SetDeliveryPayRemainder(remainder);
            world.CreditCompanyCash(income, FinanceCategory.DeliveryIncome);
            vehicle.CreditLifetimeDeliveryIncome(income);
            world.RecordCargoDelivered(
                cargoId,
                StationAdjacency.TryGetAdjacentTown(world, station, out _)
                    ? DeliveryDestinationKind.Town
                    : DeliveryDestinationKind.Industry,
                amount);
            world.RecordContractDelivery(station.Coordinate, cargoId, amount);
        }

        private static bool HasLoadableCargo(
            GeneratedWorld world,
            PlacedIndustry industry,
            CargoInventory unloadedThisVisit,
            RoadVehicle vehicle,
            int currentStationId,
            ContentCatalog? catalog)
        {
            return FindLoadableStackIndex(
                world,
                industry.Cargo,
                unloadedThisVisit,
                vehicle,
                currentStationId,
                catalog) >= 0;
        }

        private static bool HasLoadableTownCargo(Town town, RoadVehicle vehicle)
        {
            if (vehicle.PassengerCargoOriginTownId != PassengerCargo.NoOriginTownId &&
                vehicle.PassengerCargoOriginTownId != town.Id)
            {
                return false;
            }

            for (int index = 0; index < town.WaitingCargo.Stacks.Count; index++)
            {
                CargoAmount stack = town.WaitingCargo.Stacks[index];
                if (stack.Amount > 0 && vehicle.AllowsLoadCargo(stack.CargoId))
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindLoadableStackIndex(
            GeneratedWorld world,
            CargoInventory industryCargo,
            CargoInventory unloadedThisVisit,
            RoadVehicle vehicle,
            int currentStationId,
            ContentCatalog? catalog)
        {
            for (int index = 0; index < industryCargo.Stacks.Count; index++)
            {
                CargoAmount stack = industryCargo.Stacks[index];
                if (catalog != null)
                {
                    if (!VehicleRouteCargoFilter.IsFreightAcceptedAtOtherRoadStop(
                        world,
                        vehicle,
                        catalog,
                        currentStationId,
                        stack.CargoId))
                    {
                        continue;
                    }
                }
                else if (!vehicle.AllowedLoadCargoDirty && !vehicle.AllowsLoadCargo(stack.CargoId))
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
    }
}
