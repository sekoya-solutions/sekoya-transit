#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV11ToV12RailWagonCountMigrationTests
    {
        [Test]
        public void Migrate_VersionEleven_DerivesWagonCountFromCapacity()
        {
            SaveSnapshot versionEleven = CreateVersionElevenSnapshotWithTrain(cargoCapacity: 30, cargoAmount: 12);
            var migration = new SaveFormatV11ToV12RailWagonCountMigration();

            SaveSnapshot migrated = migration.Migrate(versionEleven);

            Assert.That(migrated.FormatVersion, Is.EqualTo(12));
            Assert.That(migrated.RailVehicles, Has.Count.EqualTo(1));
            Assert.That(migrated.RailVehicles[0].WagonCount, Is.EqualTo(3));
            Assert.That(migrated.RailVehicles[0].CargoCapacity, Is.EqualTo(30));
            Assert.That(migrated.RailVehicles[0].Cargo.TotalAmount, Is.EqualTo(12));
        }

        [Test]
        public void Migrate_VersionEleven_ZeroCapacityBecomesZeroWagons()
        {
            SaveSnapshot versionEleven = CreateVersionElevenSnapshotWithTrain(cargoCapacity: 0, cargoAmount: 0);
            var migration = new SaveFormatV11ToV12RailWagonCountMigration();

            SaveSnapshot migrated = migration.Migrate(versionEleven);

            Assert.That(migrated.RailVehicles[0].WagonCount, Is.EqualTo(0));
            Assert.That(migrated.RailVehicles[0].CargoCapacity, Is.EqualTo(0));
        }

        [Test]
        public void CreatePipeline_MigratesVersionElevenToCurrent()
        {
            SaveSnapshot versionEleven = CreateVersionElevenSnapshotWithTrain(cargoCapacity: 30, cargoAmount: 0);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(versionEleven);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(migrated.RailVehicles[0].WagonCount, Is.EqualTo(3));
        }

        private static SaveSnapshot CreateVersionElevenSnapshotWithTrain(int cargoCapacity, int cargoAmount)
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

            // Build a temporary v12-shaped vehicle then force capacity for the v11 snapshot path:
            // v11 snapshots constructed in-memory may already carry wagon counts from the new ctor;
            // migration re-derives from CargoCapacity.
            int wagonCount = cargoCapacity <= 0 ? 0 : cargoCapacity / RailVehicle.LegacyCapacityPerWagon;
            CargoAmount[]? cargo = null;
            if (cargoAmount > 0)
            {
                cargo = new[] { new CargoAmount("wood", cargoAmount) };
            }

            var vehicle = new RailVehicle(
                id: 0,
                typeId: RailVehicle.StarterTypeId,
                orders: Array.Empty<RailVehicleOrder>(),
                orderIndex: 0,
                isStuck: false,
                isOnEdge: false,
                edgeFromNodeId: 0,
                edgeToNodeId: 0,
                progressAlongEdge: FixedPoint.Zero,
                cargo: cargo,
                wagonCount: wagonCount,
                cargoCapacity: wagonCount * RailVehicle.LegacyCapacityPerWagon);

            return new SaveSnapshot(
                11,
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
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                new[] { vehicle });
        }
    }
}
