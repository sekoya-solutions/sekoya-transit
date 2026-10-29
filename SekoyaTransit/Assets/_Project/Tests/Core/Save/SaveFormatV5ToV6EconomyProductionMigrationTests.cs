#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV5ToV6EconomyProductionMigrationTests
    {
        [Test]
        public void Migrate_VersionFive_BumpsToVersionSixWithStartingCash()
        {
            SaveSnapshot versionFive = CreateVersionFiveSnapshot();
            var migration = new SaveFormatV5ToV6EconomyProductionMigration();

            SaveSnapshot migrated = migration.Migrate(versionFive);

            Assert.That(migrated.FormatVersion, Is.EqualTo(6));
            Assert.That(migrated.CompanyCash, Is.EqualTo(GeneratedWorld.PlaceholderStartingCash));
            Assert.That(migrated.Industries, Is.Empty);
        }

        [Test]
        public void CreatePipeline_MigratesVersionFiveToCurrentWithStartingCash()
        {
            SaveSnapshot versionFive = CreateVersionFiveSnapshotWithIndustry();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionFive);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.CompanyCash, Is.EqualTo(GeneratedWorld.PlaceholderStartingCash));
            Assert.That(migrated.Industries, Has.Count.EqualTo(1));
            Assert.That(migrated.Industries[0].Cargo.IsEmpty, Is.True);
        }

        private static SaveSnapshot CreateVersionFiveSnapshot()
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
                5,
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

        private static SaveSnapshot CreateVersionFiveSnapshotWithIndustry()
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
            var industries = new[] { new PlacedIndustry("forest", 0, new TileCoordinate(5, 7)) };
            objects[(towns[0].Y * sideLength) + towns[0].X] = 0;
            objects[(industries[0].Coordinate.Y * sideLength) + industries[0].Coordinate.X] = 1;

            return new SaveSnapshot(
                5,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                null,
                towns,
                industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>());
        }
    }
}
