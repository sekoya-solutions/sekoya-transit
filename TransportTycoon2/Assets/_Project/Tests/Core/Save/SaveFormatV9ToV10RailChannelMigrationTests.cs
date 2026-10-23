#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV9ToV10RailChannelMigrationTests
    {
        [Test]
        public void Migrate_VersionNine_AddsEmptyRailChannel()
        {
            SaveSnapshot versionNine = CreateVersionNineSnapshot();
            var migration = new SaveFormatV9ToV10RailChannelMigration();

            SaveSnapshot migrated = migration.Migrate(versionNine);

            Assert.That(migrated.FormatVersion, Is.EqualTo(10));
            Assert.That(migrated.HasRailTypeIds, Is.True);
            Assert.That(migrated.RailTypeIds.Length, Is.EqualTo(migrated.TileCount));
            for (int index = 0; index < migrated.RailTypeIds.Length; index++)
            {
                Assert.That(migrated.RailTypeIds[index], Is.EqualTo(TileMap.NoRailTypeId));
            }
        }

        [Test]
        public void Migrate_ThroughPipeline_ReachesCurrentVersion()
        {
            SaveSnapshot versionNine = CreateVersionNineSnapshot();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionNine);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.HasRailTypeIds, Is.True);
        }

        private static SaveSnapshot CreateVersionNineSnapshot()
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
                9,
                99UL,
                new Tick(3),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                null,
                towns,
                Array.Empty<PlacedIndustry>(),
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash);
        }
    }
}
