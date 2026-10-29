#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Replaces a road vehicle's looping station-order list.
    /// </summary>
    public sealed class SetRoadVehicleOrdersCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _vehicleId;
        private readonly RoadVehicleOrder[] _orders;
        private readonly ContentCatalog? _catalog;

        /// <summary>
        /// Initializes a set-orders command from station ids (wait-for-full defaults to false).
        /// </summary>
        /// <param name="vehicleId">The vehicle to update.</param>
        /// <param name="stationIds">Ordered station ids (may be empty or a single stop for idle).</param>
        /// <param name="catalog">
        /// When provided, rebuilds the vehicle's allowed-load cargo list from the new route immediately.
        /// </param>
        public SetRoadVehicleOrdersCommand(
            int vehicleId,
            IReadOnlyList<int> stationIds,
            ContentCatalog? catalog = null)
        {
#pragma warning disable CA1510
            if (stationIds == null)
            {
                throw new ArgumentNullException(nameof(stationIds));
            }
#pragma warning restore CA1510

            _vehicleId = vehicleId;
            _catalog = catalog;
            _orders = new RoadVehicleOrder[stationIds.Count];
            for (int index = 0; index < stationIds.Count; index++)
            {
                _orders[index] = new RoadVehicleOrder(stationIds[index], waitForFull: false);
            }
        }

        /// <summary>
        /// Initializes a set-orders command with per-stop wait-for-full flags.
        /// </summary>
        /// <param name="catalog">
        /// When provided, rebuilds the vehicle's allowed-load cargo list from the new route immediately.
        /// </param>
        public SetRoadVehicleOrdersCommand(
            int vehicleId,
            IReadOnlyList<RoadVehicleOrder> orders,
            ContentCatalog? catalog = null)
        {
#pragma warning disable CA1510
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }
#pragma warning restore CA1510

            _vehicleId = vehicleId;
            _catalog = catalog;
            _orders = new RoadVehicleOrder[orders.Count];
            for (int index = 0; index < orders.Count; index++)
            {
                _orders[index] = orders[index];
            }
        }

        /// <summary>
        /// Gets the target vehicle id.
        /// </summary>
        public int VehicleId
        {
            get
            {
                return _vehicleId;
            }
        }

        /// <summary>
        /// Gets a copy of the ordered stops.
        /// </summary>
        public IReadOnlyList<RoadVehicleOrder> Orders
        {
            get
            {
                return _orders;
            }
        }

        /// <summary>
        /// Gets station ids only (convenience for callers that ignore wait-for-full).
        /// </summary>
        public IReadOnlyList<int> StationIds
        {
            get
            {
                var stationIds = new int[_orders.Length];
                for (int index = 0; index < _orders.Length; index++)
                {
                    stationIds[index] = _orders[index].StationId;
                }

                return stationIds;
            }
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRoadVehicle(_vehicleId, out _))
            {
                return CommandValidationResult.Rejected("Road vehicle does not exist.");
            }

            for (int index = 0; index < _orders.Length; index++)
            {
                int stationId = _orders[index].StationId;
                if (stationId < 0)
                {
                    return CommandValidationResult.Rejected("Order station ids cannot be negative.");
                }

                if (!state.TryGetRoadStation(stationId, out _))
                {
                    return CommandValidationResult.Rejected($"Order references missing road station {stationId}.");
                }
            }

            return CommandValidationResult.Valid;
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRoadVehicle(_vehicleId, out RoadVehicle? vehicle) || vehicle == null)
            {
                throw new InvalidOperationException($"Road vehicle {_vehicleId} does not exist.");
            }

            vehicle.SetOrders(_orders);
            StartAtStandingStation(state, vehicle);
            if (_catalog != null)
            {
                VehicleRouteCargoFilter.RefreshRoad(state, vehicle, _catalog);
            }
        }

        // A vehicle standing on a station of its new route services that stop first, so it loads before leaving
        // instead of driving off empty to the first order (Issue #193 preview feedback).
        private static void StartAtStandingStation(GeneratedWorld state, RoadVehicle vehicle)
        {
            if (vehicle.IsOnEdge || vehicle.Orders.Count < 2)
            {
                return;
            }

            for (int index = 0; index < vehicle.Orders.Count; index++)
            {
                if (state.TryGetRoadStation(vehicle.Orders[index].StationId, out RoadStation? station) &&
                    station != null &&
                    state.RoadGraph.TryGetNodeId(station.Coordinate, out int nodeId) &&
                    nodeId == vehicle.EdgeFromNodeId)
                {
                    vehicle.StartAtOrder(index);
                    return;
                }
            }
        }
    }
}
