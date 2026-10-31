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
    public sealed class SaveFormatV24ToV25CompanyStatisticsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV25AndStartsCountersAtZero()
        {
            GeneratedWorld world = CreateWorld(67001UL);
            world.CreditCompanyCash(500L, FinanceCategory.DeliveryIncome);
            world.Statistics.AddRoadTiles(9L);
            world.RecordCargoDelivered("wood", DeliveryDestinationKind.Industry, 4);
            SaveSnapshot v24 = SaveSnapshot.Capture(67001UL, Tick.Zero, world).WithFormatVersion(24);

            SaveSnapshot migrated = new SaveFormatV24ToV25CompanyStatisticsMigration().Migrate(v24);

            Assert.That(v24.HasCompanyStatistics, Is.False);
            Assert.That(migrated.FormatVersion, Is.EqualTo(25));
            Assert.That(migrated.HasCompanyStatistics, Is.True);
            Assert.That(migrated.CompanyStatistics.LifetimeIncome, Is.Zero);
            Assert.That(migrated.CompanyStatistics.RoadTilesTravelled, Is.Zero);
            Assert.That(migrated.CargoDeliveryCounters[0].Amount, Is.EqualTo(4L));
        }

        [Test]
        public void Migrate_KeepsCompletionsOfOldAchievementIds()
        {
            GeneratedWorld world = CreateWorld(67002UL);
            var oldTrack = new AchievementDefinition(
                "town_1k",
                AchievementKind.LargestTown,
                new[]
                {
                    new AchievementMilestone(1L, "b"),
                    new AchievementMilestone(2L, "s"),
                    new AchievementMilestone(3L, "g")
                },
                "label");
            Achievements.AdvanceAll(
                world,
                new ContentCatalog(Array.Empty<CargoDefinition>(), Array.Empty<IndustryDefinition>(), achievements: new[] { oldTrack }),
                new GameDate(1901, 1, 1));
            SaveSnapshot v24 = SaveSnapshot.Capture(67002UL, Tick.Zero, world).WithFormatVersion(24);

            SaveSnapshot migrated = new SaveFormatV24ToV25CompanyStatisticsMigration().Migrate(v24);

            CollectionAssert.AreEqual(v24.AchievementCompletions, migrated.AchievementCompletions);
        }

        [Test]
        public void Pipeline_RefusesV24AsTooOld()
        {
            SaveSnapshot v24 = SaveSnapshot.Capture(67004UL, Tick.Zero, CreateWorld(67004UL)).WithFormatVersion(24);

            Assert.Throws<SaveFormatTooOldException>(
                () => CurrentSaveMigrations.CreatePipeline().Migrate(v24));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot current = SaveSnapshot.Capture(67005UL, Tick.Zero, CreateWorld(67005UL));

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV24ToV25CompanyStatisticsMigration().Migrate(current));
        }

        [Test]
        public void Constructor_StatisticsBelowV25_Throws()
        {
            SaveSnapshot v24 = SaveSnapshot.Capture(67006UL, Tick.Zero, CreateWorld(67006UL)).WithFormatVersion(24);

            Assert.Throws<ArgumentException>(
                () => new SaveSnapshot(
                    24,
                    v24.MapSeed,
                    v24.CurrentTick,
                    v24.MapSize,
                    v24.TerrainTypeIds.ToArray(),
                    v24.ObjectIds.ToArray(),
                    v24.OwnerIds.ToArray(),
                    v24.RoadTypeIds.ToArray(),
                    v24.RailTypeIds.ToArray(),
                    v24.TownAnchors,
                    v24.Industries,
                    v24.RoadStations,
                    v24.RoadVehicles,
                    v24.CompanyCash,
                    v24.RailStations,
                    v24.RailVehicles,
                    v24.Towns,
                    companyStatistics: new CompanyStatistics()));
        }

        private static GeneratedWorld CreateWorld(ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, seed);
        }
    }
}
