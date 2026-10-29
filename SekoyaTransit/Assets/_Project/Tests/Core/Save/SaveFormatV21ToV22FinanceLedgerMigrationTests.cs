#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV21ToV22FinanceLedgerMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV22WithAnEmptyLedger()
        {
            SaveSnapshot v21 = CreateV21Snapshot(64001UL);

            SaveSnapshot migrated = new SaveFormatV21ToV22FinanceLedgerMigration().Migrate(v21);

            Assert.That(migrated.FormatVersion, Is.EqualTo(22));
            Assert.That(migrated.HasFinanceLedger, Is.True);
            Assert.That(migrated.FinanceLedger.Year, Is.Zero);
            Assert.That(migrated.FinanceLedger.Current.IsEmpty, Is.True);
            Assert.That(migrated.FinanceLedger.Previous.IsEmpty, Is.True);
        }

        [Test]
        public void Pipeline_MigratesV21ToCurrentVersion()
        {
            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(CreateV21Snapshot(64002UL));

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.FinanceLedger.Current.IsEmpty, Is.True);
        }

        [Test]
        public void Migrate_KeepsCashAndContracts()
        {
            SaveSnapshot v21 = CreateV21Snapshot(64003UL);

            SaveSnapshot migrated = new SaveFormatV21ToV22FinanceLedgerMigration().Migrate(v21);

            Assert.That(migrated.CompanyCash, Is.EqualTo(v21.CompanyCash));
            Assert.That(migrated.NextContractId, Is.EqualTo(v21.NextContractId));
            Assert.That(migrated.ContractRandomState.State, Is.EqualTo(v21.ContractRandomState.State));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot current = SaveSnapshot.Capture(64004UL, Tick.Zero, CreateWorld(64004UL));

            Assert.Throws<SaveFormatException>(() => new SaveFormatV21ToV22FinanceLedgerMigration().Migrate(current));
        }

        [Test]
        public void Constructor_BelowV22WithLedger_Throws()
        {
            GeneratedWorld world = CreateWorld(64005UL);
            world.Finance.AdvanceTo(1900);

            Assert.Throws<ArgumentException>(() => Rebuild(CreateV21Snapshot(64005UL), 21, world.Finance));
            Assert.DoesNotThrow(() => Rebuild(CreateV21Snapshot(64005UL), 22, world.Finance));
        }

        [Test]
        public void WithFormatVersion_DowngradeDropsTheLedger()
        {
            GeneratedWorld world = CreateWorld(64006UL);
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(5L, FinanceCategory.DeliveryIncome);
            SaveSnapshot current = SaveSnapshot.Capture(64006UL, Tick.Zero, world);

            SaveSnapshot older = current.WithFormatVersion(21);

            Assert.That(older.HasFinanceLedger, Is.False);
            Assert.That(older.FinanceLedger.Current.IsEmpty, Is.True);
        }

        private static SaveSnapshot Rebuild(SaveSnapshot source, int formatVersion, FinanceLedger ledger)
        {
            return new SaveSnapshot(
                formatVersion,
                source.MapSeed,
                source.CurrentTick,
                source.MapSize,
                source.TerrainTypeIds.ToArray(),
                source.ObjectIds.ToArray(),
                source.OwnerIds.ToArray(),
                source.RoadTypeIds.ToArray(),
                source.RailTypeIds.ToArray(),
                source.TownAnchors,
                source.Industries,
                source.RoadStations,
                source.RoadVehicles,
                source.CompanyCash,
                source.RailStations,
                source.RailVehicles,
                source.Towns,
                unlockedVehicleTypeIds: source.UnlockedVehicleTypeIds,
                financeLedger: ledger);
        }

        private static GeneratedWorld CreateWorld(ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, seed);
        }

        private static SaveSnapshot CreateV21Snapshot(ulong seed)
        {
            return SaveSnapshot.Capture(seed, Tick.Zero, CreateWorld(seed)).WithFormatVersion(21);
        }
    }
}
