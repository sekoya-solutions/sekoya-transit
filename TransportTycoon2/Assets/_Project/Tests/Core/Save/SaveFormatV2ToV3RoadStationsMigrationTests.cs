#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV2ToV3RoadStationsMigrationTests
    {
        [Test]
        public void Migrate_VersionTwo_AddsEmptyStationsAtVersionThree()
        {
            SaveSnapshot versionTwo = CreateVersionTwoSnapshot();
            var migration = new SaveFormatV2ToV3RoadStationsMigration();

            SaveSnapshot migrated = migration.Migrate(versionTwo);

            Assert.That(migrated.FormatVersion, Is.EqualTo(3));
            Assert.That(migrated.HasRoadStations, Is.True);
            Assert.That(migrated.RoadStations, Is.Empty);
            CollectionAssert.AreEqual(versionTwo.RoadTypeIds.ToArray(), migrated.RoadTypeIds.ToArray());
        }

        [Test]
        public void CreatePipeline_MigratesVersionOneToCurrentWithEmptyStations()
        {
            SaveSnapshot versionOne = CreateVersionOneSnapshot();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionOne);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.HasRoadTypeIds, Is.True);
            Assert.That(migrated.HasRoadStations, Is.True);
            Assert.That(migrated.RoadStations, Is.Empty);
        }

        private static SaveSnapshot CreateVersionOneSnapshot()
        {
            const int sideLength = (int)MapSize.Small;
            int tileCount = sideLength * sideLength;
            var terrain = new int[tileCount];
            var objects = new int[tileCount];
            var owners = new int[tileCount];
            Array.Fill(objects, TileMap.NoObjectId);
            Array.Fill(owners, TileMap.UnownedId);
            var towns = new[] { new TileCoordinate(2, 3) };
            objects[(towns[0].Y * sideLength) + towns[0].X] = 0;

            return new SaveSnapshot(
                1,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                null,
                towns,
                Array.Empty<PlacedIndustry>());
        }

        private static SaveSnapshot CreateVersionTwoSnapshot()
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

            return new SaveSnapshot(
                2,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                towns,
                Array.Empty<PlacedIndustry>());
        }
    }
}
