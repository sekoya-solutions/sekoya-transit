#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A generated tile map and its ordered town and industry anchors.
    /// </summary>
    public sealed class GeneratedWorld
    {
        private readonly ReadOnlyCollection<TileCoordinate> _townAnchors;
        private readonly ReadOnlyCollection<PlacedIndustry> _industries;
        private readonly List<RoadStation> _roadStations;
        private readonly ReadOnlyCollection<RoadStation> _roadStationsView;
        private readonly List<RoadVehicle> _roadVehicles;
        private readonly ReadOnlyCollection<RoadVehicle> _roadVehiclesView;
        private int _nextRoadStationId;
        private int _nextRoadVehicleId;

        internal GeneratedWorld(
            TileMap map,
            TileCoordinate[] townAnchors,
            PlacedIndustry[] industries)
        {
            Map = map;
            _townAnchors = Array.AsReadOnly((TileCoordinate[])townAnchors.Clone());
            _industries = Array.AsReadOnly((PlacedIndustry[])industries.Clone());
            _roadStations = new List<RoadStation>();
            _roadStationsView = _roadStations.AsReadOnly();
            _roadVehicles = new List<RoadVehicle>();
            _roadVehiclesView = _roadVehicles.AsReadOnly();
            RoadGraph = new RoadGraph();
            RoadRouteCache = new RoadRouteCache();
            RoadGraph.RebuildFromMap(map);
        }

        /// <summary>
        /// Gets the generated map. Terrain is flat, and unowned/unoccupied tile sentinels are preserved.
        /// </summary>
        public TileMap Map { get; }

        /// <summary>
        /// Gets the compressed road network derived from <see cref="Map"/> road and station tiles.
        /// </summary>
        public RoadGraph RoadGraph { get; }

        /// <summary>
        /// Gets the version-stamped route cache shared by vehicle pathfinding.
        /// </summary>
        public RoadRouteCache RoadRouteCache { get; }

        /// <summary>
        /// Gets town anchors in deterministic placement order. Their object IDs are their zero-based indices.
        /// </summary>
        public IReadOnlyList<TileCoordinate> TownAnchors
        {
            get
            {
                return _townAnchors;
            }
        }

        /// <summary>
        /// Gets industries in deterministic level, industry-ID, and instance order.
        /// Their object IDs follow the town IDs in this list's order.
        /// </summary>
        public IReadOnlyList<PlacedIndustry> Industries
        {
            get
            {
                return _industries;
            }
        }

        /// <summary>
        /// Gets player-built road stations in ascending id order.
        /// </summary>
        public IReadOnlyList<RoadStation> RoadStations
        {
            get
            {
                return _roadStationsView;
            }
        }

        /// <summary>
        /// Gets player-owned road vehicles in ascending id order.
        /// </summary>
        public IReadOnlyList<RoadVehicle> RoadVehicles
        {
            get
            {
                return _roadVehiclesView;
            }
        }

        /// <summary>
        /// Gets the map object ID assigned to a town by its placement-order index.
        /// </summary>
        public int GetTownObjectId(int townIndex)
        {
            if (townIndex < 0 || townIndex >= _townAnchors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(townIndex));
            }

            return townIndex;
        }

        /// <summary>
        /// Gets the map object ID assigned to an industry by its placement-order index.
        /// </summary>
        public int GetIndustryObjectId(int industryIndex)
        {
            if (industryIndex < 0 || industryIndex >= _industries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(industryIndex));
            }

            return checked(_townAnchors.Count + industryIndex);
        }

        /// <summary>
        /// Returns the station occupying the tile when one exists.
        /// </summary>
        public bool TryGetRoadStationAt(TileCoordinate coordinate, out RoadStation station)
        {
            int encoded = Map.GetStationId(coordinate);
            if (encoded == TileMap.NoStationId)
            {
                station = default;
                return false;
            }

            int stationId = checked(encoded - 1);
            return TryGetRoadStation(stationId, out station);
        }

        /// <summary>
        /// Returns the station with the given id when it exists.
        /// </summary>
        public bool TryGetRoadStation(int stationId, out RoadStation station)
        {
            for (int index = 0; index < _roadStations.Count; index++)
            {
                if (_roadStations[index].Id == stationId)
                {
                    station = _roadStations[index];
                    return true;
                }
            }

            station = default;
            return false;
        }

        /// <summary>
        /// Returns the vehicle with the given id when it exists.
        /// </summary>
        public bool TryGetRoadVehicle(int vehicleId, out RoadVehicle? vehicle)
        {
            for (int index = 0; index < _roadVehicles.Count; index++)
            {
                if (_roadVehicles[index].Id == vehicleId)
                {
                    vehicle = _roadVehicles[index];
                    return true;
                }
            }

            vehicle = null;
            return false;
        }

        /// <summary>
        /// Places a road station on an already-validated empty tile and rebuilds the road graph.
        /// </summary>
        internal RoadStation AddRoadStation(TileCoordinate coordinate)
        {
            int id = _nextRoadStationId;
            _nextRoadStationId = checked(_nextRoadStationId + 1);
            var station = new RoadStation(id, coordinate);
            _roadStations.Add(station);
            Map.SetStationId(coordinate, checked(id + 1));
            RoadGraph.RebuildFromMap(Map);
            return station;
        }

        /// <summary>
        /// Removes the road station on the tile when present and rebuilds the road graph.
        /// </summary>
        internal bool RemoveRoadStationAt(TileCoordinate coordinate)
        {
            if (!TryGetRoadStationAt(coordinate, out RoadStation station))
            {
                return false;
            }

            for (int index = 0; index < _roadStations.Count; index++)
            {
                if (_roadStations[index].Id == station.Id)
                {
                    _roadStations.RemoveAt(index);
                    break;
                }
            }

            Map.SetStationId(coordinate, TileMap.NoStationId);
            RoadGraph.RebuildFromMap(Map);
            return true;
        }

        /// <summary>
        /// Spawns a placeholder road vehicle parked at an existing station.
        /// </summary>
        internal RoadVehicle AddRoadVehicle(int stationId)
        {
            if (!TryGetRoadStation(stationId, out RoadStation station))
            {
                throw new InvalidOperationException($"Road station {stationId} does not exist.");
            }

            if (!RoadGraph.TryGetNodeId(station.Coordinate, out int nodeId))
            {
                throw new InvalidOperationException($"Road station {stationId} is not a graph node.");
            }

            int id = _nextRoadVehicleId;
            _nextRoadVehicleId = checked(_nextRoadVehicleId + 1);
            var vehicle = new RoadVehicle(id, RoadVehicle.PlaceholderTypeId, nodeId);
            InsertVehicleSorted(vehicle);
            return vehicle;
        }

        /// <summary>
        /// Inserts a restored vehicle and advances the next-id counter past its id.
        /// </summary>
        internal void RestoreRoadVehicle(RoadVehicle vehicle)
        {
#pragma warning disable CA1510
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            InsertVehicleSorted(vehicle);
            if (vehicle.Id >= _nextRoadVehicleId)
            {
                _nextRoadVehicleId = checked(vehicle.Id + 1);
            }
        }

        /// <summary>
        /// Removes a vehicle by id when present.
        /// </summary>
        internal bool RemoveRoadVehicle(int vehicleId)
        {
            for (int index = 0; index < _roadVehicles.Count; index++)
            {
                if (_roadVehicles[index].Id == vehicleId)
                {
                    _roadVehicles.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        private void InsertVehicleSorted(RoadVehicle vehicle)
        {
            int insertAt = _roadVehicles.Count;
            for (int index = 0; index < _roadVehicles.Count; index++)
            {
                if (_roadVehicles[index].Id == vehicle.Id)
                {
                    throw new ArgumentException($"Duplicate road vehicle id {vehicle.Id}.", nameof(vehicle));
                }

                if (_roadVehicles[index].Id > vehicle.Id)
                {
                    insertAt = index;
                    break;
                }
            }

            _roadVehicles.Insert(insertAt, vehicle);
        }
    }
}
