#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV7ToV8IndustryMonthStatsMigrationTests
    {
        [Test]
        public void Migrate_VersionSeven_BumpsToVersionEightWithUnsetMonthStats()
        {
            SaveSnapshot versionSeven = CreateVersionSevenSnapshotWithIndustry();
            var migration = new SaveFormatV7ToV8IndustryMonthStatsMigration();

            SaveSnapshot migrated = migration.Migrate(versionSeven);

            Assert.That(migrated.FormatVersion, Is.EqualTo(8));
            Assert.That(migrated.Industries, Has.Count.EqualTo(1));
            Assert.That(migrated.Industries[0].MonthProduced, Is.EqualTo(0L));
            Assert.That(migrated.Industries[0].MonthTransported, Is.EqualTo(0L));
            Assert.That(migrated.Industries[0].StatsYear, Is.EqualTo(0));
            Assert.That(migrated.Industries[0].StatsMonth, Is.EqualTo(0));
        }

        [Test]
        public void CreatePipeline_MigratesVersionSevenToCurrent()
        {
            SaveSnapshot versionSeven = CreateVersionSevenSnapshotWithIndustry();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionSeven);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.Industries[0].MonthProduced, Is.EqualTo(0L));
        }

        private static SaveSnapshot CreateVersionSevenSnapshotWithIndustry()
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

            return new SaveSnapshot(
                7,
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
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash);
        }
    }
}
