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
        /// Placeholder type id until a data-driven vehicle catalog lands (Epic #11 / Issue #145).
        /// </summary>
        public const int PlaceholderTypeId = 0;

        /// <summary>
        /// Maximum wagons the placeholder locomotive may haul (catalog will own this later).
        /// </summary>
        public const int MaxWagons = 3;

        /// <summary>
        /// Cargo capacity contributed by each wagon.
        /// </summary>
        public const int CapacityPerWagon = 10;

        /// <summary>
        /// Full-consist placeholder capacity (<see cref="MaxWagons"/> × <see cref="CapacityPerWagon"/>).
        /// Used when migrating older saves that assumed a fixed three-wagon train.
        /// </summary>
        public const int PlaceholderCargoCapacity = MaxWagons * CapacityPerWagon;

        private readonly List<RailVehicleOrder> _orders;
        private readonly ReadOnlyCollection<RailVehicleOrder> _ordersView;
        private readonly List<int> _orderStationIds;
        private readonly ReadOnlyCollection<int> _orderStationIdsView;

        /// <summary>
        /// Initializes a vehicle parked at a station node with no orders.
        /// </summary>
        /// <param name="id">Stable non-negative vehicle id.</param>
        /// <param name="typeId">Vehicle type id (use <see cref="PlaceholderTypeId"/> in V1).</param>
        /// <param name="stationNodeId">Graph node id of the spawn station (tile id).</param>
        public RailVehicle(int id, int typeId, int stationNodeId)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Vehicle id cannot be negative.");
            }

            if (typeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Type id cannot be negative.");
            }

            if (stationNodeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stationNodeId), stationNodeId, "Station node id cannot be negative.");
            }

            Id = id;
            TypeId = typeId;
            WagonCount = 0;
            CargoCapacity = 0;
            Cargo = new CargoInventory();
            _orders = new List<RailVehicleOrder>();
            _ordersView = _orders.AsReadOnly();
            _orderStationIds = new List<int>();
            _orderStationIdsView = _orderStationIds.AsReadOnly();
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
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
        }

        /// <summary>
        /// Restores a vehicle from persisted fields (save load / tests).
        /// </summary>
        public RailVehicle(
            int id,
            int typeId,
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
            long lifetimeDeliveryIncome = 0L)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Vehicle id cannot be negative.");
            }

            if (typeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Type id cannot be negative.");
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }
#pragma warning restore CA1510

            if (wagonCount < 0 || wagonCount > MaxWagons)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    $"Wagon count must be between 0 and {MaxWagons}.");
            }

            int resolvedCapacity = cargoCapacity < 0 ? wagonCount * CapacityPerWagon : cargoCapacity;
            if (resolvedCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cargoCapacity),
                    cargoCapacity,
                    "Cargo capacity cannot be negative.");
            }

            if (resolvedCapacity != wagonCount * CapacityPerWagon)
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

            Id = id;
            TypeId = typeId;
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
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
        }

        /// <summary>
        /// Compatibility restore from station-id-only orders.
        /// </summary>
        public RailVehicle(
            int id,
            int typeId,
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
        /// Gets the vehicle type id.
        /// </summary>
        public int TypeId { get; }

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
