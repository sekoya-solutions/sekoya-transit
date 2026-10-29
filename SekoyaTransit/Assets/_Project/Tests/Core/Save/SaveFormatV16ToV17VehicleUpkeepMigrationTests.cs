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
    public sealed class SaveFormatV16ToV17VehicleUpkeepMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV17_WithUnsetUpkeepMonth()
        {
            GeneratedWorld world = GenerateWorld(61010UL);
            SaveSnapshot v16 = new SaveSnapshot(
                16,
                61010UL,
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

            SaveSnapshot migrated = new SaveFormatV16ToV17VehicleUpkeepMigration().Migrate(v16);

            Assert.That(migrated.FormatVersion, Is.EqualTo(17));
            Assert.That(migrated.LastUpkeepYear, Is.EqualTo(0));
            Assert.That(migrated.LastUpkeepMonth, Is.EqualTo(0));
            Assert.That(migrated.CompanyCash, Is.EqualTo(GeneratedWorld.PlaceholderStartingCash));
        }

        [Test]
        public void Pipeline_MigratesV16ThroughCurrentVersion()
        {
            GeneratedWorld world = GenerateWorld(61011UL);
            SaveSnapshot v16 = new SaveSnapshot(
                16,
                61011UL,
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

            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(v16);

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.LastUpkeepYear, Is.EqualTo(0));
            Assert.That(current.LastUpkeepMonth, Is.EqualTo(0));
        }

        [Test]
        public void Capture_PreservesLastUpkeepMonth()
        {
            GeneratedWorld world = GenerateWorld(61012UL);
            VehicleUpkeep.AdvanceAll(world, CreateCatalog(), new GameDate(1901, 4, 1));

            SaveSnapshot snapshot = SaveSnapshot.Capture(61012UL, Tick.Zero, world);

            Assert.That(snapshot.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(snapshot.LastUpkeepYear, Is.EqualTo(1901));
            Assert.That(snapshot.LastUpkeepMonth, Is.EqualTo(4));
        }

        private static GeneratedWorld GenerateWorld(ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, seed);
        }
        private static ContentCatalog CreateCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }
    }
}
