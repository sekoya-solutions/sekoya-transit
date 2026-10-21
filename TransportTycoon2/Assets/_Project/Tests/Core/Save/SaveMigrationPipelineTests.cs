#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveMigrationPipelineTests
    {
        private static readonly int[] _expectedMigrationOrder = { 1, 2 };

        [Test]
        public void Migrate_SequentialVersions_AppliesEveryStepInOrder()
        {
            SaveSnapshot snapshot = CreateSnapshot(1);
            var order = new List<int>();
            var pipeline = new SaveMigrationPipeline(
                3,
                new ISaveMigration[]
                {
                    new RecordingMigration(1, order),
                    new RecordingMigration(2, order)
                });

            SaveSnapshot migrated = pipeline.Migrate(snapshot);

            Assert.That(migrated.FormatVersion, Is.EqualTo(3));
            CollectionAssert.AreEqual(_expectedMigrationOrder, order);
            Assert.That(migrated.MapSeed, Is.EqualTo(snapshot.MapSeed));
            Assert.That(migrated.CurrentTick, Is.EqualTo(snapshot.CurrentTick));
        }

        [Test]
        public void Migrate_CurrentVersion_ReturnsSameSnapshot()
        {
            SaveSnapshot snapshot = CreateSnapshot(1);
            var pipeline = new SaveMigrationPipeline(1, Array.Empty<ISaveMigration>());

            Assert.That(pipeline.Migrate(snapshot), Is.SameAs(snapshot));
        }

        [Test]
        public void Migrate_MissingStep_ThrowsExplicitFormatException()
        {
            var pipeline = new SaveMigrationPipeline(3, Array.Empty<ISaveMigration>());

            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => pipeline.Migrate(CreateSnapshot(1)))!;

            StringAssert.Contains("No save migration", exception.Message);
        }

        [Test]
        public void Migrate_NewerSnapshot_ThrowsExplicitFormatException()
        {
            var pipeline = new SaveMigrationPipeline(1, Array.Empty<ISaveMigration>());

            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => pipeline.Migrate(CreateSnapshot(2)))!;

            StringAssert.Contains("newer than supported", exception.Message);
        }

        [Test]
        public void Constructor_NonConsecutiveMigrationOutput_ThrowsExplicitFormatException()
        {
            var pipeline = new SaveMigrationPipeline(3, new[] { new InvalidMigration() });

            SaveFormatException exception = Assert.Throws<SaveFormatException>(
                () => pipeline.Migrate(CreateSnapshot(1)))!;

            StringAssert.Contains("must produce version 2", exception.Message);
        }

        private static SaveSnapshot CreateSnapshot(int version)
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
            var industries = new[] { new PlacedIndustry("forest", 0, new TileCoordinate(5, 7)) };
            objects[(industries[0].Coordinate.Y * sideLength) + industries[0].Coordinate.X] = 1;

            return new SaveSnapshot(
                version,
                ulong.MaxValue,
                new Tick(1234),
                MapSize.Small,
                terrain,
                objects,
                owners,
                towns,
                industries);
        }

        private sealed class RecordingMigration : ISaveMigration
        {
            private readonly List<int> _order;

            public RecordingMigration(int fromVersion, List<int> order)
            {
                FromVersion = fromVersion;
                _order = order;
            }

            public int FromVersion { get; }

            public SaveSnapshot Migrate(SaveSnapshot snapshot)
            {
                _order.Add(FromVersion);
                return snapshot.WithFormatVersion(FromVersion + 1);
            }
        }

        private sealed class InvalidMigration : ISaveMigration
        {
            public int FromVersion
            {
                get
                {
                    return 1;
                }
            }

            public SaveSnapshot Migrate(SaveSnapshot snapshot)
            {
                return snapshot.WithFormatVersion(3);
            }
        }
    }
}
