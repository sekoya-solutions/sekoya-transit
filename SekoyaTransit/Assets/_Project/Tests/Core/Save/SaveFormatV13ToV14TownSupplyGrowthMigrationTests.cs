#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV13ToV14TownSupplyGrowthMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV14_PreservesTownPopulationAndNeeds()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 58110UL);
            var needs = new[] { new TownNeed("wood", monthSupply: 0) };
            var towns = new[]
            {
                new Town(0, world.TownAnchors[0], 250, needs)
            };
            SaveSnapshot v13 = new SaveSnapshot(
                13,
                58110UL,
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
                towns);

            SaveSnapshot migrated = new SaveFormatV13ToV14TownSupplyGrowthMigration().Migrate(v13);

            Assert.That(migrated.FormatVersion, Is.EqualTo(14));
            Assert.That(migrated.Towns[0].Population, Is.EqualTo(250));
            Assert.That(migrated.Towns[0].Needs, Has.Count.EqualTo(1));
            Assert.That(migrated.Towns[0].Needs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(migrated.Towns[0].Needs[0].MonthSupply, Is.EqualTo(0));
            Assert.That(migrated.Towns[0].StatsYear, Is.EqualTo(0));
            Assert.That(migrated.Towns[0].StatsMonth, Is.EqualTo(0));
        }

        [Test]
        public void Pipeline_MigratesV13ThroughCurrentVersion()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 58111UL);
            SaveSnapshot v13 = new SaveSnapshot(
                13,
                58111UL,
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

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v13);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.Towns, Has.Count.EqualTo(1));
        }
    }
}
