#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic unload-then-load transfer between a road vehicle and a station.
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
                UnloadIntoStation(world, vehicle, station, ref budget, catalog);
                if (vehicle.Cargo.IsEmpty || station.FreeCargoCapacity == 0)
                {
                    vehicle.MarkUnloadComplete();
                }
                else
                {
                    return false;
                }
            }

            if (budget > 0 && vehicle.FreeCargoCapacity > 0 && !station.Cargo.IsEmpty)
            {
                station.Cargo.TransferTo(vehicle.Cargo, budget, vehicle.FreeCargoCapacity);
            }

            bool isFull = vehicle.FreeCargoCapacity == 0;
            bool stationHasLoadableCargo = !station.Cargo.IsEmpty && vehicle.FreeCargoCapacity > 0;
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

        private static void UnloadIntoStation(
            GeneratedWorld world,
            RoadVehicle vehicle,
            RoadStation station,
            ref int budget,
            ContentCatalog? catalog)
        {
            while (budget > 0 && !vehicle.Cargo.IsEmpty && station.FreeCargoCapacity > 0)
            {
                CargoAmount stack = vehicle.Cargo.Stacks[0];
                int free = station.FreeCargoCapacity;
                int move = stack.Amount;
                if (move > budget)
                {
                    move = budget;
                }

                if (move > free)
                {
                    move = free;
                }

                int removed = vehicle.Cargo.TryRemove(stack.CargoId, move);
                if (removed <= 0)
                {
                    break;
                }

                station.Cargo.Add(stack.CargoId, removed);
                budget -= removed;

                if (catalog != null &&
                    DeliveryAcceptance.Accepts(world, station, stack.CargoId, catalog) &&
                    catalog.TryGetCargo(stack.CargoId, out CargoDefinition? cargo) &&
                    cargo != null)
                {
                    world.CreditCompanyCash((long)cargo.PricePerUnit * removed);
                }
            }
        }
    }
}
