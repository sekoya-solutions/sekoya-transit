#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV19ToV20AchievementsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV20WithNothingCompletedAndZeroCounters()
        {
            SaveSnapshot v19 = CreateV19Snapshot(62001UL);

            SaveSnapshot migrated = new SaveFormatV19ToV20AchievementsMigration().Migrate(v19);

            Assert.That(migrated.FormatVersion, Is.EqualTo(20));
            Assert.That(migrated.HasAchievementProgress, Is.True);
            Assert.That(migrated.AchievementCompletions, Is.Empty);
            Assert.That(migrated.CargoDeliveryCounters, Is.Empty);
        }

        [Test]
        public void Pipeline_MigratesV19ToCurrentVersion()
        {
            SaveSnapshot v19 = CreateV19Snapshot(62002UL);

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v19);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.AchievementCompletions, Is.Empty);
            Assert.That(current.CargoDeliveryCounters, Is.Empty);
        }

        [Test]
        public void Constructor_BelowV20WithAchievementProgress_Throws()
        {
            SaveSnapshot v19 = CreateV19Snapshot(62003UL);

            Assert.Throws<ArgumentException>(
                () => new SaveSnapshot(
                    19,
                    v19.MapSeed,
                    v19.CurrentTick,
                    v19.MapSize,
                    v19.TerrainTypeIds.ToArray(),
                    v19.ObjectIds.ToArray(),
                    v19.OwnerIds.ToArray(),
                    v19.RoadTypeIds.ToArray(),
                    v19.RailTypeIds.ToArray(),
                    v19.TownAnchors,
                    v19.Industries,
                    v19.RoadStations,
                    v19.RoadVehicles,
                    v19.CompanyCash,
                    v19.RailStations,
                    v19.RailVehicles,
                    v19.Towns,
                    unlockedVehicleTypeIds: v19.UnlockedVehicleTypeIds,
                    cargoDeliveryCounters: new[]
                    {
                        new CargoDeliveryCounter("wood", DeliveryDestinationKind.Town, 1L)
                    }));
        }

        private static SaveSnapshot CreateV19Snapshot(ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, seed);
            return new SaveSnapshot(
                19,
                seed,
                Tick.Zero,
                MapSize.Small,
                world.Map.TerrainTypeIds.ToArray(),
                world.Map.ObjectIds.ToArray(),
                world.Map.OwnerIds.ToArray(),
                world.Map.RoadTypeIds.ToArray(),
                world.Map.RailTypeIds.ToArray(),
                world.TownAnchors,
                world.Industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>(),
                world.Towns,
                unlockedVehicleTypeIds: Array.Empty<string>());
        }
    }
}
