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
    public sealed class SaveFormatV33ToV34LastMonthAfterFirstCloseMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV34()
        {
            SaveSnapshot v33 = SaveSnapshot.Capture(97001UL, Tick.Zero, CreateWorld(97001UL)).WithFormatVersion(33);

            SaveSnapshot migrated = new SaveFormatV33ToV34LastMonthAfterFirstCloseMigration().Migrate(v33);

            Assert.That(migrated.FormatVersion, Is.EqualTo(34));
        }

        [Test]
        public void Pipeline_MigratesV33ToCurrent()
        {
            SaveSnapshot v33 = SaveSnapshot.Capture(97002UL, Tick.Zero, CreateWorld(97002UL)).WithFormatVersion(33);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(v33);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v32 = SaveSnapshot.Capture(97003UL, Tick.Zero, CreateWorld(97003UL)).WithFormatVersion(32);

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV33ToV34LastMonthAfterFirstCloseMigration().Migrate(v32));
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
