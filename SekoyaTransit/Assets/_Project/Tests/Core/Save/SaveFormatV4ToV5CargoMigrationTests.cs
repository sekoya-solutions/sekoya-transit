#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
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
        public void CreatePipeline_RefusesVersionFourAsTooOld()
        {
            SaveSnapshot versionFour = CreateVersionFourSnapshotWithVehicle();

            Assert.Throws<SaveFormatTooOldException>(
                () => CurrentSaveMigrations.CreatePipeline().Migrate(versionFour));
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
                null,
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
                    RoadVehicle.StarterTypeId,
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
                null,
                towns,
                Array.Empty<PlacedIndustry>(),
                stations,
                vehicles);
        }
    }
}
