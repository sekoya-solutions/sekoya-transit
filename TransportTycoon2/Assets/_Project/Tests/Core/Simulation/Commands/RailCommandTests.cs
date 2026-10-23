#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.Tests.World;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RailCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuildRail_EmptyTile_AppliesAndRecordsForReplay()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            int versionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRailCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRailTypeId(tile), Is.EqualTo(TileMap.BasicRailTypeId));
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(world.RailGraph.TryGetNodeId(tile, out _), Is.True);
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));

            GeneratedWorld replayed = CreateWorld();
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRailTypeId(tile), Is.EqualTo(TileMap.BasicRailTypeId));
            Assert.That(replayed.RailGraph.TryGetNodeId(tile, out _), Is.True);
        }

        [Test]
        public void BuildRail_OccupiedOrExistingRail_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townTile = world.TownAnchors[0];
            TileCoordinate empty = FindEmptyTile(world);
            world.Map.SetRailTypeId(empty, TileMap.BasicRailTypeId);
            world.RailGraph.RebuildFromMap(world.Map);
            int versionBefore = world.RailGraph.Version;
            string fingerprintBefore = RailGraphTopology.Fingerprint(world.RailGraph);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult onTown = processor.Process(new Tick(1), new BuildRailCommand(townTile));
            CommandProcessingResult onRail = processor.Process(new Tick(1), new BuildRailCommand(empty));

            Assert.That(onTown.IsAccepted, Is.False);
            Assert.That(onTown.RejectionReason, Does.Contain("occupied"));
            Assert.That(onRail.IsAccepted, Is.False);
            Assert.That(onRail.RejectionReason, Does.Contain("already has rail track"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.Map.GetRailTypeId(townTile), Is.EqualTo(TileMap.NoRailTypeId));
            Assert.That(world.Map.GetRailTypeId(empty), Is.EqualTo(TileMap.BasicRailTypeId));
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore));
            Assert.That(RailGraphTopology.Fingerprint(world.RailGraph), Is.EqualTo(fingerprintBefore));
        }

        [Test]
        public void BuildRail_OnRoadTile_AllowsCoexistence()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            world.Map.SetRoadTypeId(tile, TileMap.BasicRoadTypeId);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRailCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.BasicRoadTypeId));
            Assert.That(world.Map.GetRailTypeId(tile), Is.EqualTo(TileMap.BasicRailTypeId));
        }

        [Test]
        public void BuildRail_OutOfBounds_Rejects()
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuildRailCommand(new TileCoordinate(-1, 0)));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("outside"));
            Assert.That(processor.Log.Entries, Is.Empty);
        }

        [Test]
        public void DemolishRail_ExistingRoad_ClearsAndReplays()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            world.Map.SetRailTypeId(tile, TileMap.BasicRailTypeId);
            world.RailGraph.RebuildFromMap(world.Map);
            int versionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(2), new DemolishRailCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRailTypeId(tile), Is.EqualTo(TileMap.NoRailTypeId));
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(world.RailGraph.TryGetNodeId(tile, out _), Is.False);

            GeneratedWorld replayed = CreateWorld();
            replayed.Map.SetRailTypeId(tile, TileMap.BasicRailTypeId);
            replayed.RailGraph.RebuildFromMap(replayed.Map);
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRailTypeId(tile), Is.EqualTo(TileMap.NoRailTypeId));
            Assert.That(replayed.RailGraph.TryGetNodeId(tile, out _), Is.False);
        }

        [Test]
        public void RailCommands_GraphMatchesFreshRebuildFingerprint()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate a = FindEmptyTile(world);
            TileCoordinate b = new TileCoordinate(a.X + 1, a.Y);
            TileCoordinate c = new TileCoordinate(a.X + 2, a.Y);
            Assume.That(RailPlacement.CanBuild(world.Map, b), Is.True);
            Assume.That(RailPlacement.CanBuild(world.Map, c), Is.True);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            Assert.That(processor.Process(new Tick(1), new BuildRailCommand(a)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(1), new BuildRailCommand(b)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(1), new BuildRailCommand(c)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new DemolishRailCommand(b)).IsAccepted, Is.True);

            string fromCommands = RailGraphTopology.Fingerprint(world.RailGraph);
            var fresh = new RailGraph();
            fresh.RebuildFromMap(world.Map);
            Assert.That(RailGraphTopology.Fingerprint(fresh), Is.EqualTo(fromCommands));
        }

        [Test]
        public void DemolishRail_NoRoad_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindEmptyTile(world);
            int versionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new DemolishRailCommand(tile));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("no rail"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.Map.GetRailTypeId(tile), Is.EqualTo(TileMap.NoRailTypeId));
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore));
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
                    if (RailPlacement.CanBuild(world.Map, coordinate))
                    {
                        return coordinate;
                    }
                }
            }

            throw new InvalidOperationException("No empty tile found on the generated map.");
        }
    }
}
