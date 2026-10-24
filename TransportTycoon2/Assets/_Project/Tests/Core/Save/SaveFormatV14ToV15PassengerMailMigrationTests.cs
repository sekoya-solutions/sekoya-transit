#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV14ToV15PassengerMailMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV15_PreservesTownsAndLeavesWaitingEmpty()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 59110UL);
            var needs = new[] { new TownNeed("wood", monthSupply: 4) };
            var towns = new[]
            {
                new Town(0, world.TownAnchors[0], 250, needs, statsYear: 1900, statsMonth: 3)
            };
            SaveSnapshot v14 = new SaveSnapshot(
                14,
                59110UL,
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

            SaveSnapshot migrated = new SaveFormatV14ToV15PassengerMailMigration().Migrate(v14);

            Assert.That(migrated.FormatVersion, Is.EqualTo(15));
            Assert.That(migrated.Towns[0].Population, Is.EqualTo(250));
            Assert.That(migrated.Towns[0].Needs[0].MonthSupply, Is.EqualTo(4));
            Assert.That(migrated.Towns[0].WaitingCargo.IsEmpty, Is.True);
            Assert.That(migrated.Towns[0].LoadRoundRobinIndex, Is.EqualTo(0));
        }

        [Test]
        public void Pipeline_MigratesV14ThroughCurrentVersion()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 59111UL);
            SaveSnapshot v14 = new SaveSnapshot(
                14,
                59111UL,
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

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v14);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.Towns, Has.Count.EqualTo(1));
            Assert.That(current.Towns[0].WaitingCargo.IsEmpty, Is.True);
        }
    }
}
