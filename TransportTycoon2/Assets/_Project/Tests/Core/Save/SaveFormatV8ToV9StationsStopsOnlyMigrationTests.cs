#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV8ToV9StationsStopsOnlyMigrationTests
    {
        [Test]
        public void Migrate_BumpsVersionEightToNine()
        {
            SaveSnapshot versionEight = CreateVersionEightSnapshot();
            var migration = new SaveFormatV8ToV9StationsStopsOnlyMigration();

            SaveSnapshot migrated = migration.Migrate(versionEight);

            Assert.That(migrated.FormatVersion, Is.EqualTo(9));
            Assert.That(migrated.Industries[0].LoadRoundRobinIndex, Is.EqualTo(0));
        }

        [Test]
        public void CreatePipeline_MigratesVersionEightToCurrent()
        {
            SaveSnapshot versionEight = CreateVersionEightSnapshot();

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionEight);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        private static SaveSnapshot CreateVersionEightSnapshot()
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
            var industries = new[]
            {
                new PlacedIndustry("forest", 0, new TileCoordinate(10, 10))
            };
            objects[(10 * sideLength) + 10] = 1;
            var stations = new[] { new RoadStation(0, new TileCoordinate(4, 4)) };

            return new SaveSnapshot(
                8,
                99UL,
                new Tick(3),
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                towns,
                industries,
                stations,
                Array.Empty<RoadVehicle>(),
                companyCash: 1000L);
        }
    }
}
