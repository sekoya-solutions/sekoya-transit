#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV10ToV11RailStationsVehiclesMigrationTests
    {
        [Test]
        public void Migrate_VersionTen_AddsEmptyRailStationsAndVehiclesAtVersionEleven()
        {
            SaveSnapshot versionTen = CreateVersionTenSnapshot();
            var migration = new SaveFormatV10ToV11RailStationsVehiclesMigration();

            SaveSnapshot migrated = migration.Migrate(versionTen);

            Assert.That(migrated.FormatVersion, Is.EqualTo(11));
            Assert.That(migrated.HasRailStations, Is.True);
            Assert.That(migrated.RailStations, Is.Empty);
            Assert.That(migrated.HasRailVehicles, Is.True);
            Assert.That(migrated.RailVehicles, Is.Empty);
            Assert.That(migrated.HasRailTypeIds, Is.True);
        }

        [Test]
        public void CreatePipeline_MigratesVersionTenToCurrentWithEmptyRailEntities()
        {
            SaveSnapshot versionTen = CreateVersionTenSnapshot();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionTen);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.HasRailStations, Is.True);
            Assert.That(migrated.RailStations, Is.Empty);
            Assert.That(migrated.HasRailVehicles, Is.True);
            Assert.That(migrated.RailVehicles, Is.Empty);
        }

        private static SaveSnapshot CreateVersionTenSnapshot()
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

            return new SaveSnapshot(
                10,
                99UL,
                new Tick(7),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                rails,
                towns,
                Array.Empty<PlacedIndustry>(),
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash);
        }
    }
}
