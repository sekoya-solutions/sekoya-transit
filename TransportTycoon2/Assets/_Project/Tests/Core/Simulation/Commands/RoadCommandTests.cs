#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RoadCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuildRoad_EmptyTile_AppliesAndRecordsForReplay()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRoadCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.BasicRoadTypeId));
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));

            GeneratedWorld replayed = CreateWorld();
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.BasicRoadTypeId));
        }

        [Test]
        public void BuildRoad_OccupiedOrExistingRoad_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townTile = world.TownAnchors[0];
            TileCoordinate empty = FindEmptyTile(world);
            world.Map.SetRoadTypeId(empty, TileMap.BasicRoadTypeId);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult onTown = processor.Process(new Tick(1), new BuildRoadCommand(townTile));
            CommandProcessingResult onRoad = processor.Process(new Tick(1), new BuildRoadCommand(empty));

            Assert.That(onTown.IsAccepted, Is.False);
            Assert.That(onTown.RejectionReason, Does.Contain("occupied"));
            Assert.That(onRoad.IsAccepted, Is.False);
            Assert.That(onRoad.RejectionReason, Does.Contain("already has a road"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.Map.GetRoadTypeId(townTile), Is.EqualTo(TileMap.NoRoadTypeId));
            Assert.That(world.Map.GetRoadTypeId(empty), Is.EqualTo(TileMap.BasicRoadTypeId));
        }

        [Test]
        public void BuildRoad_OutOfBounds_Rejects()
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuildRoadCommand(new TileCoordinate(-1, 0)));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("outside"));
            Assert.That(processor.Log.Entries, Is.Empty);
        }

        [Test]
        public void DemolishRoad_ExistingRoad_ClearsAndReplays()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            world.Map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(2), new DemolishRoadCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.NoRoadTypeId));

            GeneratedWorld replayed = CreateWorld();
            replayed.Map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.NoRoadTypeId));
        }

        [Test]
        public void DemolishRoad_NoRoad_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new DemolishRoadCommand(tile));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("no road"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.NoRoadTypeId));
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0)
                });
            return WorldGenerator.Generate(settings, 41UL);
        }

        private static TileCoordinate FindEmptyTile(GeneratedWorld world)
        {
            for (int y = 0; y < world.Map.Height; y++)
            {
                for (int x = 0; x < world.Map.Width; x++)
                {
                    var coordinate = new TileCoordinate(x, y);
                    if (RoadPlacement.CanBuild(world.Map, coordinate))
                    {
                        return coordinate;
                    }
                }
            }

            throw new InvalidOperationException("No empty tile found on the generated map.");
        }
    }
}
