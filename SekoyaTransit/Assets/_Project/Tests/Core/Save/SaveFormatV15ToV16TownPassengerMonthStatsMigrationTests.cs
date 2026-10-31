#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV15ToV16TownPassengerMonthStatsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV16_PreservesTownsAndZerosMonthPassengerStats()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 60110UL);
            var needs = new[] { new TownNeed("wood", monthSupply: 4) };
            var towns = new[]
            {
                new Town(0, world.TownAnchors[0], 250, needs, statsYear: 1900, statsMonth: 3)
            };
            SaveSnapshot v15 = new SaveSnapshot(
                15,
                60110UL,
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

            SaveSnapshot migrated = new SaveFormatV15ToV16TownPassengerMonthStatsMigration().Migrate(v15);

            Assert.That(migrated.FormatVersion, Is.EqualTo(16));
            Assert.That(migrated.Towns[0].Population, Is.EqualTo(250));
            Assert.That(migrated.Towns[0].MonthPassengersGenerated, Is.EqualTo(0L));
            Assert.That(migrated.Towns[0].MonthPassengersTransported, Is.EqualTo(0L));
            Assert.That(migrated.Towns[0].MonthMailGenerated, Is.EqualTo(0L));
            Assert.That(migrated.Towns[0].MonthMailTransported, Is.EqualTo(0L));
        }

        [Test]
        public void Pipeline_RefusesV15AsTooOld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 60111UL);
            SaveSnapshot v15 = new SaveSnapshot(
                15,
                60111UL,
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

            Assert.Throws<SaveFormatTooOldException>(
                () => CurrentSaveMigrations.CreatePipeline().Migrate(v15));
        }
    }
}
