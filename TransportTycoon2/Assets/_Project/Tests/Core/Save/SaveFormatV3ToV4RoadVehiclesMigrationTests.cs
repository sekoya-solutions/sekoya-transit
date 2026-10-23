#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV3ToV4RoadVehiclesMigrationTests
    {
        [Test]
        public void Migrate_VersionThree_AddsEmptyVehiclesAtVersionFour()
        {
            SaveSnapshot versionThree = CreateVersionThreeSnapshot();
            var migration = new SaveFormatV3ToV4RoadVehiclesMigration();

            SaveSnapshot migrated = migration.Migrate(versionThree);

            Assert.That(migrated.FormatVersion, Is.EqualTo(4));
            Assert.That(migrated.HasRoadVehicles, Is.True);
            Assert.That(migrated.RoadVehicles, Is.Empty);
            CollectionAssert.AreEqual(versionThree.RoadStations, migrated.RoadStations);
        }

        [Test]
        public void CreatePipeline_MigratesVersionThreeToCurrentWithEmptyVehicles()
        {
            SaveSnapshot versionThree = CreateVersionThreeSnapshot();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionThree);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.HasRoadVehicles, Is.True);
            Assert.That(migrated.RoadVehicles, Is.Empty);
        }

        private static SaveSnapshot CreateVersionThreeSnapshot()
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
                3,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                null,
                towns,
                Array.Empty<PlacedIndustry>(),
                Array.Empty<RoadStation>());
        }
    }
}
