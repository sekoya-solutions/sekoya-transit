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

            if (!ulong.TryParse(document.mapSeed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong mapSeed))
            {
                throw new SaveFormatException("The JSON save has an invalid map seed.");
            }

            if (!long.TryParse(document.currentTick, NumberStyles.None, CultureInfo.InvariantCulture, out long currentTick))
            {
                throw new SaveFormatException("The JSON save has an invalid simulation tick.");
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

                industries[index] = new PlacedIndustry(
                    industry.industryId,
                    industry.instanceIndex,
                    new TileCoordinate(industry.x, industry.y));
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

                    roadStations[index] = new RoadStation(
                        station.id,
                        new TileCoordinate(station.x, station.y));
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
                    roadStations);
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
                    y = industry.Coordinate.Y
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
                    y = station.Coordinate.Y
                };
            }

            return new SaveDocument
            {
                formatVersion = snapshot.FormatVersion,
                mapSeed = snapshot.MapSeed.ToString(CultureInfo.InvariantCulture),
                currentTick = snapshot.CurrentTick.Value.ToString(CultureInfo.InvariantCulture),
                mapSize = (int)snapshot.MapSize,
                terrainTypeIds = terrainTypeIds,
                objectIds = objectIds,
                ownerIds = ownerIds,
                roadTypeIds = roadTypeIds,
                townAnchors = townAnchors,
                industries = industries,
                roadStations = roadStations
            };
        }

        [Serializable]
        private sealed class SaveDocument
        {
            public int formatVersion;
            public string? mapSeed;
            public string? currentTick;
            public int mapSize;
            public int[]? terrainTypeIds;
            public int[]? objectIds;
            public int[]? ownerIds;
            public int[]? roadTypeIds;
            public SavedCoordinate[]? townAnchors;
            public SavedIndustry[]? industries;
            public SavedRoadStation[]? roadStations;
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
        }

        [Serializable]
        private sealed class SavedRoadStation
        {
            public int id;
            public int x;
            public int y;
        }
    }
}
