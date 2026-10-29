#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV22ToV23TownNamesNeedPlansMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV23AndKeepsIndustryStock()
        {
            GeneratedWorld world = CreateWorld(65001UL);
            world.Industries[0].TryAddCargo("wood", 4);
            SaveSnapshot v22 = SaveSnapshot.Capture(65001UL, Tick.Zero, world).WithFormatVersion(22);

            SaveSnapshot migrated = new SaveFormatV22ToV23TownNamesNeedPlansMigration().Migrate(v22);

            Assert.That(migrated.FormatVersion, Is.EqualTo(23));
            Assert.That(migrated.Industries[0].Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(migrated.Industries[0].InputLeftover.IsEmpty, Is.True);
        }

        [Test]
        public void Pipeline_MigratesV22ToCurrentVersion()
        {
            SaveSnapshot v22 = SaveSnapshot.Capture(65002UL, Tick.Zero, CreateWorld(65002UL)).WithFormatVersion(22);

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v22);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot current = SaveSnapshot.Capture(65003UL, Tick.Zero, CreateWorld(65003UL));

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV22ToV23TownNamesNeedPlansMigration().Migrate(current));
        }

        [Test]
        public void WithFormatVersion_DowngradeDropsTheInputLeftover()
        {
            GeneratedWorld world = CreateWorld(65004UL);
            world.Industries[0].InputLeftover.Add("wood", 1);
            SaveSnapshot current = SaveSnapshot.Capture(65004UL, Tick.Zero, world);

            SaveSnapshot older = current.WithFormatVersion(22);

            Assert.That(current.Industries[0].InputLeftover.GetAmount("wood"), Is.EqualTo(1));
            Assert.That(older.Industries[0].InputLeftover.IsEmpty, Is.True);
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
