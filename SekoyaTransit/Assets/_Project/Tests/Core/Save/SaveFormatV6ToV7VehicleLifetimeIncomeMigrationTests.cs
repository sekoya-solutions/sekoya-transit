#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV6ToV7VehicleLifetimeIncomeMigrationTests
    {
        [Test]
        public void Migrate_VersionSix_BumpsToVersionSevenPreservingZeroIncome()
        {
            SaveSnapshot versionSix = CreateVersionSixSnapshotWithVehicle();
            var migration = new SaveFormatV6ToV7VehicleLifetimeIncomeMigration();

            SaveSnapshot migrated = migration.Migrate(versionSix);

            Assert.That(migrated.FormatVersion, Is.EqualTo(7));
            Assert.That(migrated.RoadVehicles, Has.Count.EqualTo(1));
            Assert.That(migrated.RoadVehicles[0].LifetimeDeliveryIncome, Is.EqualTo(0L));
            Assert.That(migrated.CompanyCash, Is.EqualTo(GeneratedWorld.PlaceholderStartingCash));
        }

        [Test]
        public void CreatePipeline_MigratesVersionSixToCurrent()
        {
            SaveSnapshot versionSix = CreateVersionSixSnapshotWithVehicle();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionSix);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.RoadVehicles[0].LifetimeDeliveryIncome, Is.EqualTo(0L));
        }

        private static SaveSnapshot CreateVersionSixSnapshotWithVehicle()
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
            var stations = new[] { new RoadStation(0, new TileCoordinate(4, 4)) };
            var vehicles = new[]
            {
                new RoadVehicle(0, RoadVehicle.StarterTypeId, stations[0].Id)
            };

            return new SaveSnapshot(
                6,
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
                stations,
                vehicles,
                GeneratedWorld.PlaceholderStartingCash);
        }
    }
}
