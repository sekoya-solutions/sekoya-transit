#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV23ToV24DeliveryPayIndustryGrowthMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV24AndStartsIndustriesAtTheBaseLevel()
        {
            GeneratedWorld world = CreateWorld(66001UL);
            world.Industries[0].TryAddCargo("wood", 4);
            SaveSnapshot v23 = SaveSnapshot.Capture(66001UL, Tick.Zero, world).WithFormatVersion(23);

            SaveSnapshot migrated = new SaveFormatV23ToV24DeliveryPayIndustryGrowthMigration().Migrate(v23);

            Assert.That(migrated.FormatVersion, Is.EqualTo(24));
            Assert.That(migrated.Industries[0].Cargo.GetAmount("wood"), Is.EqualTo(4));
            Assert.That(migrated.Industries[0].ProductionLevel, Is.Zero);
            Assert.That(migrated.Industries[0].ServedMonths, Is.Zero);
        }

        [Test]
        public void Pipeline_RefusesV23AsTooOld()
        {
            SaveSnapshot v23 = SaveSnapshot.Capture(66002UL, Tick.Zero, CreateWorld(66002UL)).WithFormatVersion(23);

            Assert.Throws<SaveFormatTooOldException>(
                () => CurrentSaveMigrations.CreatePipeline().Migrate(v23));
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot current = SaveSnapshot.Capture(66003UL, Tick.Zero, CreateWorld(66003UL));

            Assert.Throws<SaveFormatException>(
                () => new SaveFormatV23ToV24DeliveryPayIndustryGrowthMigration().Migrate(current));
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
