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
    public sealed class SaveFormatV29ToV30IndustryLastMonthMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV30AndKeepsIndustries()
        {
            GeneratedWorld world = CreateWorld(24281UL);
            SaveSnapshot v29 = SaveSnapshot.Capture(24281UL, Tick.Zero, world).WithFormatVersion(29);

            SaveSnapshot migrated = new SaveFormatV29ToV30IndustryLastMonthMigration().Migrate(v29);

            Assert.That(migrated.FormatVersion, Is.EqualTo(30));
            Assert.That(migrated.Industries, Has.Count.EqualTo(v29.Industries.Count));
            Assert.That(migrated.Industries[0].LastMonthProduced, Is.Zero);
            Assert.That(migrated.Industries[0].LastMonthTransported, Is.Zero);
        }

        [Test]
        public void Pipeline_MigratesV29ToCurrent()
        {
            SaveSnapshot v29 = SaveSnapshot.Capture(24282UL, Tick.Zero, CreateWorld(24282UL)).WithFormatVersion(29);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(v29);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v28 = SaveSnapshot.Capture(24283UL, Tick.Zero, CreateWorld(24283UL)).WithFormatVersion(28);

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV29ToV30IndustryLastMonthMigration().Migrate(v28));
        }

        private static GeneratedWorld CreateWorld(ulong seed)
        {
            var catalog = new ContentCatalog(
                new[] { new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 3, 1) },
                new[]
                {
                    new IndustryDefinition(
                        "forest",
                        "industry.forest",
                        1,
                        Array.Empty<CargoAmountDefinition>(),
                        new[] { new CargoAmountDefinition("wood", 4) })
                });
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                5,
                rules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }
    }
}
