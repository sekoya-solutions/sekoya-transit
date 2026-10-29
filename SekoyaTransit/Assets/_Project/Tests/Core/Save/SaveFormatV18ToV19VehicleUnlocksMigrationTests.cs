#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV18ToV19VehicleUnlocksMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV19()
        {
            GeneratedWorld world = GenerateWorld(61030UL);
            SaveSnapshot v18 = new SaveSnapshot(
                18,
                61030UL,
                Tick.Zero,
                MapSize.Small,
                world.Map.TerrainTypeIds.ToArray(),
                world.Map.ObjectIds.ToArray(),
                world.Map.OwnerIds.ToArray(),
                world.Map.RoadTypeIds.ToArray(),
                world.Map.RailTypeIds.ToArray(),
                world.TownAnchors,
                world.Industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>(),
                world.Towns);

            SaveSnapshot migrated = new SaveFormatV18ToV19VehicleUnlocksMigration().Migrate(v18);

            Assert.That(migrated.FormatVersion, Is.EqualTo(19));
            Assert.That(migrated.HasUnlockedVehicleTypes, Is.True);
            Assert.That(migrated.UnlockedVehicleTypeIds, Is.Empty);
        }

        [Test]
        public void Pipeline_MigratesV18ThroughCurrentVersion()
        {
            GeneratedWorld world = GenerateWorld(61031UL);
            SaveSnapshot v18 = new SaveSnapshot(
                18,
                61031UL,
                Tick.Zero,
                MapSize.Small,
                world.Map.TerrainTypeIds.ToArray(),
                world.Map.ObjectIds.ToArray(),
                world.Map.OwnerIds.ToArray(),
                world.Map.RoadTypeIds.ToArray(),
                world.Map.RailTypeIds.ToArray(),
                world.TownAnchors,
                world.Industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>(),
                world.Towns);

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v18);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.HasUnlockedVehicleTypes, Is.True);
            Assert.That(current.UnlockedVehicleTypeIds, Is.Empty);
        }

        private static GeneratedWorld GenerateWorld(ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, seed);
        }
    }
}
