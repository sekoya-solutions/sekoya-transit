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
            _orderStationIds = new List<int>();
            _orderStationIdsView = _orderStationIds.AsReadOnly();
            OrderIndex = 0;
            IsStuck = false;
            IsOnEdge = false;
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
            IReadOnlyList<int> orderStationIds,
            int orderIndex,
            bool isStuck,
            bool isOnEdge,
            int edgeFromNodeId,
            int edgeToNodeId,
            FixedPoint progressAlongEdge)
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
            if (orderStationIds == null)
            {
                throw new ArgumentNullException(nameof(orderStationIds));
            }
#pragma warning restore CA1510

            if (orderIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(orderIndex), orderIndex, "Order index cannot be negative.");
            }

            if (orderStationIds.Count > 0 && orderIndex >= orderStationIds.Count)
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
            _orderStationIds = new List<int>(orderStationIds.Count);
            for (int index = 0; index < orderStationIds.Count; index++)
            {
                int stationId = orderStationIds[index];
                if (stationId < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(orderStationIds), "Order station ids cannot be negative.");
                }

                _orderStationIds.Add(stationId);
            }

            _orderStationIdsView = _orderStationIds.AsReadOnly();
            OrderIndex = orderStationIds.Count == 0 ? 0 : orderIndex;
            IsStuck = isStuck;
            IsOnEdge = isOnEdge;
            EdgeFromNodeId = edgeFromNodeId;
            EdgeToNodeId = edgeToNodeId;
            ProgressAlongEdge = progressAlongEdge;
            CachedGraphVersion = -1;
            PathEdgeIndex = 0;
            ActivePath = null;
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
        /// Gets the index of the current destination stop in <see cref="OrderStationIds"/>.
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

        internal int CachedGraphVersion { get; set; }

        internal int PathEdgeIndex { get; set; }

        internal RoadPath? ActivePath { get; set; }

        /// <summary>
        /// Replaces the order list and resets the destination index to zero.
        /// Clears stuck state so movement can repath on the next tick.
        /// </summary>
        internal void SetOrders(IReadOnlyList<int> stationIds)
        {
#pragma warning disable CA1510
            if (stationIds == null)
            {
                throw new ArgumentNullException(nameof(stationIds));
            }
#pragma warning restore CA1510

            _orderStationIds.Clear();
            for (int index = 0; index < stationIds.Count; index++)
            {
                int stationId = stationIds[index];
                if (stationId < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(stationIds), "Order station ids cannot be negative.");
                }

                _orderStationIds.Add(stationId);
            }

            OrderIndex = 0;
            IsStuck = false;
            ActivePath = null;
            PathEdgeIndex = 0;
            CachedGraphVersion = -1;
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
            if (_orderStationIds.Count == 0)
            {
                OrderIndex = 0;
                return;
            }

            OrderIndex = (OrderIndex + 1) % _orderStationIds.Count;
        }

        /// <summary>
        /// Returns the current destination station id when orders exist.
        /// </summary>
        public bool TryGetCurrentDestinationStationId(out int stationId)
        {
            if (_orderStationIds.Count < 2)
            {
                stationId = 0;
                return false;
            }

            stationId = _orderStationIds[OrderIndex];
            return true;
        }
    }
}
