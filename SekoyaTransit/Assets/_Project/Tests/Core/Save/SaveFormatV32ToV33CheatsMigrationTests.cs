#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV32ToV33CheatsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV33WithTheYearGateInPlace()
        {
            SaveSnapshot v32 = SaveSnapshot.Capture(96001UL, Tick.Zero, CreateWorld(96001UL)).WithFormatVersion(32);

            SaveSnapshot migrated = new SaveFormatV32ToV33CheatsMigration().Migrate(v32);

            Assert.That(migrated.FormatVersion, Is.EqualTo(33));
            Assert.That(migrated.VehicleYearGateLifted, Is.False);
            Assert.That(migrated.FinanceLedger.Current.Get(FinanceCategory.Cheats), Is.Zero);
        }

        [Test]
        public void Pipeline_MigratesV32ToCurrent()
        {
            SaveSnapshot v32 = SaveSnapshot.Capture(96002UL, Tick.Zero, CreateWorld(96002UL)).WithFormatVersion(32);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(v32);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v31 = SaveSnapshot.Capture(96003UL, Tick.Zero, CreateWorld(96003UL)).WithFormatVersion(31);

            Assert.Throws<SaveFormatException>(() => new SaveFormatV32ToV33CheatsMigration().Migrate(v31));
        }

        [Test]
        public void WithFormatVersion_BelowV33_DropsTheLiftedYearGate()
        {
            GeneratedWorld world = CreateWorld(96004UL);
            new CheatUnlockAllVehicleTypesCommand(new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>())).Apply(world);
            SaveSnapshot current = SaveSnapshot.Capture(96004UL, Tick.Zero, world);

            Assert.That(current.VehicleYearGateLifted, Is.True);
            Assert.That(current.WithFormatVersion(32).VehicleYearGateLifted, Is.False);
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
