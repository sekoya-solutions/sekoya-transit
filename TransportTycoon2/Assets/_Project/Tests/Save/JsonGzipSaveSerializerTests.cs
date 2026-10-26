#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using TransportTycoon.Core.Content;
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
            CollectionAssert.AreEqual(source.RailTypeIds.ToArray(), restored.RailTypeIds.ToArray());
            CollectionAssert.AreEqual(source.TownAnchors, restored.TownAnchors);
            CollectionAssert.AreEqual(source.Industries, restored.Industries);
            Assert.That(restored.Industries[0].MonthProduced, Is.EqualTo(40L));
            Assert.That(restored.Industries[0].MonthTransported, Is.EqualTo(28L));
            Assert.That(restored.Industries[0].StatsYear, Is.EqualTo(1900));
            Assert.That(restored.Industries[0].StatsMonth, Is.EqualTo(3));
            CollectionAssert.AreEqual(source.RoadStations, restored.RoadStations);
            Assert.That(restored.HasAchievementProgress, Is.True);
            CollectionAssert.AreEqual(source.AchievementCompletions, restored.AchievementCompletions);
            CollectionAssert.AreEqual(source.CargoDeliveryCounters, restored.CargoDeliveryCounters);
            Assert.That(restored.AchievementCompletions[1].Date, Is.EqualTo(new GameDate(1902, 1, 2)));
            Assert.That(restored.HasContracts, Is.True);
            Assert.That(restored.OpenContracts, Has.Count.EqualTo(2));
            Assert.That(restored.OpenContracts[0].Id, Is.EqualTo(3));
            Assert.That(restored.OpenContracts[0].Delivered, Is.EqualTo(4_000_000_000L));
            Assert.That(restored.OpenContracts[0].Deadline, Is.EqualTo(new GameDate(1904, 5, 17)));
            Assert.That(restored.OpenContracts[1].TargetKind, Is.EqualTo(DeliveryDestinationKind.Industry));
            Assert.That(restored.NextContractId, Is.EqualTo(9));
            Assert.That(restored.ContractRandomState.State, Is.EqualTo(source.ContractRandomState.State));
            Assert.That(restored.ContractRandomState.Increment, Is.EqualTo(source.ContractRandomState.Increment));
            Assert.That(restored.RoadVehicles, Has.Count.EqualTo(source.RoadVehicles.Count));
            for (int index = 0; index < source.RoadVehicles.Count; index++)
            {
                RoadVehicle expected = source.RoadVehicles[index];
                RoadVehicle actual = restored.RoadVehicles[index];
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.TypeId, Is.EqualTo(expected.TypeId));
                CollectionAssert.AreEqual(expected.OrderStationIds, actual.OrderStationIds);
                Assert.That(actual.OrderIndex, Is.EqualTo(expected.OrderIndex));
                Assert.That(actual.IsStuck, Is.EqualTo(expected.IsStuck));
                Assert.That(actual.IsOnEdge, Is.EqualTo(expected.IsOnEdge));
                Assert.That(actual.EdgeFromNodeId, Is.EqualTo(expected.EdgeFromNodeId));
                Assert.That(actual.EdgeToNodeId, Is.EqualTo(expected.EdgeToNodeId));
                Assert.That(actual.ProgressAlongEdge, Is.EqualTo(expected.ProgressAlongEdge));
                Assert.That(actual.LifetimeDeliveryIncome, Is.EqualTo(expected.LifetimeDeliveryIncome));
            }
        }

        [Test]
        public void SerializeAndDeserialize_ZeroCashSnapshot_PreservesBankruptcyBalance()
        {
            SaveSnapshot source = CreateSnapshot(companyCash: 0L);

            SaveSnapshot restored = _serializer.Deserialize(_serializer.Serialize(source));

            Assert.That(restored.CompanyCash, Is.Zero);
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

        private static SaveSnapshot CreateSnapshot(long companyCash = GeneratedWorld.PlaceholderStartingCash)
        {
            const int sideLength = (int)MapSize.Small;
            int tileCount = sideLength * sideLength;
            var terrain = new int[tileCount];
            var objects = new int[tileCount];
            var owners = new int[tileCount];
            var roads = new int[tileCount];
            var rails = new int[tileCount];
            Array.Fill(objects, TileMap.NoObjectId);
            Array.Fill(owners, TileMap.UnownedId);
            Array.Fill(roads, TileMap.NoRoadTypeId);
            Array.Fill(rails, TileMap.NoRailTypeId);
            var towns = new[] { new TileCoordinate(2, 3) };
            objects[(towns[0].Y * sideLength) + towns[0].X] = 0;
            var industries = new[]
            {
                new PlacedIndustry("forest", 0, new TileCoordinate(5, 7), null, 40L, 28L, 1900, 3)
            };
            objects[(industries[0].Coordinate.Y * sideLength) + industries[0].Coordinate.X] = 1;
            terrain[(4 * sideLength) + 8] = 2;
            owners[(4 * sideLength) + 8] = 9;
            roads[(6 * sideLength) + 6] = TileMap.BasicRoadTypeId;
            var roadStations = new[] { new RoadStation(0, new TileCoordinate(3, 3)) };
            var roadVehicles = new[]
            {
                new RoadVehicle(
                    0,
                    RoadVehicle.StarterTypeId,
                    new[] { new RoadVehicleOrder(0, waitForFull: false) },
                    0,
                    false,
                    false,
                    99,
                    99,
                    FixedPoint.Zero,
                    cargo: null,
                    cargoCapacity: RoadVehicle.PlaceholderCargoCapacity,
                    isServicingStop: false,
                    isUnloadComplete: false,
                    lifetimeDeliveryIncome: 12345L)
            };

            return new SaveSnapshot(
                SaveSnapshot.CurrentFormatVersion,
                ulong.MaxValue,
                new Tick(1234567),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                rails,
                towns,
                industries,
                roadStations,
                roadVehicles,
                companyCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>(),
                achievementCompletions: new[]
                {
                    new AchievementCompletion("cash_1m", new GameDate(1903, 5, 17)),
                    new AchievementCompletion("town_1k", new GameDate(1902, 1, 2))
                },
                cargoDeliveryCounters: new[]
                {
                    new CargoDeliveryCounter("bread", DeliveryDestinationKind.Town, 4_000_000_000L),
                    new CargoDeliveryCounter("wood", DeliveryDestinationKind.Industry, 12L)
                },
                openContracts: new[]
                {
                    new Contract(
                        3,
                        "bread",
                        DeliveryDestinationKind.Town,
                        0,
                        120,
                        4_000_000_000L,
                        new GameDate(1903, 5, 17),
                        new GameDate(1904, 5, 17)),
                    new Contract(
                        5,
                        "wood",
                        DeliveryDestinationKind.Industry,
                        0,
                        60,
                        0L,
                        new GameDate(1903, 6, 1),
                        new GameDate(1904, 6, 1))
                },
                nextContractId: 9,
                contractRandomState: new Pcg32State(ulong.MaxValue - 2UL, 0xFFFFFFFFFFFFFFFDUL));
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
            public string companyCash = "0";
            public int[] terrainTypeIds = new int[(int)MapSize.Small * (int)MapSize.Small];
            public int[] objectIds = CreateEmptyObjects();
            public int[] ownerIds = CreateEmptyObjects();
            public int[] roadTypeIds = CreateEmptyRoads();
            public int[] railTypeIds = CreateEmptyRails();
            public SavedCoordinate[] townAnchors = Array.Empty<SavedCoordinate>();
            public SavedIndustry[] industries = Array.Empty<SavedIndustry>();
            public SavedRoadStation[] roadStations = Array.Empty<SavedRoadStation>();
            public SavedRoadVehicle[] roadVehicles = Array.Empty<SavedRoadVehicle>();
            public SavedRailStation[] railStations = Array.Empty<SavedRailStation>();
            public SavedRailVehicle[] railVehicles = Array.Empty<SavedRailVehicle>();

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

            private static int[] CreateEmptyRails()
            {
                var values = new int[(int)MapSize.Small * (int)MapSize.Small];
                Array.Fill(values, TileMap.NoRailTypeId);
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

        [Serializable]
        private sealed class SavedRoadStation
        {
            public int id;
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class SavedRoadVehicle
        {
            public int id;
            public int typeId;
            public int[]? orderStationIds;
            public int orderIndex;
            public bool isStuck;
            public bool isOnEdge;
            public int edgeFromNodeId;
            public int edgeToNodeId;
            public long progressAlongEdgeRaw;
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
            public int orderIndex;
            public bool isStuck;
            public bool isOnEdge;
            public int edgeFromNodeId;
            public int edgeToNodeId;
            public long progressAlongEdgeRaw;
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
