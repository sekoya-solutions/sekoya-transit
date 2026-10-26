#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A player-owned rail vehicle (train) that follows looping station orders along the rail graph.
    /// </summary>
    public sealed class RailVehicle
    {
        /// <summary>
        /// Starter rail locomotive type id (<c>loco_mk1</c>).
        /// </summary>
        public const string StarterTypeId = "loco_mk1";

        /// <summary>
        /// Legacy integer type id used in save formats before string type ids (Issue #145).
        /// </summary>
        public const int LegacyPlaceholderTypeId = 0;

        /// <summary>
        /// Legacy max wagons for the starter locomotive / pre-catalog saves.
        /// </summary>
        public const int LegacyMaxWagons = 3;

        /// <summary>
        /// Legacy capacity per wagon for the starter locomotive / pre-catalog saves.
        /// </summary>
        public const int LegacyCapacityPerWagon = 10;

        /// <summary>
        /// Full-consist placeholder capacity for migrating older saves that assumed a fixed three-wagon train.
        /// </summary>
        public const int PlaceholderCargoCapacity = LegacyMaxWagons * LegacyCapacityPerWagon;

        private readonly List<RailVehicleOrder> _orders;
        private readonly ReadOnlyCollection<RailVehicleOrder> _ordersView;
        private readonly List<int> _orderStationIds;
        private readonly ReadOnlyCollection<int> _orderStationIdsView;
        private readonly List<string> _allowedLoadCargoIds;
        private readonly ReadOnlyCollection<string> _allowedLoadCargoIdsView;
        private bool _allowedLoadCargoDirty;

        /// <summary>
        /// Initializes a vehicle parked at a station node with no orders.
        /// </summary>
        /// <param name="id">Stable non-negative vehicle id.</param>
        /// <param name="typeId">Vehicle type content id (use <see cref="StarterTypeId"/> for the starter loco).</param>
        /// <param name="stationNodeId">Graph node id of the spawn station (tile id).</param>
        /// <param name="maxWagons">Maximum wagons this locomotive may haul.</param>
        /// <param name="capacityPerWagon">Cargo capacity contributed by each wagon.</param>
        public RailVehicle(
            int id,
            string typeId,
            int stationNodeId,
            int maxWagons = LegacyMaxWagons,
            int capacityPerWagon = LegacyCapacityPerWagon)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Vehicle id cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Type id cannot be null or whitespace.", nameof(typeId));
            }

            if (stationNodeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stationNodeId), stationNodeId, "Station node id cannot be negative.");
            }

            if (maxWagons < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWagons), maxWagons, "Max wagons must be positive.");
            }

            if (capacityPerWagon < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacityPerWagon),
                    capacityPerWagon,
                    "Capacity per wagon must be positive.");
            }

            Id = id;
            TypeId = typeId;
            MaxWagons = maxWagons;
            CapacityPerWagon = capacityPerWagon;
            WagonCount = 0;
            CargoCapacity = 0;
            Cargo = new CargoInventory();
            _orders = new List<RailVehicleOrder>();
            _ordersView = _orders.AsReadOnly();
            _orderStationIds = new List<int>();
            _orderStationIdsView = _orderStationIds.AsReadOnly();
            _allowedLoadCargoIds = new List<string>();
            _allowedLoadCargoIdsView = _allowedLoadCargoIds.AsReadOnly();
            _allowedLoadCargoDirty = true;
            OrderIndex = 0;
            IsStuck = false;
            IsOnEdge = false;
            IsServicingStop = false;
            IsUnloadComplete = false;
            CargoUnloadedThisVisit = new CargoInventory();
            EdgeFromNodeId = stationNodeId;
            EdgeToNodeId = stationNodeId;
            ProgressAlongEdge = FixedPoint.Zero;
            LifetimeDeliveryIncome = 0L;
            PassengerCargoOriginTownId = PassengerCargo.NoOriginTownId;
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
        }

        /// <summary>
        /// Restores a vehicle from persisted fields (save load / tests).
        /// </summary>
        public RailVehicle(
            int id,
            string typeId,
            IReadOnlyList<RailVehicleOrder> orders,
            int orderIndex,
            bool isStuck,
            bool isOnEdge,
            int edgeFromNodeId,
            int edgeToNodeId,
            FixedPoint progressAlongEdge,
            IReadOnlyList<CargoAmount>? cargo = null,
            int wagonCount = 0,
            int cargoCapacity = -1,
            bool isServicingStop = false,
            bool isUnloadComplete = false,
            long lifetimeDeliveryIncome = 0L,
            int passengerCargoOriginTownId = PassengerCargo.NoOriginTownId,
            int maxWagons = LegacyMaxWagons,
            int capacityPerWagon = LegacyCapacityPerWagon)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Vehicle id cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Type id cannot be null or whitespace.", nameof(typeId));
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }
#pragma warning restore CA1510

            if (maxWagons < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWagons), maxWagons, "Max wagons must be positive.");
            }

            if (capacityPerWagon < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacityPerWagon),
                    capacityPerWagon,
                    "Capacity per wagon must be positive.");
            }

            if (wagonCount < 0 || wagonCount > maxWagons)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    $"Wagon count must be between 0 and {maxWagons}.");
            }

            int resolvedCapacity = cargoCapacity < 0 ? wagonCount * capacityPerWagon : cargoCapacity;
            if (resolvedCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cargoCapacity),
                    cargoCapacity,
                    "Cargo capacity cannot be negative.");
            }

            if (resolvedCapacity != wagonCount * capacityPerWagon)
            {
                throw new ArgumentException(
                    "Cargo capacity must equal wagon count times capacity per wagon.",
                    nameof(cargoCapacity));
            }

            if (orderIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(orderIndex), orderIndex, "Order index cannot be negative.");
            }

            if (orders.Count > 0 && orderIndex >= orders.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(orderIndex), orderIndex, "Order index is out of range.");
            }

            if (edgeFromNodeId < 0 || edgeToNodeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeFromNodeId), "Edge node ids cannot be negative.");
            }

            if (progressAlongEdge < FixedPoint.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(progressAlongEdge), "Progress cannot be negative.");
            }

            if (lifetimeDeliveryIncome < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetimeDeliveryIncome),
                    lifetimeDeliveryIncome,
                    "Lifetime delivery income cannot be negative.");
            }

            if (passengerCargoOriginTownId < PassengerCargo.NoOriginTownId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(passengerCargoOriginTownId),
                    passengerCargoOriginTownId,
                    "Passenger cargo origin town id cannot be less than -1.");
            }

            Id = id;
            TypeId = typeId;
            MaxWagons = maxWagons;
            CapacityPerWagon = capacityPerWagon;
            WagonCount = wagonCount;
            CargoCapacity = resolvedCapacity;
            Cargo = cargo == null ? new CargoInventory() : new CargoInventory(cargo);
            if (Cargo.TotalAmount > CargoCapacity)
            {
                throw new ArgumentException("Restored cargo exceeds vehicle capacity.", nameof(cargo));
            }

            _orders = new List<RailVehicleOrder>(orders.Count);
            _orderStationIds = new List<int>(orders.Count);
            for (int index = 0; index < orders.Count; index++)
            {
                RailVehicleOrder order = orders[index];
                _orders.Add(order);
                _orderStationIds.Add(order.StationId);
            }

            _ordersView = _orders.AsReadOnly();
            _orderStationIdsView = _orderStationIds.AsReadOnly();
            _allowedLoadCargoIds = new List<string>();
            _allowedLoadCargoIdsView = _allowedLoadCargoIds.AsReadOnly();
            _allowedLoadCargoDirty = true;
            OrderIndex = orders.Count == 0 ? 0 : orderIndex;
            IsStuck = isStuck;
            IsOnEdge = isOnEdge;
            IsServicingStop = isServicingStop;
            IsUnloadComplete = isUnloadComplete;
            CargoUnloadedThisVisit = new CargoInventory();
            EdgeFromNodeId = edgeFromNodeId;
            EdgeToNodeId = edgeToNodeId;
            ProgressAlongEdge = progressAlongEdge;
            LifetimeDeliveryIncome = lifetimeDeliveryIncome;
            PassengerCargoOriginTownId = passengerCargoOriginTownId;
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
        }

        /// <summary>
        /// Compatibility restore from station-id-only orders.
        /// </summary>
        public RailVehicle(
            int id,
            string typeId,
            IReadOnlyList<int> orderStationIds,
            int orderIndex,
            bool isStuck,
            bool isOnEdge,
            int edgeFromNodeId,
            int edgeToNodeId,
            FixedPoint progressAlongEdge)
            : this(
                id,
                typeId,
                ToOrders(orderStationIds),
                orderIndex,
                isStuck,
                isOnEdge,
                edgeFromNodeId,
                edgeToNodeId,
                progressAlongEdge)
        {
        }

        /// <summary>
        /// Gets the stable vehicle id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the vehicle type content id.
        /// </summary>
        public string TypeId { get; }

        /// <summary>
        /// Gets the maximum wagons this locomotive may haul.
        /// </summary>
        public int MaxWagons { get; }

        /// <summary>
        /// Gets cargo capacity contributed by each wagon.
        /// </summary>
        public int CapacityPerWagon { get; }

        /// <summary>
        /// Gets the number of wagons currently attached (0 = locomotive only).
        /// </summary>
        public int WagonCount { get; private set; }

        /// <summary>
        /// Gets the maximum cargo units this vehicle may carry (<see cref="WagonCount"/> × <see cref="CapacityPerWagon"/>).
        /// </summary>
        public int CargoCapacity { get; private set; }

        /// <summary>
        /// Gets the vehicle cargo hold.
        /// </summary>
        public CargoInventory Cargo { get; }

        /// <summary>
        /// Gets the looping order list (station id + wait-for-full).
        /// </summary>
        public IReadOnlyList<RailVehicleOrder> Orders
        {
            get
            {
                return _ordersView;
            }
        }

        /// <summary>
        /// Gets cargo ids this vehicle may load, derived from route acceptances.
        /// Empty until <see cref="VehicleRouteCargoFilter"/> refreshes after orders are set.
        /// </summary>
        public IReadOnlyList<string> AllowedLoadCargoIds
        {
            get
            {
                return _allowedLoadCargoIdsView;
            }
        }

        /// <summary>
        /// Gets whether the allowed-load list must be rebuilt from the current route.
        /// </summary>
        public bool AllowedLoadCargoDirty
        {
            get
            {
                return _allowedLoadCargoDirty;
            }
        }

        /// <summary>
        /// Gets the looping order list of rail-station ids.
        /// </summary>
        public IReadOnlyList<int> OrderStationIds
        {
            get
            {
                return _orderStationIdsView;
            }
        }

        /// <summary>
        /// Gets the index of the current destination stop in <see cref="Orders"/>.
        /// </summary>
        public int OrderIndex { get; private set; }

        /// <summary>
        /// Gets whether the vehicle is stuck because its destination is unreachable.
        /// </summary>
        public bool IsStuck { get; private set; }

        /// <summary>
        /// Gets whether the vehicle is mid-edge (<see langword="true"/>) or parked on a node.
        /// </summary>
        public bool IsOnEdge { get; private set; }

        /// <summary>
        /// Gets whether the vehicle is unloading/loading at the current order stop.
        /// </summary>
        public bool IsServicingStop { get; private set; }

        /// <summary>
        /// Gets whether unload has finished for the current service stop (load phase may continue).
        /// </summary>
        public bool IsUnloadComplete { get; private set; }

        /// <summary>
        /// Gets cargo amounts this vehicle unloaded into the current stop (excluded from reload).
        /// </summary>
        public CargoInventory CargoUnloadedThisVisit { get; private set; }

        /// <summary>
        /// Gets the travel-from node id (current node when not on an edge).
        /// </summary>
        public int EdgeFromNodeId { get; private set; }

        /// <summary>
        /// Gets the travel-to node id (same as from when parked).
        /// </summary>
        public int EdgeToNodeId { get; private set; }

        /// <summary>
        /// Gets progress along the current edge in tile steps (0 at from, length at to).
        /// </summary>
        public FixedPoint ProgressAlongEdge { get; private set; }

        /// <summary>
        /// Gets cumulative company cash credited from this vehicle's accepted deliveries.
        /// </summary>
        public long LifetimeDeliveryIncome { get; private set; }

        /// <summary>
        /// Gets the town id that loaded the current passenger-class cargo, or
        /// <see cref="PassengerCargo.NoOriginTownId"/> when none.
        /// </summary>
        public int PassengerCargoOriginTownId { get; private set; }

        /// <summary>
        /// Gets remaining free cargo capacity.
        /// </summary>
        public int FreeCargoCapacity
        {
            get
            {
                return CargoCapacity - Cargo.TotalAmount;
            }
        }

        internal int CachedGraphVersion { get; set; }

        internal int PathEdgeIndex { get; set; }

        internal RailPath? ActivePath { get; set; }

        /// <summary>
        /// Replaces the order list and resets the destination index to zero.
        /// Clears stuck and servicing state so movement can repath on the next tick.
        /// With fewer than 2 stops the vehicle is idle: if it was mid-edge, it snaps off the
        /// track onto the last graph node so status becomes Parked (not Moving).
        /// </summary>
        internal void SetOrders(IReadOnlyList<RailVehicleOrder> orders)
        {
#pragma warning disable CA1510
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }
#pragma warning restore CA1510

            _orders.Clear();
            _orderStationIds.Clear();
            for (int index = 0; index < orders.Count; index++)
            {
                RailVehicleOrder order = orders[index];
                _orders.Add(order);
                _orderStationIds.Add(order.StationId);
            }

            OrderIndex = 0;
            IsStuck = false;
            IsServicingStop = false;
            IsUnloadComplete = false;
            ActivePath = null;
            PathEdgeIndex = 0;
            CachedGraphVersion = -1;
            _allowedLoadCargoIds.Clear();
            _allowedLoadCargoDirty = true;

            if (_orders.Count < 2 && IsOnEdge)
            {
                ParkAtNode(EdgeFromNodeId);
            }
        }

        /// <summary>
        /// Replaces orders from station ids only (wait-for-full defaults to false).
        /// </summary>
        internal void SetOrders(IReadOnlyList<int> stationIds)
        {
            SetOrders(ToOrders(stationIds));
        }

        /// <summary>
        /// Replaces the allowed-load cargo list (sorted, no duplicates) and clears the dirty flag.
        /// </summary>
        internal void SetAllowedLoadCargoIds(IReadOnlyList<string> cargoIds)
        {
#pragma warning disable CA1510
            if (cargoIds == null)
            {
                throw new ArgumentNullException(nameof(cargoIds));
            }
#pragma warning restore CA1510

            _allowedLoadCargoIds.Clear();
            for (int index = 0; index < cargoIds.Count; index++)
            {
                string cargoId = cargoIds[index];
                if (string.IsNullOrWhiteSpace(cargoId))
                {
                    throw new ArgumentException("Allowed load cargo ids cannot be null or whitespace.", nameof(cargoIds));
                }

                _allowedLoadCargoIds.Add(cargoId);
            }

            _allowedLoadCargoDirty = false;
        }

        /// <summary>
        /// Marks the allowed-load list stale so the next filter refresh rebuilds it.
        /// </summary>
        internal void MarkAllowedLoadCargoDirty()
        {
            _allowedLoadCargoDirty = true;
        }

        /// <summary>
        /// Returns whether <paramref name="cargoId"/> is on the current allowed-load list.
        /// </summary>
        public bool AllowsLoadCargo(string cargoId)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                return false;
            }

            for (int index = 0; index < _allowedLoadCargoIds.Count; index++)
            {
                if (string.Equals(_allowedLoadCargoIds[index], cargoId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal void SetStuck(bool stuck)
        {
            IsStuck = stuck;
            if (stuck)
            {
                ActivePath = null;
                PathEdgeIndex = 0;
            }
        }

        /// <summary>
        /// Attaches one wagon when below <see cref="MaxWagons"/>. Returns false at the limit.
        /// </summary>
        internal bool TryAddWagon()
        {
            if (WagonCount >= MaxWagons)
            {
                return false;
            }

            WagonCount++;
            CargoCapacity = WagonCount * CapacityPerWagon;
            return true;
        }

        /// <summary>
        /// Detaches one wagon when any remain. Excess cargo above the new capacity is discarded.
        /// A locomotive that loses its last wagon parks on its current from-node if it was mid-edge.
        /// </summary>
        internal bool TrySellWagon()
        {
            if (WagonCount <= 0)
            {
                return false;
            }

            WagonCount--;
            CargoCapacity = WagonCount * CapacityPerWagon;
            Cargo.TrimToMaxTotal(CargoCapacity);
            if (WagonCount == 0)
            {
                if (IsOnEdge)
                {
                    ParkAtNode(EdgeFromNodeId);
                }

                IsServicingStop = false;
                IsUnloadComplete = false;
                CargoUnloadedThisVisit.Clear();
                ActivePath = null;
                PathEdgeIndex = 0;
                CachedGraphVersion = -1;
            }

            return true;
        }

        /// <summary>
        /// Adds accepted delivery income attributed to this vehicle.
        /// </summary>
        internal void CreditLifetimeDeliveryIncome(long amount)
        {
            if (amount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Income amount cannot be negative.");
            }

            if (amount == 0L)
            {
                return;
            }

            LifetimeDeliveryIncome = checked(LifetimeDeliveryIncome + amount);
        }

        /// <summary>
        /// Sets or clears the passenger-class cargo origin town id.
        /// </summary>
        internal void SetPassengerCargoOriginTownId(int townId)
        {
            if (townId < PassengerCargo.NoOriginTownId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(townId),
                    townId,
                    "Passenger cargo origin town id cannot be less than -1.");
            }

            PassengerCargoOriginTownId = townId;
        }

        internal void BeginServicingStop()
        {
            IsServicingStop = true;
            IsUnloadComplete = false;
            CargoUnloadedThisVisit.Clear();
            ActivePath = null;
            PathEdgeIndex = 0;
        }

        internal void MarkUnloadComplete()
        {
            IsUnloadComplete = true;
        }

        internal void FinishServicingStop()
        {
            IsServicingStop = false;
            IsUnloadComplete = false;
            CargoUnloadedThisVisit.Clear();
        }

        internal void ParkAtNode(int nodeId)
        {
            IsOnEdge = false;
            EdgeFromNodeId = nodeId;
            EdgeToNodeId = nodeId;
            ProgressAlongEdge = FixedPoint.Zero;
        }

        internal void BeginEdge(int fromNodeId, int toNodeId)
        {
            IsOnEdge = true;
            EdgeFromNodeId = fromNodeId;
            EdgeToNodeId = toNodeId;
            ProgressAlongEdge = FixedPoint.Zero;
        }

        internal void SetProgress(FixedPoint progress)
        {
            if (progress < FixedPoint.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress cannot be negative.");
            }

            ProgressAlongEdge = progress;
        }

        internal void AdvanceOrderIndex()
        {
            if (_orders.Count == 0)
            {
                OrderIndex = 0;
                return;
            }

            OrderIndex = (OrderIndex + 1) % _orders.Count;
        }

        /// <summary>
        /// Returns the current destination station id when orders exist.
        /// </summary>
        public bool TryGetCurrentDestinationStationId(out int stationId)
        {
            if (_orders.Count < 2)
            {
                stationId = 0;
                return false;
            }

            stationId = _orders[OrderIndex].StationId;
            return true;
        }

        /// <summary>
        /// Returns the current order stop when the vehicle has a looping route.
        /// </summary>
        public bool TryGetCurrentOrder(out RailVehicleOrder order)
        {
            if (_orders.Count < 2)
            {
                order = default;
                return false;
            }

            order = _orders[OrderIndex];
            return true;
        }

        private static RailVehicleOrder[] ToOrders(IReadOnlyList<int> stationIds)
        {
#pragma warning disable CA1510
            if (stationIds == null)
            {
                throw new ArgumentNullException(nameof(stationIds));
            }
#pragma warning restore CA1510

            var orders = new RailVehicleOrder[stationIds.Count];
            for (int index = 0; index < stationIds.Count; index++)
            {
                orders[index] = new RailVehicleOrder(stationIds[index], waitForFull: false);
            }

            return orders;
        }
    }
}
