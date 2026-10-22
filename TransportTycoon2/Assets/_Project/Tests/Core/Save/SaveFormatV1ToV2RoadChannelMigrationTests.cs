#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV1ToV2RoadChannelMigrationTests
    {
        [Test]
        public void Migrate_VersionOne_AddsEmptyRoadChannelAtVersionTwo()
        {
            SaveSnapshot versionOne = CreateVersionOneSnapshot();
            var migration = new SaveFormatV1ToV2RoadChannelMigration();

            SaveSnapshot migrated = migration.Migrate(versionOne);

            Assert.That(migrated.FormatVersion, Is.EqualTo(2));
            Assert.That(migrated.HasRoadTypeIds, Is.True);
            Assert.That(migrated.RoadTypeIds.Length, Is.EqualTo(migrated.TileCount));
            for (int index = 0; index < migrated.RoadTypeIds.Length; index++)
            {
                Assert.That(migrated.RoadTypeIds[index], Is.EqualTo(TileMap.NoRoadTypeId));
            }

            CollectionAssert.AreEqual(versionOne.ObjectIds.ToArray(), migrated.ObjectIds.ToArray());
        }

        [Test]
        public void CreatePipeline_MigratesVersionOneToCurrent()
        {
            SaveSnapshot versionOne = CreateVersionOneSnapshot();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionOne);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.HasRoadTypeIds, Is.True);
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
    }
}
