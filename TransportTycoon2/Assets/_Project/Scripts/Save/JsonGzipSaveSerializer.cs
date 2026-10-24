#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Save
{
    /// <summary>
    /// Serializes versioned snapshots as Unity JSON compressed with gzip.
    /// </summary>
    public sealed class JsonGzipSaveSerializer : ISaveSerializer
    {
        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);
        private readonly SaveMigrationPipeline _migrationPipeline;

        /// <summary>
        /// Initializes the serializer with the migration chain for the current format.
        /// </summary>
        public JsonGzipSaveSerializer(SaveMigrationPipeline migrationPipeline)
        {
#pragma warning disable CA1510 // Keep the Unity 6.3-compatible Core API surface.
            if (migrationPipeline == null)
            {
                throw new ArgumentNullException(nameof(migrationPipeline));
            }
#pragma warning restore CA1510

            _migrationPipeline = migrationPipeline;
        }

        /// <inheritdoc />
        public byte[] Serialize(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Keep the Unity 6.3-compatible Core API surface.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion != _migrationPipeline.CurrentVersion)
            {
                throw new SaveFormatException(
                    $"Only current format version {_migrationPipeline.CurrentVersion} can be written.");
            }

            string json;
            try
            {
                json = JsonUtility.ToJson(CreateDocument(snapshot));
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException("The save snapshot could not be converted to JSON.", exception);
            }

            byte[] jsonBytes = _utf8.GetBytes(json);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    gzip.Write(jsonBytes, 0, jsonBytes.Length);
                }

                return output.ToArray();
            }
        }

        /// <inheritdoc />
        public SaveSnapshot Deserialize(byte[] compressedData)
        {
#pragma warning disable CA1510 // Keep the Unity 6.3-compatible Core API surface.
            if (compressedData == null)
            {
                throw new ArgumentNullException(nameof(compressedData));
            }
#pragma warning restore CA1510

            if (compressedData.Length == 0)
            {
                throw new SaveFormatException("The compressed save is empty.");
            }

            string json = Decompress(compressedData);
            SaveDocument? document;
            try
            {
                document = JsonUtility.FromJson<SaveDocument>(json);
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException("The decompressed save is not valid JSON.", exception);
            }

            if (document == null || document.formatVersion < 1)
            {
                throw new SaveFormatException("The JSON save has no valid format version.");
            }

            if (document.formatVersion > _migrationPipeline.CurrentVersion)
            {
                throw new SaveFormatException(
                    $"Save format version {document.formatVersion} is newer than supported version {_migrationPipeline.CurrentVersion}.");
            }

            if (document.terrainTypeIds == null ||
                document.objectIds == null ||
                document.ownerIds == null ||
                document.townAnchors == null ||
                document.industries == null ||
                string.IsNullOrWhiteSpace(document.mapSeed) ||
                string.IsNullOrWhiteSpace(document.currentTick))
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 2 && document.roadTypeIds == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 10 && document.railTypeIds == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 3 && document.roadStations == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 4 && document.roadVehicles == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 11 && document.railStations == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 11 && document.railVehicles == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 6 && string.IsNullOrWhiteSpace(document.companyCash))
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing company cash.");
            }

            if (!ulong.TryParse(document.mapSeed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong mapSeed))
            {
                throw new SaveFormatException("The JSON save has an invalid map seed.");
            }

            if (!long.TryParse(document.currentTick, NumberStyles.None, CultureInfo.InvariantCulture, out long currentTick))
            {
                throw new SaveFormatException("The JSON save has an invalid simulation tick.");
            }

            long companyCash = 0L;
            if (document.formatVersion >= 6)
            {
                if (!long.TryParse(document.companyCash, NumberStyles.None, CultureInfo.InvariantCulture, out companyCash) ||
                    companyCash < 0)
                {
                    throw new SaveFormatException("The JSON save has an invalid company cash value.");
                }
            }

            var towns = new TileCoordinate[document.townAnchors.Length];
            Town[]? townStates = document.formatVersion >= 13
                ? new Town[document.townAnchors.Length]
                : null;
            for (int index = 0; index < towns.Length; index++)
            {
                SavedCoordinate? coordinate = document.townAnchors[index];
                if (coordinate == null)
                {
                    throw new SaveFormatException($"Town anchor {index} is missing.");
                }

                var tile = new TileCoordinate(coordinate.x, coordinate.y);
                towns[index] = tile;
                if (townStates != null)
                {
                    if (coordinate.population < 0)
                    {
                        throw new SaveFormatException($"Town {index} has an invalid population.");
                    }

                    TownNeed[] needs = ReadTownNeeds(
                        coordinate.needs,
                        index,
                        document.formatVersion >= 14);
                    int statsYear = document.formatVersion >= 14 ? coordinate.statsYear : 0;
                    int statsMonth = document.formatVersion >= 14 ? coordinate.statsMonth : 0;
                    townStates[index] = new Town(
                        index,
                        tile,
                        coordinate.population,
                        needs,
                        statsYear,
                        statsMonth);
                }
            }

            var industries = new PlacedIndustry[document.industries.Length];
            for (int index = 0; index < industries.Length; index++)
            {
                SavedIndustry? industry = document.industries[index];
                if (industry == null)
                {
                    throw new SaveFormatException($"Industry placement {index} is missing.");
                }

                CargoAmount[] industryCargo = document.formatVersion >= 6
                    ? ReadCargoEntries(industry.cargo, $"Industry placement {index}")
                    : Array.Empty<CargoAmount>();
                long monthProduced = 0L;
                long monthTransported = 0L;
                int statsYear = 0;
                int statsMonth = 0;
                if (document.formatVersion >= 8)
                {
                    if (string.IsNullOrWhiteSpace(industry.monthProduced) ||
                        !long.TryParse(
                            industry.monthProduced,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out monthProduced) ||
                        monthProduced < 0L)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid monthProduced value.");
                    }

                    if (string.IsNullOrWhiteSpace(industry.monthTransported) ||
                        !long.TryParse(
                            industry.monthTransported,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out monthTransported) ||
                        monthTransported < 0L)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid monthTransported value.");
                    }

                    statsYear = industry.statsYear;
                    statsMonth = industry.statsMonth;
                }

                int loadRoundRobinIndex = 0;
                if (document.formatVersion >= 9)
                {
                    if (industry.loadRoundRobinIndex < 0)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid loadRoundRobinIndex value.");
                    }

                    loadRoundRobinIndex = industry.loadRoundRobinIndex;
                }

                industries[index] = new PlacedIndustry(
                    industry.industryId,
                    industry.instanceIndex,
                    new TileCoordinate(industry.x, industry.y),
                    industryCargo,
                    monthProduced,
                    monthTransported,
                    statsYear,
                    statsMonth,
                    loadRoundRobinIndex);
            }

            RoadStation[]? roadStations = null;
            if (document.formatVersion >= 3)
            {
                SavedRoadStation[] savedStations = document.roadStations!;
                roadStations = new RoadStation[savedStations.Length];
                for (int index = 0; index < savedStations.Length; index++)
                {
                    SavedRoadStation? station = savedStations[index];
                    if (station == null)
                    {
                        throw new SaveFormatException($"Road station {index} is missing.");
                    }

                    // Format v5–v8 may still serialize station cargo; v9+ ignores it (stations are stops only).
                    roadStations[index] = new RoadStation(
                        station.id,
                        new TileCoordinate(station.x, station.y));
                }
            }

            RoadVehicle[]? roadVehicles = null;
            if (document.formatVersion >= 4)
            {
                SavedRoadVehicle[] savedVehicles = document.roadVehicles!;
                roadVehicles = new RoadVehicle[savedVehicles.Length];
                for (int index = 0; index < savedVehicles.Length; index++)
                {
                    SavedRoadVehicle? vehicle = savedVehicles[index];
                    if (vehicle == null)
                    {
                        throw new SaveFormatException($"Road vehicle {index} is missing.");
                    }

                    int[] orderStationIds = vehicle.orderStationIds ?? Array.Empty<int>();
                    bool[] waitForFullFlags = vehicle.orderWaitForFull ?? Array.Empty<bool>();
                    if (document.formatVersion >= 5 && waitForFullFlags.Length != orderStationIds.Length)
                    {
                        throw new SaveFormatException(
                            $"Road vehicle {index} order wait-for-full flags length mismatch.");
                    }

                    var orders = new RoadVehicleOrder[orderStationIds.Length];
                    for (int orderIndex = 0; orderIndex < orderStationIds.Length; orderIndex++)
                    {
                        bool waitForFull = document.formatVersion >= 5 &&
                                           orderIndex < waitForFullFlags.Length &&
                                           waitForFullFlags[orderIndex];
                        orders[orderIndex] = new RoadVehicleOrder(orderStationIds[orderIndex], waitForFull);
                    }

                    CargoAmount[] vehicleCargo = document.formatVersion >= 5
                        ? ReadCargoEntries(vehicle.cargo, $"Road vehicle {index}")
                        : Array.Empty<CargoAmount>();
                    int cargoCapacity = document.formatVersion >= 5 && vehicle.cargoCapacity > 0
                        ? vehicle.cargoCapacity
                        : RoadVehicle.PlaceholderCargoCapacity;

                    long lifetimeIncome = 0L;
                    if (document.formatVersion >= 7)
                    {
                        if (string.IsNullOrWhiteSpace(vehicle.lifetimeDeliveryIncome) ||
                            !long.TryParse(
                                vehicle.lifetimeDeliveryIncome,
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out lifetimeIncome) ||
                            lifetimeIncome < 0L)
                        {
                            throw new SaveFormatException(
                                $"Road vehicle {index} has an invalid lifetime delivery income.");
                        }
                    }

                    roadVehicles[index] = new RoadVehicle(
                        vehicle.id,
                        vehicle.typeId,
                        orders,
                        vehicle.orderIndex,
                        vehicle.isStuck,
                        vehicle.isOnEdge,
                        vehicle.edgeFromNodeId,
                        vehicle.edgeToNodeId,
                        FixedPoint.FromRaw(vehicle.progressAlongEdgeRaw),
                        vehicleCargo,
                        cargoCapacity,
                        document.formatVersion >= 5 && vehicle.isServicingStop,
                        document.formatVersion >= 5 && vehicle.isUnloadComplete,
                        lifetimeIncome);
                }
            }

            RailStation[]? railStations = null;
            if (document.formatVersion >= 11)
            {
                SavedRailStation[] savedStations = document.railStations!;
                railStations = new RailStation[savedStations.Length];
                for (int index = 0; index < savedStations.Length; index++)
                {
                    SavedRailStation? station = savedStations[index];
                    if (station == null)
                    {
                        throw new SaveFormatException($"Rail station {index} is missing.");
                    }

                    railStations[index] = new RailStation(
                        station.id,
                        new TileCoordinate(station.x, station.y));
                }
            }

            RailVehicle[]? railVehicles = null;
            if (document.formatVersion >= 11)
            {
                SavedRailVehicle[] savedVehicles = document.railVehicles!;
                railVehicles = new RailVehicle[savedVehicles.Length];
                for (int index = 0; index < savedVehicles.Length; index++)
                {
                    SavedRailVehicle? vehicle = savedVehicles[index];
                    if (vehicle == null)
                    {
                        throw new SaveFormatException($"Rail vehicle {index} is missing.");
                    }

                    int[] orderStationIds = vehicle.orderStationIds ?? Array.Empty<int>();
                    bool[] waitForFullFlags = vehicle.orderWaitForFull ?? Array.Empty<bool>();
                    if (waitForFullFlags.Length != orderStationIds.Length)
                    {
                        throw new SaveFormatException(
                            $"Rail vehicle {index} order wait-for-full flags length mismatch.");
                    }

                    var orders = new RailVehicleOrder[orderStationIds.Length];
                    for (int orderIndex = 0; orderIndex < orderStationIds.Length; orderIndex++)
                    {
                        bool waitForFull = orderIndex < waitForFullFlags.Length && waitForFullFlags[orderIndex];
                        orders[orderIndex] = new RailVehicleOrder(orderStationIds[orderIndex], waitForFull);
                    }

                    CargoAmount[] vehicleCargo = ReadCargoEntries(vehicle.cargo, $"Rail vehicle {index}");
                    int wagonCount;
                    int cargoCapacity;
                    if (document.formatVersion >= 12)
                    {
                        wagonCount = vehicle.wagonCount;
                        if (wagonCount < 0 || wagonCount > RailVehicle.MaxWagons)
                        {
                            throw new SaveFormatException(
                                $"Rail vehicle {index} has an invalid wagon count.");
                        }

                        cargoCapacity = wagonCount * RailVehicle.CapacityPerWagon;
                        if (vehicle.cargoCapacity > 0 && vehicle.cargoCapacity != cargoCapacity)
                        {
                            throw new SaveFormatException(
                                $"Rail vehicle {index} cargo capacity does not match wagon count.");
                        }
                    }
                    else
                    {
                        int rawCapacity = vehicle.cargoCapacity > 0
                            ? vehicle.cargoCapacity
                            : RailVehicle.PlaceholderCargoCapacity;
                        wagonCount = DeriveRailWagonCount(rawCapacity);
                        cargoCapacity = wagonCount * RailVehicle.CapacityPerWagon;
                    }

                    var cargoInventory = new CargoInventory(vehicleCargo);
                    cargoInventory.TrimToMaxTotal(cargoCapacity);

                    if (string.IsNullOrWhiteSpace(vehicle.lifetimeDeliveryIncome) ||
                        !long.TryParse(
                            vehicle.lifetimeDeliveryIncome,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out long lifetimeIncome) ||
                        lifetimeIncome < 0L)
                    {
                        throw new SaveFormatException(
                            $"Rail vehicle {index} has an invalid lifetime delivery income.");
                    }

                    railVehicles[index] = new RailVehicle(
                        vehicle.id,
                        vehicle.typeId,
                        orders,
                        vehicle.orderIndex,
                        vehicle.isStuck,
                        vehicle.isOnEdge,
                        vehicle.edgeFromNodeId,
                        vehicle.edgeToNodeId,
                        FixedPoint.FromRaw(vehicle.progressAlongEdgeRaw),
                        cargoInventory.Stacks,
                        wagonCount,
                        cargoCapacity,
                        vehicle.isServicingStop,
                        vehicle.isUnloadComplete,
                        lifetimeIncome);
                }
            }

            SaveSnapshot snapshot;
            try
            {
                snapshot = new SaveSnapshot(
                    document.formatVersion,
                    mapSeed,
                    new Tick(currentTick),
                    (MapSize)document.mapSize,
                    document.terrainTypeIds,
                    document.objectIds,
                    document.ownerIds,
                    document.formatVersion >= 2 ? document.roadTypeIds : null,
                    document.formatVersion >= 10 ? document.railTypeIds : null,
                    towns,
                    industries,
                    roadStations,
                    roadVehicles,
                    companyCash,
                    railStations,
                    railVehicles,
                    townStates);
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException($"The JSON save contains invalid world data: {exception.Message}", exception);
            }

            return _migrationPipeline.Migrate(snapshot);
        }

        private static string Decompress(byte[] compressedData)
        {
            try
            {
                using (var input = new MemoryStream(compressedData, false))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return _utf8.GetString(output.ToArray());
                }
            }
            catch (IOException exception)
            {
                throw new SaveFormatException("The gzip save data is corrupt or truncated.", exception);
            }
            catch (InvalidDataException exception)
            {
                throw new SaveFormatException("The gzip save data is corrupt or truncated.", exception);
            }
            catch (DecoderFallbackException exception)
            {
                throw new SaveFormatException("The decompressed save is not valid UTF-8.", exception);
            }
        }

        private static SaveDocument CreateDocument(SaveSnapshot snapshot)
        {
            var townAnchors = new SavedCoordinate[snapshot.TownAnchors.Count];
            for (int index = 0; index < townAnchors.Length; index++)
            {
                TileCoordinate coordinate = snapshot.TownAnchors[index];
                var saved = new SavedCoordinate { x = coordinate.X, y = coordinate.Y };
                if (snapshot.HasTowns)
                {
                    Town town = snapshot.Towns[index];
                    saved.population = town.Population;
                    saved.needs = WriteTownNeeds(town.Needs);
                    saved.statsYear = town.StatsYear;
                    saved.statsMonth = town.StatsMonth;
                }

                townAnchors[index] = saved;
            }

            var industries = new SavedIndustry[snapshot.Industries.Count];
            for (int index = 0; index < industries.Length; index++)
            {
                PlacedIndustry industry = snapshot.Industries[index];
                industries[index] = new SavedIndustry
                {
                    industryId = industry.IndustryId,
                    instanceIndex = industry.InstanceIndex,
                    x = industry.Coordinate.X,
                    y = industry.Coordinate.Y,
                    cargo = WriteCargoEntries(industry.Cargo),
                    monthProduced = industry.MonthProduced.ToString(CultureInfo.InvariantCulture),
                    monthTransported = industry.MonthTransported.ToString(CultureInfo.InvariantCulture),
                    statsYear = industry.StatsYear,
                    statsMonth = industry.StatsMonth,
                    loadRoundRobinIndex = industry.LoadRoundRobinIndex
                };
            }

            var terrainTypeIds = new int[snapshot.TileCount];
            var objectIds = new int[snapshot.TileCount];
            var ownerIds = new int[snapshot.TileCount];
            var roadTypeIds = new int[snapshot.TileCount];
            var railTypeIds = new int[snapshot.TileCount];
            snapshot.TerrainTypeIds.CopyTo(terrainTypeIds);
            snapshot.ObjectIds.CopyTo(objectIds);
            snapshot.OwnerIds.CopyTo(ownerIds);
            snapshot.RoadTypeIds.CopyTo(roadTypeIds);
            snapshot.RailTypeIds.CopyTo(railTypeIds);

            var roadStations = new SavedRoadStation[snapshot.RoadStations.Count];
            for (int index = 0; index < roadStations.Length; index++)
            {
                RoadStation station = snapshot.RoadStations[index];
                roadStations[index] = new SavedRoadStation
                {
                    id = station.Id,
                    x = station.Coordinate.X,
                    y = station.Coordinate.Y
                };
            }

            var roadVehicles = new SavedRoadVehicle[snapshot.RoadVehicles.Count];
            for (int index = 0; index < roadVehicles.Length; index++)
            {
                RoadVehicle vehicle = snapshot.RoadVehicles[index];
                var orderStationIds = new int[vehicle.Orders.Count];
                var orderWaitForFull = new bool[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderStationIds[orderIndex] = vehicle.Orders[orderIndex].StationId;
                    orderWaitForFull[orderIndex] = vehicle.Orders[orderIndex].WaitForFull;
                }

                roadVehicles[index] = new SavedRoadVehicle
                {
                    id = vehicle.Id,
                    typeId = vehicle.TypeId,
                    orderStationIds = orderStationIds,
                    orderWaitForFull = orderWaitForFull,
                    orderIndex = vehicle.OrderIndex,
                    isStuck = vehicle.IsStuck,
                    isOnEdge = vehicle.IsOnEdge,
                    edgeFromNodeId = vehicle.EdgeFromNodeId,
                    edgeToNodeId = vehicle.EdgeToNodeId,
                    progressAlongEdgeRaw = vehicle.ProgressAlongEdge.RawValue,
                    cargo = WriteCargoEntries(vehicle.Cargo),
                    cargoCapacity = vehicle.CargoCapacity,
                    isServicingStop = vehicle.IsServicingStop,
                    isUnloadComplete = vehicle.IsUnloadComplete,
                    lifetimeDeliveryIncome = vehicle.LifetimeDeliveryIncome.ToString(CultureInfo.InvariantCulture)
                };
            }

            var railStations = new SavedRailStation[snapshot.RailStations.Count];
            for (int index = 0; index < railStations.Length; index++)
            {
                RailStation station = snapshot.RailStations[index];
                railStations[index] = new SavedRailStation
                {
                    id = station.Id,
                    x = station.Coordinate.X,
                    y = station.Coordinate.Y
                };
            }

            var railVehicles = new SavedRailVehicle[snapshot.RailVehicles.Count];
            for (int index = 0; index < railVehicles.Length; index++)
            {
                RailVehicle vehicle = snapshot.RailVehicles[index];
                var orderStationIds = new int[vehicle.Orders.Count];
                var orderWaitForFull = new bool[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderStationIds[orderIndex] = vehicle.Orders[orderIndex].StationId;
                    orderWaitForFull[orderIndex] = vehicle.Orders[orderIndex].WaitForFull;
                }

                railVehicles[index] = new SavedRailVehicle
                {
                    id = vehicle.Id,
                    typeId = vehicle.TypeId,
                    orderStationIds = orderStationIds,
                    orderWaitForFull = orderWaitForFull,
                    orderIndex = vehicle.OrderIndex,
                    isStuck = vehicle.IsStuck,
                    isOnEdge = vehicle.IsOnEdge,
                    edgeFromNodeId = vehicle.EdgeFromNodeId,
                    edgeToNodeId = vehicle.EdgeToNodeId,
                    progressAlongEdgeRaw = vehicle.ProgressAlongEdge.RawValue,
                    cargo = WriteCargoEntries(vehicle.Cargo),
                    wagonCount = vehicle.WagonCount,
                    cargoCapacity = vehicle.CargoCapacity,
                    isServicingStop = vehicle.IsServicingStop,
                    isUnloadComplete = vehicle.IsUnloadComplete,
                    lifetimeDeliveryIncome = vehicle.LifetimeDeliveryIncome.ToString(CultureInfo.InvariantCulture)
                };
            }

            return new SaveDocument
            {
                formatVersion = snapshot.FormatVersion,
                mapSeed = snapshot.MapSeed.ToString(CultureInfo.InvariantCulture),
                currentTick = snapshot.CurrentTick.Value.ToString(CultureInfo.InvariantCulture),
                mapSize = (int)snapshot.MapSize,
                companyCash = snapshot.CompanyCash.ToString(CultureInfo.InvariantCulture),
                terrainTypeIds = terrainTypeIds,
                objectIds = objectIds,
                ownerIds = ownerIds,
                roadTypeIds = roadTypeIds,
                railTypeIds = railTypeIds,
                townAnchors = townAnchors,
                industries = industries,
                roadStations = roadStations,
                roadVehicles = roadVehicles,
                railStations = railStations,
                railVehicles = railVehicles
            };
        }

        [Serializable]
        private sealed class SaveDocument
        {
            public int formatVersion;
            public string? mapSeed;
            public string? currentTick;
            public int mapSize;
            public string? companyCash;
            public int[]? terrainTypeIds;
            public int[]? objectIds;
            public int[]? ownerIds;
            public int[]? roadTypeIds;
            public int[]? railTypeIds;
            public SavedCoordinate[]? townAnchors;
            public SavedIndustry[]? industries;
            public SavedRoadStation[]? roadStations;
            public SavedRoadVehicle[]? roadVehicles;
            public SavedRailStation[]? railStations;
            public SavedRailVehicle[]? railVehicles;
        }

        [Serializable]
        private sealed class SavedCoordinate
        {
            public int x;
            public int y;
            public int population;
            public int statsYear;
            public int statsMonth;
            public SavedTownNeed[]? needs;
        }

        [Serializable]
        private sealed class SavedTownNeed
        {
            public string cargoId = string.Empty;
            public int monthSupply;
        }

        [Serializable]
        private sealed class SavedIndustry
        {
            public string industryId = string.Empty;
            public int instanceIndex;
            public int x;
            public int y;
            public SavedCargoEntry[]? cargo;
            public string? monthProduced;
            public string? monthTransported;
            public int statsYear;
            public int statsMonth;
            public int loadRoundRobinIndex;
        }

        [Serializable]
        private sealed class SavedRoadStation
        {
            public int id;
            public int x;
            public int y;
            public SavedCargoEntry[]? cargo;
        }

        [Serializable]
        private sealed class SavedRoadVehicle
        {
            public int id;
            public int typeId;
            public int[]? orderStationIds;
            public bool[]? orderWaitForFull;
            public int orderIndex;
            public bool isStuck;
            public bool isOnEdge;
            public int edgeFromNodeId;
            public int edgeToNodeId;
            public long progressAlongEdgeRaw;
            public SavedCargoEntry[]? cargo;
            public int cargoCapacity;
            public bool isServicingStop;
            public bool isUnloadComplete;
            public string? lifetimeDeliveryIncome;
        }

        [Serializable]
        private sealed class SavedRailStation
        {
            public int id;
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class SavedRailVehicle
        {
            public int id;
            public int typeId;
            public int[]? orderStationIds;
            public bool[]? orderWaitForFull;
            public int orderIndex;
            public bool isStuck;
            public bool isOnEdge;
            public int edgeFromNodeId;
            public int edgeToNodeId;
            public long progressAlongEdgeRaw;
            public SavedCargoEntry[]? cargo;
            public int wagonCount;
            public int cargoCapacity;
            public bool isServicingStop;
            public bool isUnloadComplete;
            public string? lifetimeDeliveryIncome;
        }

        [Serializable]
        private sealed class SavedCargoEntry
        {
            public string cargoId = string.Empty;
            public int amount;
        }

        private static SavedTownNeed[] WriteTownNeeds(IReadOnlyList<TownNeed> needs)
        {
            var saved = new SavedTownNeed[needs.Count];
            for (int index = 0; index < saved.Length; index++)
            {
                saved[index] = new SavedTownNeed
                {
                    cargoId = needs[index].CargoId,
                    monthSupply = needs[index].MonthSupply
                };
            }

            return saved;
        }

        private static TownNeed[] ReadTownNeeds(SavedTownNeed[]? saved, int townIndex, bool includeMonthSupply)
        {
            if (saved == null || saved.Length == 0)
            {
                return Array.Empty<TownNeed>();
            }

            var needs = new TownNeed[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedTownNeed? need = saved[index];
                if (need == null || string.IsNullOrWhiteSpace(need.cargoId))
                {
                    throw new SaveFormatException($"Town {townIndex} need {index} is invalid.");
                }

                int monthSupply = includeMonthSupply ? need.monthSupply : 0;
                if (monthSupply < 0)
                {
                    throw new SaveFormatException($"Town {townIndex} need {index} has invalid monthSupply.");
                }

                needs[index] = new TownNeed(need.cargoId, monthSupply);
            }

            return needs;
        }

        private static SavedCargoEntry[] WriteCargoEntries(CargoInventory inventory)
        {
            var stacks = new SavedCargoEntry[inventory.Stacks.Count];
            for (int index = 0; index < stacks.Length; index++)
            {
                CargoAmount stack = inventory.Stacks[index];
                stacks[index] = new SavedCargoEntry
                {
                    cargoId = stack.CargoId,
                    amount = stack.Amount
                };
            }

            return stacks;
        }

        private static CargoAmount[] ReadCargoEntries(SavedCargoEntry[]? saved, string context)
        {
            if (saved == null || saved.Length == 0)
            {
                return Array.Empty<CargoAmount>();
            }

            var stacks = new CargoAmount[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedCargoEntry? stack = saved[index];
                if (stack == null || string.IsNullOrWhiteSpace(stack.cargoId) || stack.amount <= 0)
                {
                    throw new SaveFormatException($"{context} cargo stack {index} is invalid.");
                }

                stacks[index] = new CargoAmount(stack.cargoId, stack.amount);
            }

            return stacks;
        }

        private static int DeriveRailWagonCount(int cargoCapacity)
        {
            if (cargoCapacity <= 0)
            {
                return 0;
            }

            int wagonCount = cargoCapacity / RailVehicle.CapacityPerWagon;
            if (wagonCount > RailVehicle.MaxWagons)
            {
                return RailVehicle.MaxWagons;
            }

            return wagonCount;
        }
    }
}
