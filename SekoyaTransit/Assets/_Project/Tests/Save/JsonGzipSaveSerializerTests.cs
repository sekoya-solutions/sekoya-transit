#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using UnityEngine;

namespace SekoyaTransit.Save.Tests
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
            Assert.That(restored.Industries[0].ProductionLevel, Is.EqualTo(3));
            Assert.That(restored.Industries[0].ServedMonths, Is.EqualTo(5));
            Assert.That(restored.RoadVehicles[0].CargoOriginTile, Is.EqualTo(new TileCoordinate(3, 3)));
            Assert.That(restored.RoadVehicles[0].RouteId, Is.EqualTo(1));
            RouteSaveData routes = restored.RouteData;
            Assert.That(routes.NextRouteId, Is.EqualTo(4));
            Assert.That(routes.LastUpkeepDay, Is.EqualTo(17));
            Assert.That(routes.Routes.Count, Is.EqualTo(1));
            Route route = routes.Routes[0];
            Assert.That(route.Name, Is.EqualTo("Wheat to Rome"));
            Assert.That(route.Mode, Is.EqualTo(RouteMode.Road));
            CollectionAssert.AreEqual(new[] { new RouteStop(0, true), new RouteStop(1) }, route.Stops);
            Assert.That(route.CurrentMonthRunningCost, Is.EqualTo(25L));
            Assert.That(route.CurrentMonthRevenue, Is.EqualTo(4_000_000_000L));
            Assert.That(route.HasLastMonth, Is.True);
            Assert.That(route.LastMonthRunningCost, Is.EqualTo(150L));
            Assert.That(route.LastMonthRevenue, Is.EqualTo(300L));
            Assert.That(routes.UnassignedRoadVehicles.Count, Is.EqualTo(1));
            Assert.That(routes.UnassignedRoadVehicles[0].Id, Is.EqualTo(2));
            Assert.That(routes.UnassignedRoadVehicles[0].LifetimeDeliveryIncome, Is.EqualTo(777L));
            Assert.That(routes.UnassignedRoadVehicles[0].RouteId, Is.EqualTo(RoadVehicle.NoRouteId));
            CollectionAssert.AreEqual(source.RoadStations, restored.RoadStations);
            Assert.That(restored.HasAchievementProgress, Is.True);
            CollectionAssert.AreEqual(source.AchievementCompletions, restored.AchievementCompletions);
            CollectionAssert.AreEqual(source.CargoDeliveryCounters, restored.CargoDeliveryCounters);
            Assert.That(restored.AchievementCompletions[1].Date, Is.EqualTo(new GameDate(1902, 1, 2)));
            Assert.That(restored.HasContracts, Is.True);
            Assert.That(restored.ContractList, Has.Count.EqualTo(3));
            Assert.That(restored.ContractList[0].Id, Is.EqualTo(3));
            Assert.That(restored.ContractList[0].Delivered, Is.EqualTo(4_000_000_000L));
            Assert.That(restored.ContractList[0].Status, Is.EqualTo(ContractStatus.Accepted));
            Assert.That(restored.ContractList[0].TownIndex, Is.EqualTo(0));
            Assert.That(restored.ContractList[0].StartDate, Is.EqualTo(new GameDate(1903, 5, 17)));
            Assert.That(restored.ContractList[0].Deadline, Is.EqualTo(new GameDate(1904, 5, 17)));
            Assert.That(restored.ContractList[0].SettledDate, Is.EqualTo(new GameDate(1903, 5, 17)));
            Assert.That(restored.ContractList[1].Status, Is.EqualTo(ContractStatus.Failed));
            Assert.That(restored.ContractList[1].SettledDate, Is.EqualTo(new GameDate(1904, 6, 2)));
            Assert.That(restored.ContractList[2].Status, Is.EqualTo(ContractStatus.Proposal));
            Assert.That(restored.NextContractId, Is.EqualTo(9));
            Assert.That(restored.ContractRandomState.State, Is.EqualTo(source.ContractRandomState.State));
            Assert.That(restored.ContractRandomState.Increment, Is.EqualTo(source.ContractRandomState.Increment));
            Assert.That(restored.HasFinanceLedger, Is.True);
            Assert.That(restored.FinanceLedger.Year, Is.EqualTo(1903));
            CollectionAssert.AreEqual(source.FinanceLedger.Current.ToArray(), restored.FinanceLedger.Current.ToArray());
            CollectionAssert.AreEqual(source.FinanceLedger.Previous.ToArray(), restored.FinanceLedger.Previous.ToArray());
            Assert.That(restored.HasCompanyStatistics, Is.True);
            Assert.That(restored.CompanyStatistics.LifetimeIncome, Is.EqualTo(9_000_000_000L));
            Assert.That(restored.CompanyStatistics.ContractsCompleted, Is.EqualTo(12L));
            Assert.That(restored.CompanyStatistics.RoadTilesTravelled, Is.EqualTo(345_678L));
            Assert.That(restored.CompanyStatistics.RailTilesTravelled, Is.EqualTo(90_123L));
            Assert.That(restored.FinanceLedger.Current.Get(FinanceCategory.RunningCosts), Is.EqualTo(7_000_000_000L));
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
                Assert.That(actual.CargoOriginTile, Is.EqualTo(expected.CargoOriginTile));
                Assert.That(actual.DeliveryPayRemainder, Is.EqualTo(expected.DeliveryPayRemainder));
            }
        }

        [Test]
        public void SerializeAndDeserialize_TownNamesPlansAndInputLeftover_RoundTrip()
        {
            var catalog = new ContentCatalog(
                new[]
                {
                    new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                    new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2),
                    new CargoDefinition("furniture", "cargo.furniture", CargoClass.Cargo, 40, 3),
                },
                new[]
                {
                    new IndustryDefinition(
                        "forest",
                        "industry.forest",
                        1,
                        Array.Empty<CargoAmountDefinition>(),
                        new[] { new CargoAmountDefinition("wood", 4) }),
                });
            TownNeedsSettings placeholder = TownNeedsSettings.CreatePlaceholder();
            var townNeeds = new TownNeedsSettings(
                placeholder.MinStartingPopulation,
                placeholder.MaxStartingPopulation,
                placeholder.MaxPopulation,
                new[] { new TownNeedTier(0, 1, 0, 0), new TownNeedTier(5000, 1, 1, 1) },
                names: new[] { "Paris", "Rome", "Oslo", "Lima" });
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 3, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 66001UL, townNeeds, catalog);
            world.Industries[0].InputLeftover.Add("wood", 1);
            SaveSnapshot source = SaveSnapshot.Capture(world.MapSeed, Tick.Zero, world);

            SaveSnapshot restored = _serializer.Deserialize(_serializer.Serialize(source));

            Assert.That(restored.Towns, Has.Count.EqualTo(source.Towns.Count));
            for (int index = 0; index < source.Towns.Count; index++)
            {
                Assert.That(restored.Towns[index].Name, Is.EqualTo(source.Towns[index].Name).And.Not.Empty);
                for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
                {
                    CollectionAssert.AreEqual(
                        source.Towns[index].NeedPlan.GetLevel(level),
                        restored.Towns[index].NeedPlan.GetLevel(level));
                }
            }

            Assert.That(restored.Industries[0].InputLeftover.GetAmount("wood"), Is.EqualTo(1));
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
        public void Deserialize_BeforeFormat27_IsRefusedAsTooOld()
        {
            SaveSnapshot source = CreateSnapshot();
            string json = Decompress(_serializer.Serialize(source));
            string asV26 = json.Replace(
                "\"formatVersion\":" + SaveSnapshot.CurrentFormatVersion,
                "\"formatVersion\":26");
            Assert.That(asV26, Is.Not.EqualTo(json));

            Assert.Throws<SaveFormatTooOldException>(() => _serializer.Deserialize(Compress(asV26)));
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
                new PlacedIndustry("forest", 0, new TileCoordinate(5, 7), null, 40L, 28L, 1900, 3, 0, null, 3, 5)
            };
            objects[(industries[0].Coordinate.Y * sideLength) + industries[0].Coordinate.X] = 1;
            terrain[(4 * sideLength) + 8] = 2;
            owners[(4 * sideLength) + 8] = 9;
            roads[(6 * sideLength) + 6] = TileMap.BasicRoadTypeId;
            var roadStations = new[] { new RoadStation(0, new TileCoordinate(3, 3)), new RoadStation(1, new TileCoordinate(9, 9)) };
            var roadVehicles = new[]
            {
                new RoadVehicle(
                    0,
                    RoadVehicle.StarterTypeId,
                    new[] { new RoadVehicleOrder(0, waitForFull: true), new RoadVehicleOrder(1, waitForFull: false) },
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
                    lifetimeDeliveryIncome: 12345L,
                    cargoOriginTile: new TileCoordinate(3, 3),
                    deliveryPayRemainder: 17,
                    routeId: 1)
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
                contractList: new[]
                {
                    new Contract(
                        3,
                        "bread",
                        0,
                        120,
                        4_000_000_000L,
                        ContractStatus.Accepted,
                        new GameDate(1903, 5, 17),
                        new GameDate(1904, 5, 17),
                        new GameDate(1903, 5, 17)),
                    new Contract(
                        5,
                        "wood",
                        0,
                        60,
                        10L,
                        ContractStatus.Failed,
                        new GameDate(1903, 6, 1),
                        new GameDate(1904, 6, 1),
                        new GameDate(1904, 6, 2)),
                    new Contract(
                        7,
                        "wood",
                        1,
                        80,
                        0L,
                        ContractStatus.Proposal,
                        new GameDate(1903, 7, 1),
                        new GameDate(1903, 7, 1),
                        new GameDate(1903, 7, 1))
                },
                nextContractId: 9,
                contractRandomState: new Pcg32State(ulong.MaxValue - 2UL, 0xFFFFFFFFFFFFFFFDUL),
                financeLedger: CreateFinanceLedger(),
                companyStatistics: CreateCompanyStatistics(),
                routeData: CreateRouteData());
        }

        // Format 27 (Issue #229): one road route with last month's finance, the truck above on it, and an unassigned
        // truck with lifetime income.
        private static RouteSaveData CreateRouteData()
        {
            var route = new Route(
                1,
                "Wheat to Rome",
                RouteMode.Road,
                new[] { new RouteStop(0, waitForFull: true), new RouteStop(1) },
                25L,
                4_000_000_000L,
                true,
                150L,
                300L);
            var unassigned = new RoadVehicle(
                2,
                RoadVehicle.StarterTypeId,
                Array.Empty<RoadVehicleOrder>(),
                0,
                false,
                false,
                0,
                0,
                FixedPoint.Zero,
                lifetimeDeliveryIncome: 777L);
            return new RouteSaveData(new[] { route }, new[] { unassigned }, Array.Empty<RailVehicle>(), 4, 17);
        }

        private static CompanyStatistics CreateCompanyStatistics()
        {
            var statistics = new CompanyStatistics();
            statistics.Restore(9_000_000_000L, 12L, 345_678L, 90_123L);
            return statistics;
        }

        private static FinanceLedger CreateFinanceLedger()
        {
            var ledger = new FinanceLedger();
            ledger.Restore(
                1903,
                new FinanceYearTotals(new long[] { 4_000_000_000L, 25L, 0L, 7_000_000_000L, 1L, 2L }),
                new FinanceYearTotals(new long[] { 9L, 0L, 3L, 0L, 6L, 0L }));
            return ledger;
        }

        private static string Decompress(byte[] compressed)
        {
            using (var input = new MemoryStream(compressed))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
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
