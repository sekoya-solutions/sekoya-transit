#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Save.Tests
{
    [TestFixture]
    public sealed class JsonGzipSaveSerializerTests
    {
        private JsonGzipSaveSerializer _serializer = null!;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline());
        }

        [Test]
        public void SerializeAndDeserialize_CompleteSnapshot_RoundTripsWithoutDataLoss()
        {
            SaveSnapshot source = CreateSnapshot();

            byte[] compressed = _serializer.Serialize(source);
            SaveSnapshot restored = _serializer.Deserialize(compressed);

            Assert.That(restored.FormatVersion, Is.EqualTo(source.FormatVersion));
            Assert.That(restored.MapSeed, Is.EqualTo(ulong.MaxValue));
            Assert.That(restored.CurrentTick, Is.EqualTo(source.CurrentTick));
            Assert.That(restored.MapSize, Is.EqualTo(source.MapSize));
            CollectionAssert.AreEqual(source.TerrainTypeIds.ToArray(), restored.TerrainTypeIds.ToArray());
            CollectionAssert.AreEqual(source.ObjectIds.ToArray(), restored.ObjectIds.ToArray());
            CollectionAssert.AreEqual(source.OwnerIds.ToArray(), restored.OwnerIds.ToArray());
            CollectionAssert.AreEqual(source.RoadTypeIds.ToArray(), restored.RoadTypeIds.ToArray());
            CollectionAssert.AreEqual(source.TownAnchors, restored.TownAnchors);
            CollectionAssert.AreEqual(source.Industries, restored.Industries);
        }

        [Test]
        public void Deserialize_OlderSnapshot_RunsMigrationPipeline()
        {
            SaveSnapshot source = CreateSnapshot();
            byte[] compressed = _serializer.Serialize(source);
            int nextVersion = SaveSnapshot.CurrentFormatVersion + 1;
            var migrationPipeline = new SaveMigrationPipeline(
                nextVersion,
                new ISaveMigration[] { new PromoteVersionMigration() });
            var serializer = new JsonGzipSaveSerializer(migrationPipeline);

            SaveSnapshot restored = serializer.Deserialize(compressed);

            Assert.That(restored.FormatVersion, Is.EqualTo(nextVersion));
            Assert.That(restored.MapSeed, Is.EqualTo(source.MapSeed));
            Assert.That(restored.CurrentTick, Is.EqualTo(source.CurrentTick));
            CollectionAssert.AreEqual(source.RoadTypeIds.ToArray(), restored.RoadTypeIds.ToArray());
        }

        [Test]
        public void Deserialize_CorruptGzip_ThrowsExplicitFormatException()
        {
            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => _serializer.Deserialize(new byte[] { 1, 2, 3, 4 }))!;

            StringAssert.Contains("gzip", exception.Message);
        }

        [Test]
        public void Deserialize_MalformedJson_ThrowsExplicitFormatException()
        {
            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => _serializer.Deserialize(Compress("{")))!;

            StringAssert.Contains("not valid JSON", exception.Message);
        }

        [Test]
        public void Deserialize_IncompleteSnapshot_ThrowsExplicitFormatException()
        {
            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => _serializer.Deserialize(Compress("{\"formatVersion\":1,\"mapSize\":128}")))!;

            StringAssert.Contains("incomplete", exception.Message);
        }

        [Test]
        public void Deserialize_UnsupportedVersion_ThrowsExplicitFormatException()
        {
            int unsupportedVersion = SaveSnapshot.CurrentFormatVersion + 1;
            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => _serializer.Deserialize(Compress("{\"formatVersion\":" + unsupportedVersion + "}")))!;

            StringAssert.Contains("newer than supported", exception.Message);
        }

        [Test]
        public void Deserialize_InvalidSeed_ThrowsExplicitFormatException()
        {
            string json = JsonUtility.ToJson(new InvalidSeedDocument());

            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => _serializer.Deserialize(Compress(json)))!;

            StringAssert.Contains("invalid map seed", exception.Message);
        }

        private static SaveSnapshot CreateSnapshot()
        {
            const int sideLength = (int)MapSize.Small;
            int tileCount = sideLength * sideLength;
            var terrain = new int[tileCount];
            var objects = new int[tileCount];
            var owners = new int[tileCount];
            var roads = new int[tileCount];
            Array.Fill(objects, TileMap.NoObjectId);
            Array.Fill(owners, TileMap.UnownedId);
            Array.Fill(roads, TileMap.NoRoadTypeId);
            var towns = new[] { new TileCoordinate(2, 3) };
            objects[(towns[0].Y * sideLength) + towns[0].X] = 0;
            var industries = new[] { new PlacedIndustry("forest", 0, new TileCoordinate(5, 7)) };
            objects[(industries[0].Coordinate.Y * sideLength) + industries[0].Coordinate.X] = 1;
            terrain[(4 * sideLength) + 8] = 2;
            owners[(4 * sideLength) + 8] = 9;
            roads[(6 * sideLength) + 6] = TileMap.BasicRoadTypeId;

            return new SaveSnapshot(
                SaveSnapshot.CurrentFormatVersion,
                ulong.MaxValue,
                new Tick(1234567),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                towns,
                industries);
        }

        private static byte[] Compress(string json)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    gzip.Write(data, 0, data.Length);
                }

                return output.ToArray();
            }
        }

        [Serializable]
        private sealed class InvalidSeedDocument
        {
            public int formatVersion = SaveSnapshot.CurrentFormatVersion;
            public string mapSeed = "not-a-number";
            public string currentTick = "0";
            public int mapSize = (int)MapSize.Small;
            public int[] terrainTypeIds = new int[(int)MapSize.Small * (int)MapSize.Small];
            public int[] objectIds = CreateEmptyObjects();
            public int[] ownerIds = CreateEmptyObjects();
            public int[] roadTypeIds = CreateEmptyRoads();
            public SavedCoordinate[] townAnchors = Array.Empty<SavedCoordinate>();
            public SavedIndustry[] industries = Array.Empty<SavedIndustry>();

            private static int[] CreateEmptyObjects()
            {
                var values = new int[(int)MapSize.Small * (int)MapSize.Small];
                Array.Fill(values, TileMap.NoObjectId);
                return values;
            }

            private static int[] CreateEmptyRoads()
            {
                var values = new int[(int)MapSize.Small * (int)MapSize.Small];
                Array.Fill(values, TileMap.NoRoadTypeId);
                return values;
            }
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

        private sealed class PromoteVersionMigration : ISaveMigration
        {
            public int FromVersion
            {
                get
                {
                    return SaveSnapshot.CurrentFormatVersion;
                }
            }

            public SaveSnapshot Migrate(SaveSnapshot snapshot)
            {
                return snapshot.WithFormatVersion(FromVersion + 1);
            }
        }
    }
}
