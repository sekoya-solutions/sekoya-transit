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
    public sealed class SaveFormatV31ToV32FinanceMonthsByModeMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV32()
        {
            SaveSnapshot v31 = SaveSnapshot.Capture(95001UL, Tick.Zero, CreateWorld(95001UL)).WithFormatVersion(31);

            SaveSnapshot migrated = new SaveFormatV31ToV32FinanceMonthsByModeMigration().Migrate(v31);

            Assert.That(migrated.FormatVersion, Is.EqualTo(32));
        }

        [Test]
        public void Pipeline_MigratesV31ToCurrent()
        {
            SaveSnapshot v31 = SaveSnapshot.Capture(95002UL, Tick.Zero, CreateWorld(95002UL)).WithFormatVersion(31);

            SaveSnapshot migrated = CurrentSaveMigrations.CreatePipeline().Migrate(v31);

            Assert.That(migrated.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot v30 = SaveSnapshot.Capture(95003UL, Tick.Zero, CreateWorld(95003UL)).WithFormatVersion(30);

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV31ToV32FinanceMonthsByModeMigration().Migrate(v30));
        }

        [Test]
        public void Migrate_KeepsYearTotalsAsGeneralAndHasNoMonthlyData()
        {
            GeneratedWorld world = CreateWorld(95004UL);
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(400L, FinanceCategory.DeliveryIncome);
            world.Finance.AdvanceTo(1901);
            world.CreditCompanyCash(30L, FinanceCategory.ContractBonus);
            SaveSnapshot v31 = SaveSnapshot.Capture(95004UL, Tick.Zero, world).WithFormatVersion(31);

            FinanceLedger ledger = new SaveFormatV31ToV32FinanceMonthsByModeMigration().Migrate(v31).FinanceLedger;

            Assert.That(ledger.Year, Is.EqualTo(1901));
            Assert.That(ledger.Current.Get(FinanceCategory.ContractBonus), Is.EqualTo(30L));
            Assert.That(ledger.Previous.Get(FinanceCategory.DeliveryIncome), Is.EqualTo(400L));
            Assert.That(ledger.Previous.Get(FinanceCategory.DeliveryIncome, FinanceMode.General), Is.EqualTo(400L));
            Assert.That(ledger.HasPreviousYear, Is.True);
            Assert.That(ledger.MonthIndex, Is.Zero);
            Assert.That(ledger.HasPreviousMonth, Is.False);
            Assert.That(ledger.HasFullCurrentMonth, Is.False);
        }

        [Test]
        public void LegacyRestore_ARestoredMidMonthSave_WaitsForTwoMonthCloses()
        {
            var ledger = new FinanceLedger();
            ledger.Restore(1901, new FinanceYearTotals(), new FinanceYearTotals());

            ledger.AdvanceToMonth(1901, 3, 12);
            ledger.AdvanceToMonth(1901, 4, 1);
            Assert.That(ledger.HasPreviousMonth, Is.False);

            ledger.AdvanceToMonth(1901, 5, 1);
            Assert.That(ledger.HasPreviousMonth, Is.True);
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
