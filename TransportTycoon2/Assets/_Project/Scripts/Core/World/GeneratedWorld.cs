#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A generated tile map and its ordered town and industry anchors.
    /// </summary>
    public sealed class GeneratedWorld
    {
        private readonly List<Town> _towns;
        private readonly ReadOnlyCollection<Town> _townsView;
        private readonly ReadOnlyCollection<TileCoordinate> _townAnchors;
        private readonly ReadOnlyCollection<PlacedIndustry> _industries;
        private readonly List<RoadStation> _roadStations;
        private readonly ReadOnlyCollection<RoadStation> _roadStationsView;
        private readonly List<RoadVehicle> _roadVehicles;
        private readonly ReadOnlyCollection<RoadVehicle> _roadVehiclesView;
        private readonly List<RailStation> _railStations;
        private readonly ReadOnlyCollection<RailStation> _railStationsView;
        private readonly List<RailVehicle> _railVehicles;
        private readonly ReadOnlyCollection<RailVehicle> _railVehiclesView;
        private int _nextRoadStationId;
        private int _nextRoadVehicleId;
        private int _nextRailStationId;
        private int _nextRailVehicleId;

        /// <summary>
        /// Placeholder starting company cash for a new world.
        /// </summary>
        public const long PlaceholderStartingCash = 100000L;

        /// <summary>
        /// Population assigned to towns restored from pre-v13 saves (empty needs until reassigned).
        /// </summary>
        public const int MigratedTownPopulation = 100;

        internal GeneratedWorld(
            TileMap map,
            Town[] towns,
            PlacedIndustry[] industries,
            ulong mapSeed,
            long companyCash = PlaceholderStartingCash)
        {
#pragma warning disable CA1510
            if (towns is null)
            {
                throw new ArgumentNullException(nameof(towns));
            }
#pragma warning restore CA1510

            Map = map;
            MapSeed = mapSeed;
            _towns = CopyTowns(towns);
            _townsView = _towns.AsReadOnly();
            var townAnchors = new TileCoordinate[_towns.Count];
            for (int index = 0; index < _towns.Count; index++)
            {
                townAnchors[index] = _towns[index].Coordinate;
            }

            _townAnchors = Array.AsReadOnly(townAnchors);
            _industries = Array.AsReadOnly(CopyIndustries(industries));
            _roadStations = new List<RoadStation>();
            _roadStationsView = _roadStations.AsReadOnly();
            _roadVehicles = new List<RoadVehicle>();
            _roadVehiclesView = _roadVehicles.AsReadOnly();
            _railStations = new List<RailStation>();
            _railStationsView = _railStations.AsReadOnly();
            _railVehicles = new List<RailVehicle>();
            _railVehiclesView = _railVehicles.AsReadOnly();
            RoadGraph = new RoadGraph();
            RoadRouteCache = new RoadRouteCache();
            RoadGraph.RebuildFromMap(map);
            RailGraph = new RailGraph();
            RailRouteCache = new RailRouteCache();
            RailGraph.RebuildFromMap(map);
            if (companyCash < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(companyCash), companyCash, "Company cash cannot be negative.");
            }

            CompanyCash = companyCash;
        }

        /// <summary>
        /// Gets the world seed used for deterministic town needs (and generation).
        /// </summary>
        public ulong MapSeed { get; }

        /// <summary>
        /// Gets the player's company cash balance.
        /// </summary>
        public long CompanyCash { get; private set; }

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
        /// Gets the compressed rail network derived from <see cref="Map"/> rail and rail-station tiles.
        /// </summary>
        public RailGraph RailGraph { get; }

        /// <summary>
        /// Gets the version-stamped route cache shared by rail pathfinding.
        /// </summary>
        public RailRouteCache RailRouteCache { get; }

        /// <summary>
        /// Gets towns in deterministic placement order. Their object IDs are their zero-based indices.
        /// </summary>
        public IReadOnlyList<Town> Towns
        {
            get
            {
                return _townsView;
            }
        }

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
        /// Sets a town's population and refreshes needs from the tier schedule (Issue #57 / #58).
        /// </summary>
        public void SetTownPopulation(
            int townIndex,
            int population,
            TownNeedsSettings settings,
            ContentCatalog catalog)
        {
            if (townIndex < 0 || townIndex >= _towns.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(townIndex));
            }

            TownNeedsAssignment.SetPopulation(_towns[townIndex], population, settings, catalog, MapSeed);
            MarkAllVehicleAllowedLoadsDirty();
        }

        /// <summary>
        /// Marks every vehicle's allowed-load list stale (town needs or route acceptances changed).
        /// </summary>
        public void MarkAllVehicleAllowedLoadsDirty()
        {
            for (int index = 0; index < _roadVehicles.Count; index++)
            {
                _roadVehicles[index].MarkAllowedLoadCargoDirty();
            }

            for (int index = 0; index < _railVehicles.Count; index++)
            {
                _railVehicles[index].MarkAllowedLoadCargoDirty();
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
        /// Gets player-built rail stations in ascending id order.
        /// </summary>
        public IReadOnlyList<RailStation> RailStations
        {
            get
            {
                return _railStationsView;
            }
        }

        /// <summary>
        /// Gets player-owned rail vehicles in ascending id order.
        /// </summary>
        public IReadOnlyList<RailVehicle> RailVehicles
        {
            get
            {
                return _railVehiclesView;
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
        public bool TryGetRoadStationAt(TileCoordinate coordinate, out RoadStation? station)
        {
            int encoded = Map.GetStationId(coordinate);
            if (encoded == TileMap.NoStationId)
            {
                station = null;
                return false;
            }

            int stationId = checked(encoded - 1);
            return TryGetRoadStation(stationId, out station);
        }

        /// <summary>
        /// Returns the station with the given id when it exists.
        /// </summary>
        public bool TryGetRoadStation(int stationId, out RoadStation? station)
        {
            for (int index = 0; index < _roadStations.Count; index++)
            {
                if (_roadStations[index].Id == stationId)
                {
                    station = _roadStations[index];
                    return true;
                }
            }

            station = null;
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
            if (!TryGetRoadStationAt(coordinate, out RoadStation? station) || station == null)
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
        /// Inserts a restored station and advances the next-id counter past its id.
        /// </summary>
        internal void RestoreRoadStation(RoadStation station)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            for (int index = 0; index < _roadStations.Count; index++)
            {
                if (_roadStations[index].Id == station.Id)
                {
                    throw new ArgumentException($"Duplicate road station id {station.Id}.", nameof(station));
                }
            }

            int insertAt = _roadStations.Count;
            for (int index = 0; index < _roadStations.Count; index++)
            {
                if (_roadStations[index].Id > station.Id)
                {
                    insertAt = index;
                    break;
                }
            }

            _roadStations.Insert(insertAt, station);
            Map.SetStationId(station.Coordinate, checked(station.Id + 1));
            if (station.Id >= _nextRoadStationId)
            {
                _nextRoadStationId = checked(station.Id + 1);
            }
        }

        /// <summary>
        /// Spawns a placeholder road vehicle parked at an existing station.
        /// </summary>
        internal RoadVehicle AddRoadVehicle(int stationId)
        {
            if (!TryGetRoadStation(stationId, out RoadStation? station) || station == null)
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

        /// <summary>
        /// Returns the rail station occupying the tile when one exists.
        /// </summary>
        public bool TryGetRailStationAt(TileCoordinate coordinate, out RailStation? station)
        {
            int encoded = Map.GetRailStationId(coordinate);
            if (encoded == TileMap.NoRailStationId)
            {
                station = null;
                return false;
            }

            int stationId = checked(encoded - 1);
            return TryGetRailStation(stationId, out station);
        }

        /// <summary>
        /// Returns the rail station with the given id when it exists.
        /// </summary>
        public bool TryGetRailStation(int stationId, out RailStation? station)
        {
            for (int index = 0; index < _railStations.Count; index++)
            {
                if (_railStations[index].Id == stationId)
                {
                    station = _railStations[index];
                    return true;
                }
            }

            station = null;
            return false;
        }

        /// <summary>
        /// Returns the rail vehicle with the given id when it exists.
        /// </summary>
        public bool TryGetRailVehicle(int vehicleId, out RailVehicle? vehicle)
        {
            for (int index = 0; index < _railVehicles.Count; index++)
            {
                if (_railVehicles[index].Id == vehicleId)
                {
                    vehicle = _railVehicles[index];
                    return true;
                }
            }

            vehicle = null;
            return false;
        }

        /// <summary>
        /// Places a rail station on an already-validated empty tile and rebuilds the rail graph.
        /// </summary>
        internal RailStation AddRailStation(TileCoordinate coordinate)
        {
            int id = _nextRailStationId;
            _nextRailStationId = checked(_nextRailStationId + 1);
            var station = new RailStation(id, coordinate);
            _railStations.Add(station);
            Map.SetRailStationId(coordinate, checked(id + 1));
            RailGraph.RebuildFromMap(Map);
            return station;
        }

        /// <summary>
        /// Removes the rail station on the tile when present and rebuilds the rail graph.
        /// </summary>
        internal bool RemoveRailStationAt(TileCoordinate coordinate)
        {
            if (!TryGetRailStationAt(coordinate, out RailStation? station) || station == null)
            {
                return false;
            }

            for (int index = 0; index < _railStations.Count; index++)
            {
                if (_railStations[index].Id == station.Id)
                {
                    _railStations.RemoveAt(index);
                    break;
                }
            }

            Map.SetRailStationId(coordinate, TileMap.NoRailStationId);
            RailGraph.RebuildFromMap(Map);
            return true;
        }

        /// <summary>
        /// Inserts a restored rail station and advances the next-id counter past its id.
        /// </summary>
        internal void RestoreRailStation(RailStation station)
        {
#pragma warning disable CA1510
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
#pragma warning restore CA1510

            for (int index = 0; index < _railStations.Count; index++)
            {
                if (_railStations[index].Id == station.Id)
                {
                    throw new ArgumentException($"Duplicate rail station id {station.Id}.", nameof(station));
                }
            }

            int insertAt = _railStations.Count;
            for (int index = 0; index < _railStations.Count; index++)
            {
                if (_railStations[index].Id > station.Id)
                {
                    insertAt = index;
                    break;
                }
            }

            _railStations.Insert(insertAt, station);
            Map.SetRailStationId(station.Coordinate, checked(station.Id + 1));
            if (station.Id >= _nextRailStationId)
            {
                _nextRailStationId = checked(station.Id + 1);
            }
        }

        /// <summary>
        /// Spawns a placeholder rail vehicle parked at an existing rail station.
        /// </summary>
        internal RailVehicle AddRailVehicle(int stationId)
        {
            if (!TryGetRailStation(stationId, out RailStation? station) || station == null)
            {
                throw new InvalidOperationException($"Rail station {stationId} does not exist.");
            }

            if (!RailGraph.TryGetNodeId(station.Coordinate, out int nodeId))
            {
                throw new InvalidOperationException($"Rail station {stationId} is not a graph node.");
            }

            int id = _nextRailVehicleId;
            _nextRailVehicleId = checked(_nextRailVehicleId + 1);
            var vehicle = new RailVehicle(id, RailVehicle.PlaceholderTypeId, nodeId);
            InsertRailVehicleSorted(vehicle);
            return vehicle;
        }

        /// <summary>
        /// Inserts a restored rail vehicle and advances the next-id counter past its id.
        /// </summary>
        internal void RestoreRailVehicle(RailVehicle vehicle)
        {
#pragma warning disable CA1510
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }
#pragma warning restore CA1510

            InsertRailVehicleSorted(vehicle);
            if (vehicle.Id >= _nextRailVehicleId)
            {
                _nextRailVehicleId = checked(vehicle.Id + 1);
            }
        }

        /// <summary>
        /// Removes a rail vehicle by id when present.
        /// </summary>
        internal bool RemoveRailVehicle(int vehicleId)
        {
            for (int index = 0; index < _railVehicles.Count; index++)
            {
                if (_railVehicles[index].Id == vehicleId)
                {
                    _railVehicles.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Credits company cash from an accepted delivery (or tests).
        /// </summary>
        public void CreditCompanyCash(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Credit amount cannot be negative.");
            }

            if (amount == 0)
            {
                return;
            }

            CompanyCash = checked(CompanyCash + amount);
        }

        /// <summary>
        /// Replaces company cash when restoring a save.
        /// </summary>
        internal void RestoreCompanyCash(long companyCash)
        {
            if (companyCash < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(companyCash), companyCash, "Company cash cannot be negative.");
            }

            CompanyCash = companyCash;
        }

        private static List<Town> CopyTowns(Town[] towns)
        {
            var copy = new List<Town>(towns.Length);
            for (int index = 0; index < towns.Length; index++)
            {
                Town town = towns[index];
#pragma warning disable CA1510
                if (town == null)
                {
                    throw new ArgumentNullException(nameof(towns), $"Town at index {index} is null.");
                }
#pragma warning restore CA1510

                if (town.Id != index)
                {
                    throw new ArgumentException($"Town at index {index} has id {town.Id}.", nameof(towns));
                }

                copy.Add(new Town(
                    town.Id,
                    town.Coordinate,
                    town.Population,
                    town.Needs,
                    town.StatsYear,
                    town.StatsMonth));
            }

            return copy;
        }

        private static PlacedIndustry[] CopyIndustries(PlacedIndustry[] industries)
        {
#pragma warning disable CA1510
            if (industries == null)
            {
                throw new ArgumentNullException(nameof(industries));
            }
#pragma warning restore CA1510

            var copy = new PlacedIndustry[industries.Length];
            for (int index = 0; index < industries.Length; index++)
            {
                PlacedIndustry industry = industries[index];
#pragma warning disable CA1510
                if (industry == null)
                {
                    throw new ArgumentNullException(nameof(industries), $"Industry at index {index} is null.");
                }
#pragma warning restore CA1510

                copy[index] = new PlacedIndustry(
                    industry.IndustryId,
                    industry.InstanceIndex,
                    industry.Coordinate,
                    industry.Cargo.Stacks);
            }

            return copy;
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

        private void InsertRailVehicleSorted(RailVehicle vehicle)
        {
            int insertAt = _railVehicles.Count;
            for (int index = 0; index < _railVehicles.Count; index++)
            {
                if (_railVehicles[index].Id == vehicle.Id)
                {
                    throw new ArgumentException($"Duplicate rail vehicle id {vehicle.Id}.", nameof(vehicle));
                }

                if (_railVehicles[index].Id > vehicle.Id)
                {
                    insertAt = index;
                    break;
                }
            }

            _railVehicles.Insert(insertAt, vehicle);
        }
    }
}
