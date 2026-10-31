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
    public sealed class SaveFormatV25ToV26ContractProposalsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV26AndKeepsTheContractCounterAndGenerator()
        {
            GeneratedWorld world = CreateWorld(68001UL);
            SaveSnapshot v25 = SaveSnapshot.Capture(68001UL, Tick.Zero, world).WithFormatVersion(25);

            SaveSnapshot migrated = new SaveFormatV25ToV26ContractProposalsMigration().Migrate(v25);

            Assert.That(migrated.FormatVersion, Is.EqualTo(26));
            Assert.That(migrated.NextContractId, Is.EqualTo(v25.NextContractId));
            Assert.That(migrated.ContractRandomState, Is.EqualTo(v25.ContractRandomState));
            Assert.That(migrated.ContractList, Is.Empty);
        }

        [Test]
        public void Pipeline_RefusesV25AsTooOld()
        {
            SaveSnapshot v25 = SaveSnapshot.Capture(68002UL, Tick.Zero, CreateWorld(68002UL)).WithFormatVersion(25);

            Assert.Throws<SaveFormatTooOldException>(
                () => CurrentSaveMigrations.CreatePipeline().Migrate(v25));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v24 = SaveSnapshot.Capture(68003UL, Tick.Zero, CreateWorld(68003UL)).WithFormatVersion(24);

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV25ToV26ContractProposalsMigration().Migrate(v24));
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
