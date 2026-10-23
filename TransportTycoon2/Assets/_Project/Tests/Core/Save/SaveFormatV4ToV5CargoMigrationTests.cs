#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV4ToV5CargoMigrationTests
    {
        [Test]
        public void Migrate_VersionFour_BumpsToVersionFive()
        {
            SaveSnapshot versionFour = CreateVersionFourSnapshot();
            var migration = new SaveFormatV4ToV5CargoMigration();

            SaveSnapshot migrated = migration.Migrate(versionFour);

            Assert.That(migrated.FormatVersion, Is.EqualTo(5));
            Assert.That(migrated.HasRoadVehicles, Is.True);
            Assert.That(migrated.RoadVehicles, Is.Empty);
            Assert.That(migrated.RoadStations, Is.Empty);
        }

        [Test]
        public void CreatePipeline_MigratesVersionFourToCurrentWithEmptyCargo()
        {
            SaveSnapshot versionFour = CreateVersionFourSnapshotWithVehicle();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionFour);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.RoadVehicles, Has.Count.EqualTo(1));
            Assert.That(migrated.RoadVehicles[0].Cargo.IsEmpty, Is.True);
            Assert.That(migrated.RoadVehicles[0].Orders[0].WaitForFull, Is.False);
            Assert.That(migrated.RoadStations, Has.Count.EqualTo(1));
        }

        private static SaveSnapshot CreateVersionFourSnapshot()
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
                4,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                towns,
                Array.Empty<PlacedIndustry>(),
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>());
        }

        private static SaveSnapshot CreateVersionFourSnapshotWithVehicle()
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
            var stations = new[] { new RoadStation(0, new TileCoordinate(3, 3)) };
            var orderStationIds = new[] { 0 };
            var vehicles = new[]
            {
                new RoadVehicle(
                    0,
                    RoadVehicle.PlaceholderTypeId,
                    orderStationIds,
                    0,
                    false,
                    false,
                    99,
                    99,
                    FixedPoint.Zero)
            };

            return new SaveSnapshot(
                4,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                towns,
                Array.Empty<PlacedIndustry>(),
                stations,
                vehicles);
        }
    }
}
