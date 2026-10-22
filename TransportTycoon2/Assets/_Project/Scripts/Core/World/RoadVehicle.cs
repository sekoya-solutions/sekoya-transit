#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A player-owned road vehicle that follows looping station orders along the road graph.
    /// </summary>
    public sealed class RoadVehicle
    {
        /// <summary>
        /// Placeholder type id until a data-driven vehicle catalog lands.
        /// </summary>
        public const int PlaceholderTypeId = 0;

        /// <summary>
        /// Placeholder cargo capacity in cargo units.
        /// </summary>
        public const int PlaceholderCargoCapacity = 10;

        private readonly List<RoadVehicleOrder> _orders;
        private readonly ReadOnlyCollection<RoadVehicleOrder> _ordersView;
        private readonly List<int> _orderStationIds;
        private readonly ReadOnlyCollection<int> _orderStationIdsView;

        /// <summary>
        /// Initializes a vehicle parked at a station node with no orders.
        /// </summary>
        /// <param name="id">Stable non-negative vehicle id.</param>
        /// <param name="typeId">Vehicle type id (use <see cref="PlaceholderTypeId"/> in V1).</param>
        /// <param name="stationNodeId">Graph node id of the spawn station (tile id).</param>
        public RoadVehicle(int id, int typeId, int stationNodeId)
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
            CargoCapacity = PlaceholderCargoCapacity;
            Cargo = new CargoInventory();
            _orders = new List<RoadVehicleOrder>();
            _ordersView = _orders.AsReadOnly();
            _orderStationIds = new List<int>();
            _orderStationIdsView = _orderStationIds.AsReadOnly();
            OrderIndex = 0;
            IsStuck = false;
            IsOnEdge = false;
            IsServicingStop = false;
            IsUnloadComplete = false;
            EdgeFromNodeId = stationNodeId;
            EdgeToNodeId = stationNodeId;
            ProgressAlongEdge = FixedPoint.Zero;
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
        }

        /// <summary>
        /// Restores a vehicle from persisted fields (save load / tests).
        /// </summary>
        public RoadVehicle(
            int id,
            int typeId,
            IReadOnlyList<RoadVehicleOrder> orders,
            int orderIndex,
            bool isStuck,
            bool isOnEdge,
            int edgeFromNodeId,
            int edgeToNodeId,
            FixedPoint progressAlongEdge,
            IReadOnlyList<CargoAmount>? cargo = null,
            int cargoCapacity = PlaceholderCargoCapacity,
            bool isServicingStop = false,
            bool isUnloadComplete = false)
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

            if (cargoCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoCapacity), cargoCapacity, "Cargo capacity must be positive.");
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

            Id = id;
            TypeId = typeId;
            CargoCapacity = cargoCapacity;
            Cargo = cargo == null ? new CargoInventory() : new CargoInventory(cargo);
            if (Cargo.TotalAmount > CargoCapacity)
            {
                throw new ArgumentException("Restored cargo exceeds vehicle capacity.", nameof(cargo));
            }

            _orders = new List<RoadVehicleOrder>(orders.Count);
            _orderStationIds = new List<int>(orders.Count);
            for (int index = 0; index < orders.Count; index++)
            {
                RoadVehicleOrder order = orders[index];
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
            EdgeFromNodeId = edgeFromNodeId;
            EdgeToNodeId = edgeToNodeId;
            ProgressAlongEdge = progressAlongEdge;
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
        }

        /// <summary>
        /// Compatibility restore from station-id-only orders (format v4 / older tests).
        /// </summary>
        public RoadVehicle(
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
        /// Gets the maximum cargo units this vehicle may carry.
        /// </summary>
        public int CargoCapacity { get; }

        /// <summary>
        /// Gets the vehicle cargo hold.
        /// </summary>
        public CargoInventory Cargo { get; }

        /// <summary>
        /// Gets the looping order list (station id + wait-for-full).
        /// </summary>
        public IReadOnlyList<RoadVehicleOrder> Orders
        {
            get
            {
                return _ordersView;
            }
        }

        /// <summary>
        /// Gets the looping order list of road-station ids.
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

        internal RoadPath? ActivePath { get; set; }

        /// <summary>
        /// Replaces the order list and resets the destination index to zero.
        /// Clears stuck and servicing state so movement can repath on the next tick.
        /// </summary>
        internal void SetOrders(IReadOnlyList<RoadVehicleOrder> orders)
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
                RoadVehicleOrder order = orders[index];
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

        internal void BeginServicingStop()
        {
            IsServicingStop = true;
            IsUnloadComplete = false;
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
        public bool TryGetCurrentOrder(out RoadVehicleOrder order)
        {
            if (_orders.Count < 2)
            {
                order = default;
                return false;
            }

            order = _orders[OrderIndex];
            return true;
        }

        private static RoadVehicleOrder[] ToOrders(IReadOnlyList<int> stationIds)
        {
#pragma warning disable CA1510
            if (stationIds == null)
            {
                throw new ArgumentNullException(nameof(stationIds));
            }
#pragma warning restore CA1510

            var orders = new RoadVehicleOrder[stationIds.Count];
            for (int index = 0; index < stationIds.Count; index++)
            {
                orders[index] = new RoadVehicleOrder(stationIds[index], waitForFull: false);
            }

            return orders;
        }
    }
}
