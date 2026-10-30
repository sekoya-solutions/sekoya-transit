#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
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
        private readonly HashSet<string> _unlockedVehicleTypeIds;
        private readonly List<string> _unlockedVehicleTypeIdsOrdered;
        private readonly ReadOnlyCollection<string> _unlockedVehicleTypeIdsView;
        private readonly HashSet<string> _completedAchievementIds;
        private readonly List<AchievementCompletion> _achievementCompletions;
        private readonly ReadOnlyCollection<AchievementCompletion> _achievementCompletionsView;
        private readonly Dictionary<string, long> _deliveredToTown;
        private readonly Dictionary<string, long> _deliveredToIndustry;
        private readonly List<Contract> _openContracts;
        private readonly ReadOnlyCollection<Contract> _openContractsView;
        private Pcg32Random _contractRandom;
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

        /// <summary>
        /// Id given to the first contract of a world.
        /// </summary>
        public const int FirstContractId = 1;

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
            LastUpkeepYear = 0;
            LastUpkeepMonth = 0;
            _unlockedVehicleTypeIds = new HashSet<string>(StringComparer.Ordinal);
            _unlockedVehicleTypeIdsOrdered = new List<string>();
            _unlockedVehicleTypeIdsView = _unlockedVehicleTypeIdsOrdered.AsReadOnly();
            _completedAchievementIds = new HashSet<string>(StringComparer.Ordinal);
            _achievementCompletions = new List<AchievementCompletion>();
            _achievementCompletionsView = _achievementCompletions.AsReadOnly();
            _deliveredToTown = new Dictionary<string, long>(StringComparer.Ordinal);
            _deliveredToIndustry = new Dictionary<string, long>(StringComparer.Ordinal);
            _openContracts = new List<Contract>();
            _openContractsView = _openContracts.AsReadOnly();
            _contractRandom = CreateContractRandom(mapSeed);
        }

        /// <summary>
        /// Creates the contract generator in its initial state for a world seed.
        /// </summary>
        public static Pcg32Random CreateContractRandom(ulong mapSeed)
        {
            return new Pcg32Random(mapSeed, Contracts.RandomSequence);
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
        /// Gets the company's income and spending by category for this and the previous calendar year.
        /// </summary>
        public FinanceLedger Finance { get; } = new FinanceLedger();

        /// <summary>
        /// Gets the lifetime counters measured by the achievement tracks (Issue #223).
        /// </summary>
        public CompanyStatistics Statistics { get; } = new CompanyStatistics();

        /// <summary>
        /// Gets whether the company has no cash and is bankrupt.
        /// </summary>
        public bool IsBankrupt
        {
            get
            {
                return CompanyCash == 0L;
            }
        }

        /// <summary>
        /// Gets the calendar year of the last applied vehicle-upkeep month, or 0 when unset.
        /// </summary>
        public int LastUpkeepYear { get; private set; }

        /// <summary>
        /// Gets the calendar month of the last applied vehicle-upkeep month, or 0 when unset.
        /// </summary>
        public int LastUpkeepMonth { get; private set; }

        /// <summary>
        /// Gets permanently unlocked non-starting vehicle type ids in ascending ordinal order.
        /// </summary>
        public IReadOnlyList<string> UnlockedVehicleTypeIds
        {
            get
            {
                return _unlockedVehicleTypeIdsView;
            }
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
            RoadVehicleMovement.RebuildGraphKeepingVehicles(this);
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
            RoadVehicleMovement.RebuildGraphKeepingVehicles(this);
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
        /// Spawns a road vehicle of the given type parked at an existing station.
        /// </summary>
        internal RoadVehicle AddRoadVehicle(int stationId, string typeId, int cargoCapacity)
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
            var vehicle = new RoadVehicle(id, typeId, nodeId, cargoCapacity);
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
            RailVehicleMovement.RebuildGraphKeepingVehicles(this);
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
            RailVehicleMovement.RebuildGraphKeepingVehicles(this);
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
        /// Spawns a rail locomotive of the given type parked at an existing rail station.
        /// </summary>
        internal RailVehicle AddRailVehicle(int stationId, string typeId, int maxWagons, int capacityPerWagon)
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
            var vehicle = new RailVehicle(id, typeId, nodeId, maxWagons, capacityPerWagon);
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
        /// Credits company cash and records the amount in the finance ledger under an income category. Delivery income
        /// and contract bonuses also count toward <see cref="CompanyStatistics.LifetimeIncome"/>.
        /// </summary>
        public void CreditCompanyCash(long amount, FinanceCategory category)
        {
            if (!FinanceCategories.IsIncome(category))
            {
                throw new ArgumentException("A credit needs an income category.", nameof(category));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Credit amount cannot be negative.");
            }

            if (amount == 0)
            {
                return;
            }

            CompanyCash = checked(CompanyCash + amount);
            Finance.Record(category, amount);
            if (category == FinanceCategory.DeliveryIncome || category == FinanceCategory.ContractBonus)
            {
                Statistics.AddIncome(amount);
            }
        }

        /// <summary>
        /// Debits company cash when the company can afford the full amount.
        /// </summary>
        /// <returns><see langword="true"/> when cash was debited; otherwise <see langword="false"/>.</returns>
        public bool TryDebitCompanyCash(long amount, FinanceCategory category)
        {
            if (FinanceCategories.IsIncome(category))
            {
                throw new ArgumentException("A debit needs a spending category.", nameof(category));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Debit amount cannot be negative.");
            }

            if (amount == 0)
            {
                return true;
            }

            if (CompanyCash < amount)
            {
                return false;
            }

            CompanyCash -= amount;
            Finance.Record(category, amount);
            return true;
        }

        /// <summary>
        /// Debits up to <paramref name="amount"/> without letting cash go negative.
        /// </summary>
        /// <returns>The amount actually debited.</returns>
        public long DebitCompanyCashClamped(long amount, FinanceCategory category)
        {
            if (FinanceCategories.IsIncome(category))
            {
                throw new ArgumentException("A debit needs a spending category.", nameof(category));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Debit amount cannot be negative.");
            }

            if (amount == 0 || CompanyCash == 0)
            {
                return 0L;
            }

            long debit = amount <= CompanyCash ? amount : CompanyCash;
            CompanyCash -= debit;
            Finance.Record(category, debit);
            return debit;
        }

        /// <summary>
        /// Records the calendar month that vehicle upkeep was last applied (or observed).
        /// </summary>
        internal void SetLastUpkeepMonth(int year, int month)
        {
            if (year < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), year, "Upkeep year cannot be negative.");
            }

            if (month < 0 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "Upkeep month must be between 0 and 12.");
            }

            if ((year == 0) != (month == 0))
            {
                throw new ArgumentException("Upkeep year and month must both be unset (0/0) or both set.");
            }

            LastUpkeepYear = year;
            LastUpkeepMonth = month;
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

        /// <summary>
        /// Returns whether <paramref name="typeId"/> was permanently unlocked with money.
        /// </summary>
        public bool IsVehicleTypeUnlocked(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return false;
            }

            return _unlockedVehicleTypeIds.Contains(typeId);
        }

        /// <summary>
        /// Records a permanent vehicle-type unlock after a successful unlock purchase.
        /// </summary>
        internal void UnlockVehicleType(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Vehicle type id cannot be null or whitespace.", nameof(typeId));
            }

            if (!_unlockedVehicleTypeIds.Add(typeId))
            {
                return;
            }

            _unlockedVehicleTypeIdsOrdered.Add(typeId);
            _unlockedVehicleTypeIdsOrdered.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// Replaces the permanent unlock set when restoring a save.
        /// </summary>
        internal void RestoreUnlockedVehicleTypes(IReadOnlyList<string> typeIds)
        {
#pragma warning disable CA1510
            if (typeIds == null)
            {
                throw new ArgumentNullException(nameof(typeIds));
            }
#pragma warning restore CA1510

            _unlockedVehicleTypeIds.Clear();
            _unlockedVehicleTypeIdsOrdered.Clear();
            for (int index = 0; index < typeIds.Count; index++)
            {
                string typeId = typeIds[index];
                if (string.IsNullOrWhiteSpace(typeId))
                {
                    throw new ArgumentException(
                        $"Unlocked vehicle type id at index {index} is null or whitespace.",
                        nameof(typeIds));
                }

                if (!_unlockedVehicleTypeIds.Add(typeId))
                {
                    throw new ArgumentException(
                        $"Duplicate unlocked vehicle type id '{typeId}'.",
                        nameof(typeIds));
                }

                _unlockedVehicleTypeIdsOrdered.Add(typeId);
            }

            _unlockedVehicleTypeIdsOrdered.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets completed achievements in completion order. Ids no longer present in content are kept.
        /// </summary>
        public IReadOnlyList<AchievementCompletion> AchievementCompletions
        {
            get
            {
                return _achievementCompletionsView;
            }
        }

        /// <summary>
        /// Returns whether <paramref name="achievementId"/> has been completed.
        /// </summary>
        public bool IsAchievementCompleted(string achievementId)
        {
            return !string.IsNullOrWhiteSpace(achievementId) && _completedAchievementIds.Contains(achievementId);
        }

        /// <summary>
        /// Returns lifetime delivered units of <paramref name="cargoId"/> for <paramref name="destinationKind"/>
        /// (<see cref="DeliveryDestinationKind.Any"/> sums town and industry).
        /// </summary>
        public long GetCargoDelivered(string cargoId, DeliveryDestinationKind destinationKind)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                return 0L;
            }

            _deliveredToTown.TryGetValue(cargoId, out long toTown);
            _deliveredToIndustry.TryGetValue(cargoId, out long toIndustry);
            switch (destinationKind)
            {
                case DeliveryDestinationKind.Town:
                    return toTown;
                case DeliveryDestinationKind.Industry:
                    return toIndustry;
                default:
                    return checked(toTown + toIndustry);
            }
        }

        /// <summary>
        /// Gets the non-zero lifetime delivery counters ordered by cargo id (ordinal), then town before industry.
        /// </summary>
        public IReadOnlyList<CargoDeliveryCounter> CargoDeliveryCounters
        {
            get
            {
                var cargoIds = new List<string>(_deliveredToTown.Keys);
                foreach (string cargoId in _deliveredToIndustry.Keys)
                {
                    if (!_deliveredToTown.ContainsKey(cargoId))
                    {
                        cargoIds.Add(cargoId);
                    }
                }

                cargoIds.Sort(StringComparer.Ordinal);
                var counters = new List<CargoDeliveryCounter>(cargoIds.Count * 2);
                for (int index = 0; index < cargoIds.Count; index++)
                {
                    string cargoId = cargoIds[index];
                    if (_deliveredToTown.TryGetValue(cargoId, out long toTown) && toTown > 0L)
                    {
                        counters.Add(new CargoDeliveryCounter(cargoId, DeliveryDestinationKind.Town, toTown));
                    }

                    if (_deliveredToIndustry.TryGetValue(cargoId, out long toIndustry) && toIndustry > 0L)
                    {
                        counters.Add(new CargoDeliveryCounter(cargoId, DeliveryDestinationKind.Industry, toIndustry));
                    }
                }

                return counters.AsReadOnly();
            }
        }

        /// <summary>
        /// Adds an accepted and paid delivery to the lifetime counters (called by delivery credit and tests).
        /// </summary>
        public void RecordCargoDelivered(string cargoId, DeliveryDestinationKind destinationKind, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Dictionary<string, long> counters = SelectDeliveryCounters(destinationKind);
            counters.TryGetValue(cargoId, out long current);
            counters[cargoId] = checked(current + amount);
        }

        /// <summary>
        /// Records a permanent achievement completion; a second completion of the same id is ignored.
        /// </summary>
        internal void CompleteAchievement(string achievementId, GameDate date)
        {
            if (!_completedAchievementIds.Add(achievementId))
            {
                return;
            }

            _achievementCompletions.Add(new AchievementCompletion(achievementId, date));
        }

        /// <summary>
        /// Replaces achievement completions and delivery counters when restoring a save.
        /// </summary>
        internal void RestoreAchievementProgress(
            IReadOnlyList<AchievementCompletion> completions,
            IReadOnlyList<CargoDeliveryCounter> counters)
        {
#pragma warning disable CA1510
            if (completions == null)
            {
                throw new ArgumentNullException(nameof(completions));
            }

            if (counters == null)
            {
                throw new ArgumentNullException(nameof(counters));
            }
#pragma warning restore CA1510

            _completedAchievementIds.Clear();
            _achievementCompletions.Clear();
            _deliveredToTown.Clear();
            _deliveredToIndustry.Clear();
            for (int index = 0; index < completions.Count; index++)
            {
                if (!_completedAchievementIds.Add(completions[index].AchievementId))
                {
                    throw new ArgumentException(
                        $"Duplicate completed achievement id '{completions[index].AchievementId}'.",
                        nameof(completions));
                }

                _achievementCompletions.Add(completions[index]);
            }

            for (int index = 0; index < counters.Count; index++)
            {
                CargoDeliveryCounter counter = counters[index];
                Dictionary<string, long> target = SelectDeliveryCounters(counter.DestinationKind);
                if (!target.TryAdd(counter.CargoId, counter.Amount))
                {
                    throw new ArgumentException(
                        $"Duplicate delivery counter for '{counter.CargoId}' ({counter.DestinationKind}).",
                        nameof(counters));
                }
            }
        }

        /// <summary>
        /// Gets the open contracts in offer order (ascending id).
        /// </summary>
        public IReadOnlyList<Contract> OpenContracts
        {
            get
            {
                return _openContractsView;
            }
        }

        /// <summary>
        /// Gets the id the next offered contract receives.
        /// </summary>
        public int NextContractId { get; private set; } = FirstContractId;

        /// <summary>
        /// Gets the captured state of the contract random generator.
        /// </summary>
        public Pcg32State ContractRandomState
        {
            get
            {
                return _contractRandom.CapturedState;
            }
        }

        /// <summary>
        /// Gets the year of the month in which contracts were last topped up (0 when not yet this session).
        /// </summary>
        public int LastContractYear { get; private set; }

        /// <summary>
        /// Gets the month in which contracts were last topped up (0 when not yet this session).
        /// </summary>
        public int LastContractMonth { get; private set; }

        internal Pcg32Random ContractRandom
        {
            get
            {
                return _contractRandom;
            }
        }

        /// <summary>
        /// Returns whether an open contract already asks for <paramref name="cargoId"/> at the given target.
        /// </summary>
        public bool HasOpenContract(string cargoId, DeliveryDestinationKind targetKind, int targetIndex)
        {
            for (int index = 0; index < _openContracts.Count; index++)
            {
                Contract contract = _openContracts[index];
                if (contract.TargetKind == targetKind &&
                    contract.TargetIndex == targetIndex &&
                    string.Equals(contract.CargoId, cargoId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Counts an accepted and paid delivery toward the open contract for that cargo and the target
        /// next to the station at <paramref name="stationCoordinate"/> (called by delivery credit and tests).
        /// </summary>
        public void RecordContractDelivery(TileCoordinate stationCoordinate, string cargoId, int amount)
        {
            if (amount <= 0 || _openContracts.Count == 0)
            {
                return;
            }

            DeliveryDestinationKind kind;
            int targetIndex;
            if (StationAdjacency.TryGetAdjacentTown(this, stationCoordinate, out Town? town) && town != null)
            {
                kind = DeliveryDestinationKind.Town;
                targetIndex = _towns.IndexOf(town);
            }
            else if (StationAdjacency.TryGetAdjacentIndustry(this, stationCoordinate, out PlacedIndustry? industry) &&
                industry != null)
            {
                kind = DeliveryDestinationKind.Industry;
                targetIndex = -1;
                for (int index = 0; index < _industries.Count; index++)
                {
                    if (ReferenceEquals(_industries[index], industry))
                    {
                        targetIndex = index;
                        break;
                    }
                }
            }
            else
            {
                return;
            }

            for (int index = 0; index < _openContracts.Count; index++)
            {
                Contract contract = _openContracts[index];
                if (contract.TargetKind == kind &&
                    contract.TargetIndex == targetIndex &&
                    string.Equals(contract.CargoId, cargoId, StringComparison.Ordinal))
                {
                    contract.AddDelivered(amount);
                    return;
                }
            }
        }

        internal void SetLastContractMonth(int year, int month)
        {
            LastContractYear = year;
            LastContractMonth = month;
        }

        public Contract OfferContract(
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            int amount,
            GameDate startDate,
            GameDate deadline)
        {
            var contract = new Contract(
                NextContractId,
                cargoId,
                targetKind,
                targetIndex,
                amount,
                0L,
                startDate,
                deadline);
            NextContractId++;
            _openContracts.Add(contract);
            return contract;
        }

        internal bool RemoveContract(int contractId)
        {
            for (int index = 0; index < _openContracts.Count; index++)
            {
                if (_openContracts[index].Id == contractId)
                {
                    _openContracts.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Replaces open contracts, the next id, and the generator state when restoring a save. Contracts whose
        /// cargo or target is no longer valid for <paramref name="catalog"/> are dropped, as are duplicates of
        /// the same (cargo, target). Returns the number dropped.
        /// </summary>
        public int RestoreContracts(
            IReadOnlyList<Contract> contracts,
            int nextContractId,
            Pcg32State randomState,
            ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (contracts == null)
            {
                throw new ArgumentNullException(nameof(contracts));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            if (nextContractId < FirstContractId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextContractId),
                    nextContractId,
                    "The next contract id cannot be below the first id.");
            }

            var incoming = new List<Contract>(contracts);
            _openContracts.Clear();
            int dropped = 0;
            for (int index = 0; index < incoming.Count; index++)
            {
                Contract contract = incoming[index];
                if (contract.Id >= nextContractId)
                {
                    throw new ArgumentException(
                        $"Contract id {contract.Id} is not below the next contract id {nextContractId}.",
                        nameof(contracts));
                }

                if (!Contracts.IsValid(this, catalog, contract.CargoId, contract.TargetKind, contract.TargetIndex) ||
                    HasOpenContract(contract.CargoId, contract.TargetKind, contract.TargetIndex))
                {
                    dropped++;
                    continue;
                }

                _openContracts.Add(contract.Copy());
            }

            NextContractId = nextContractId;
            _contractRandom = new Pcg32Random(randomState);
            LastContractYear = 0;
            LastContractMonth = 0;
            return dropped;
        }

        private Dictionary<string, long> SelectDeliveryCounters(DeliveryDestinationKind destinationKind)
        {
            switch (destinationKind)
            {
                case DeliveryDestinationKind.Town:
                    return _deliveredToTown;
                case DeliveryDestinationKind.Industry:
                    return _deliveredToIndustry;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(destinationKind),
                        destinationKind,
                        "A recorded delivery destination must be town or industry.");
            }
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

                copy.Add(town.Copy());
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

                copy[index] = industry.Copy();
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
