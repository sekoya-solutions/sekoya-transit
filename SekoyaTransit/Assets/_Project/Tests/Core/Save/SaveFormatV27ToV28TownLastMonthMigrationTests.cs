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
    public sealed class SaveFormatV27ToV28TownLastMonthMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV28AndKeepsTowns()
        {
            GeneratedWorld world = CreateWorld(24281UL);
            SaveSnapshot v27 = SaveSnapshot.Capture(24281UL, Tick.Zero, world).WithFormatVersion(27);

            SaveSnapshot migrated = new SaveFormatV27ToV28TownLastMonthMigration().Migrate(v27);

            Assert.That(migrated.FormatVersion, Is.EqualTo(28));
            Assert.That(migrated.Towns, Has.Count.EqualTo(v27.Towns.Count));
            Assert.That(migrated.Towns[0].Population, Is.EqualTo(v27.Towns[0].Population));
        }

        [Test]
        public void Pipeline_MigratesV27ToCurrent()
        {
            SaveSnapshot v27 = SaveSnapshot.Capture(24282UL, Tick.Zero, CreateWorld(24282UL)).WithFormatVersion(27);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(v27);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v26 = SaveSnapshot.Capture(24283UL, Tick.Zero, CreateWorld(24283UL)).WithFormatVersion(26);

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV27ToV28TownLastMonthMigration().Migrate(v26));
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
                4,
                rules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }
    }
}
