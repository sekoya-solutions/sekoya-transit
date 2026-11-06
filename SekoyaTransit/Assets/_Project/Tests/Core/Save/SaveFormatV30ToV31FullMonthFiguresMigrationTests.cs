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
    public sealed class SaveFormatV30ToV31FullMonthFiguresMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV31()
        {
            SaveSnapshot v30 = SaveSnapshot.Capture(94001UL, Tick.Zero, CreateWorld(94001UL)).WithFormatVersion(30);

            SaveSnapshot migrated = new SaveFormatV30ToV31FullMonthFiguresMigration().Migrate(v30);

            Assert.That(migrated.FormatVersion, Is.EqualTo(31));
        }

        [Test]
        public void Pipeline_MigratesV30ToCurrent()
        {
            SaveSnapshot v30 = SaveSnapshot.Capture(94002UL, Tick.Zero, CreateWorld(94002UL)).WithFormatVersion(30);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(v30);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v29 = SaveSnapshot.Capture(94003UL, Tick.Zero, CreateWorld(94003UL)).WithFormatVersion(29);

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV30ToV31FullMonthFiguresMigration().Migrate(v29));
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
