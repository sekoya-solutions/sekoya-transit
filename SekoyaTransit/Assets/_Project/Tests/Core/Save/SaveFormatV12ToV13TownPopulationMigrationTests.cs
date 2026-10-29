#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV12ToV13TownPopulationMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_AddsPlaceholderPopulationAndEmptyNeeds()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 57100UL);
            SaveSnapshot v12 = new SaveSnapshot(
                12,
                57100UL,
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
                Array.Empty<RailVehicle>());

            SaveSnapshot migrated = new SaveFormatV12ToV13TownPopulationMigration().Migrate(v12);

            Assert.That(migrated.FormatVersion, Is.EqualTo(13));
            Assert.That(migrated.HasTowns, Is.True);
            Assert.That(migrated.Towns, Has.Count.EqualTo(2));
            Assert.That(migrated.Towns[0].Population, Is.EqualTo(GeneratedWorld.MigratedTownPopulation));
            Assert.That(migrated.Towns[0].Needs, Is.Empty);
            Assert.That(migrated.Towns[0].Coordinate, Is.EqualTo(world.TownAnchors[0]));
        }

        [Test]
        public void Pipeline_MigratesV12ThroughCurrentVersion()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 57101UL);
            SaveSnapshot v12 = new SaveSnapshot(
                12,
                57101UL,
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
                Array.Empty<RailVehicle>());

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v12);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.Towns, Has.Count.EqualTo(1));
            Assert.That(current.Towns[0].Population, Is.EqualTo(GeneratedWorld.MigratedTownPopulation));
        }
    }
}
