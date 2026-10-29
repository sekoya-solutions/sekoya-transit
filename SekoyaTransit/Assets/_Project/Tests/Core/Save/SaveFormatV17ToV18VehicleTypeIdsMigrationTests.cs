#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV17ToV18VehicleTypeIdsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV18()
        {
            GeneratedWorld world = GenerateWorld(61020UL);
            SaveSnapshot v17 = new SaveSnapshot(
                17,
                61020UL,
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

            SaveSnapshot migrated = new SaveFormatV17ToV18VehicleTypeIdsMigration().Migrate(v17);

            Assert.That(migrated.FormatVersion, Is.EqualTo(18));
        }

        [Test]
        public void Pipeline_MigratesV17ThroughCurrentVersion()
        {
            GeneratedWorld world = GenerateWorld(61021UL);
            SaveSnapshot v17 = new SaveSnapshot(
                17,
                61021UL,
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

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v17);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
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
