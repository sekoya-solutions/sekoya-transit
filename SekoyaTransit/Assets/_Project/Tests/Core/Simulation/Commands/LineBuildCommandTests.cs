#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class LineBuildCommandTests
    {
        private const int _runLength = 6;
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void SnapLine_DragMostlyAlongX_SnapsToHorizontalLine()
        {
            TileCoordinate[] tiles = LineBuildPlan.SnapLine(new TileCoordinate(2, 5), new TileCoordinate(5, 6));

            Assert.That(tiles, Is.EqualTo(new[]
            {
                new TileCoordinate(2, 5),
                new TileCoordinate(3, 5),
                new TileCoordinate(4, 5),
                new TileCoordinate(5, 5),
            }));
        }

        [Test]
        public void SnapLine_DragMostlyAlongNegativeY_SnapsToVerticalLineInDragOrder()
        {
            TileCoordinate[] tiles = LineBuildPlan.SnapLine(new TileCoordinate(4, 6), new TileCoordinate(3, 3));

            Assert.That(tiles, Is.EqualTo(new[]
            {
                new TileCoordinate(4, 6),
                new TileCoordinate(4, 5),
                new TileCoordinate(4, 4),
                new TileCoordinate(4, 3),
            }));
        }

        [Test]
        public void SnapLine_EqualDistance_PrefersXAxis()
        {
            TileCoordinate[] tiles = LineBuildPlan.SnapLine(new TileCoordinate(1, 1), new TileCoordinate(3, 3));

            Assert.That(tiles[tiles.Length - 1], Is.EqualTo(new TileCoordinate(3, 1)));
        }

        [Test]
        public void SnapLine_SameTile_ReturnsOnlyTheStartTile()
        {
            TileCoordinate[] tiles = LineBuildPlan.SnapLine(new TileCoordinate(7, 7), new TileCoordinate(7, 7));

            Assert.That(tiles, Is.EqualTo(new[] { new TileCoordinate(7, 7) }));
        }

        [Test]
        public void BuildRoadLine_FreeRun_BuildsEveryTileAndReplays()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var end = new TileCoordinate(start.X + _runLength - 1, start.Y);
            int versionBefore = world.RoadGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRoadLineCommand(start, end));

            Assert.That(result.IsAccepted, Is.True);
            for (int i = 0; i < _runLength; i++)
            {
                Assert.That(
                    world.Map.GetRoadTypeId(new TileCoordinate(start.X + i, start.Y)),
                    Is.EqualTo(TileMap.BasicRoadTypeId));
            }

            Assert.That(world.RoadGraph.Version, Is.EqualTo(versionBefore + 1));
            GeneratedWorld replayed = CreateWorld();
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRoadTypeId(end), Is.EqualTo(TileMap.BasicRoadTypeId));
        }

        [Test]
        public void BuildRailLine_FreeRunDraggedUpwards_BuildsEveryTile()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate low = FindFreeRun(world, horizontal: false);
            var high = new TileCoordinate(low.X, low.Y + _runLength - 1);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRailLineCommand(high, low));

            Assert.That(result.IsAccepted, Is.True);
            for (int i = 0; i < _runLength; i++)
            {
                Assert.That(
                    world.Map.GetRailTypeId(new TileCoordinate(low.X, low.Y + i)),
                    Is.EqualTo(TileMap.BasicRailTypeId));
            }
        }

        [Test]
        public void BuildRoadLine_SingleTile_BuildsOneTile()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindFreeRun(world, horizontal: true);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRoadLineCommand(tile, tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRoadTypeId(tile), Is.EqualTo(TileMap.BasicRoadTypeId));
        }

        [Test]
        public void BuildRoadLine_ObstacleInPath_RejectsAndBuildsNothing()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var obstacle = new TileCoordinate(start.X + 3, start.Y);
            world.Map.SetObjectId(obstacle, 1);
            var end = new TileCoordinate(start.X + _runLength - 1, start.Y);
            int versionBefore = world.RoadGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRoadLineCommand(start, end));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("blocked"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.Map.GetRoadTypeId(start), Is.EqualTo(TileMap.NoRoadTypeId));
            Assert.That(world.RoadGraph.Version, Is.EqualTo(versionBefore));
        }

        [Test]
        public void Plan_ObstacleInPath_MarksObstacleAndEveryLaterTileInvalid()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            world.Map.SetObjectId(new TileCoordinate(start.X + 3, start.Y), 1);
            var end = new TileCoordinate(start.X + _runLength - 1, start.Y);

            LineBuildPlan plan = LineBuildPlan.Create(world.Map, LineBuildKind.Road, start, end);

            Assert.That(plan.IsBuildable, Is.False);
            for (int i = 0; i < _runLength; i++)
            {
                Assert.That(plan.IsTileValid(i), Is.EqualTo(i < 3), "tile " + i);
            }
        }

        [Test]
        public void BuildRailLine_ExistingTrackOnPath_KeepsItAndBuildsTheRest()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var existing = new TileCoordinate(start.X + 2, start.Y);
            world.Map.SetRailTypeId(existing, TileMap.BasicRailTypeId);
            world.RailGraph.RebuildFromMap(world.Map);
            var end = new TileCoordinate(start.X + _runLength - 1, start.Y);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRailLineCommand(start, end));

            Assert.That(result.IsAccepted, Is.True);
            for (int i = 0; i < _runLength; i++)
            {
                Assert.That(
                    world.Map.GetRailTypeId(new TileCoordinate(start.X + i, start.Y)),
                    Is.EqualTo(TileMap.BasicRailTypeId));
            }
        }

        [Test]
        public void BuildRoadLine_EverythingAlreadyBuilt_Rejects()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var end = new TileCoordinate(start.X + _runLength - 1, start.Y);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(processor.Process(new Tick(1), new BuildRoadLineCommand(start, end)).IsAccepted, Is.True);

            CommandProcessingResult again = processor.Process(new Tick(2), new BuildRoadLineCommand(start, end));

            Assert.That(again.IsAccepted, Is.False);
            Assert.That(again.RejectionReason, Does.Contain("already has a road"));
        }

        [Test]
        public void BuildRailLine_EndOffMap_Rejects()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new BuildRailLineCommand(start, new TileCoordinate(world.Map.Width, start.Y)));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("outside the map"));
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

        private static TileCoordinate FindFreeRun(GeneratedWorld world, bool horizontal)
        {
            for (int y = 0; y < world.Map.Height; y++)
            {
                for (int x = 0; x < world.Map.Width; x++)
                {
                    bool free = true;
                    for (int i = 0; i < _runLength && free; i++)
                    {
                        var tile = new TileCoordinate(horizontal ? x + i : x, horizontal ? y : y + i);
                        free = RoadPlacement.CanBuild(world.Map, tile) && RailPlacement.CanBuild(world.Map, tile);
                    }

                    if (free)
                    {
                        return new TileCoordinate(x, y);
                    }
                }
            }

            throw new InvalidOperationException("No free run found on the generated map.");
        }
    }
}
