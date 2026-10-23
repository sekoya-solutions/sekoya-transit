#nullable enable
using System;
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

            if (document.formatVersion >= 3 && document.roadStations == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 4 && document.roadVehicles == null)
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
            for (int index = 0; index < towns.Length; index++)
            {
                SavedCoordinate? coordinate = document.townAnchors[index];
                if (coordinate == null)
                {
                    throw new SaveFormatException($"Town anchor {index} is missing.");
                }

                towns[index] = new TileCoordinate(coordinate.x, coordinate.y);
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
                industries[index] = new PlacedIndustry(
                    industry.industryId,
                    industry.instanceIndex,
                    new TileCoordinate(industry.x, industry.y),
                    industryCargo);
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

                    CargoAmount[] stationCargo = document.formatVersion >= 5
                        ? ReadCargoEntries(station.cargo, $"Road station {index}")
                        : Array.Empty<CargoAmount>();
                    roadStations[index] = new RoadStation(
                        station.id,
                        new TileCoordinate(station.x, station.y),
                        stationCargo);
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
                        document.formatVersion >= 5 && vehicle.isUnloadComplete);
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
                    towns,
                    industries,
                    roadStations,
                    roadVehicles,
                    companyCash);
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
                townAnchors[index] = new SavedCoordinate { x = coordinate.X, y = coordinate.Y };
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
                    cargo = WriteCargoEntries(industry.Cargo)
                };
            }

            var terrainTypeIds = new int[snapshot.TileCount];
            var objectIds = new int[snapshot.TileCount];
            var ownerIds = new int[snapshot.TileCount];
            var roadTypeIds = new int[snapshot.TileCount];
            snapshot.TerrainTypeIds.CopyTo(terrainTypeIds);
            snapshot.ObjectIds.CopyTo(objectIds);
            snapshot.OwnerIds.CopyTo(ownerIds);
            snapshot.RoadTypeIds.CopyTo(roadTypeIds);

            var roadStations = new SavedRoadStation[snapshot.RoadStations.Count];
            for (int index = 0; index < roadStations.Length; index++)
            {
                RoadStation station = snapshot.RoadStations[index];
                roadStations[index] = new SavedRoadStation
                {
                    id = station.Id,
                    x = station.Coordinate.X,
                    y = station.Coordinate.Y,
                    cargo = WriteCargoEntries(station.Cargo)
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
                    isUnloadComplete = vehicle.IsUnloadComplete
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
                townAnchors = townAnchors,
                industries = industries,
                roadStations = roadStations,
                roadVehicles = roadVehicles
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
            public SavedCoordinate[]? townAnchors;
            public SavedIndustry[]? industries;
            public SavedRoadStation[]? roadStations;
            public SavedRoadVehicle[]? roadVehicles;
        }

        [Serializable]
        private sealed class SavedCoordinate
        {
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class SavedIndustry
        {
            public string industryId = string.Empty;
            public int instanceIndex;
            public int x;
            public int y;
            public SavedCargoEntry[]? cargo;
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
        }

        [Serializable]
        private sealed class SavedCargoEntry
        {
            public string cargoId = string.Empty;
            public int amount;
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
    }
}
