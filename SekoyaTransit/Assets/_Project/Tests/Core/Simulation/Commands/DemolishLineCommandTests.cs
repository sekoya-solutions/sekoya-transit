#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.Tests.World;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class DemolishLineCommandTests
    {
        private const int _runLength = 6;
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Plan_MixedLine_ResolvesEachTileLikeASingleClick()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            world.Map.SetRoadTypeId(At(start, 0), TileMap.BasicRoadTypeId);
            world.Map.SetRailTypeId(At(start, 1), TileMap.BasicRailTypeId);
            world.Map.SetRoadTypeId(At(start, 2), TileMap.BasicRoadTypeId);
            world.Map.SetRailTypeId(At(start, 2), TileMap.BasicRailTypeId);

            LineDemolishPlan plan = LineDemolishPlan.Create(world.Map, start, At(start, _runLength - 1));

            Assert.That(plan.Tiles.Count, Is.EqualTo(_runLength));
            Assert.That(plan.ClearedTileCount, Is.EqualTo(3));
            for (int i = 0; i < _runLength; i++)
            {
                TileDemolishTargets expected = TileDemolishTargets.Resolve(world.Map, At(start, i));
                TileDemolishTargets actual = plan.GetTargets(i);
                Assert.That(actual.Road, Is.EqualTo(expected.Road), "road " + i);
                Assert.That(actual.Rail, Is.EqualTo(expected.Rail), "rail " + i);
                Assert.That(actual.Any, Is.EqualTo(i < 3), "any " + i);
            }
        }

        [Test]
        public void DemolishLine_RoadAndRail_ClearsBothRebuildsEachGraphOnceAndReplays()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            TileCoordinate end = At(start, _runLength - 1);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.Accept(processor, new BuildRoadLineCommand(start, end));
            RouteTestSupport.Accept(processor, new BuildRailLineCommand(start, end));
            int roadVersion = world.RoadGraph.Version;
            int railVersion = world.RailGraph.Version;
            var demolish = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = demolish.Process(new Tick(2), new DemolishLineCommand(start, end));

            Assert.That(result.IsAccepted, Is.True);
            AssertRunCleared(world, start);
            Assert.That(world.RoadGraph.Version, Is.EqualTo(roadVersion + 1));
            Assert.That(world.RailGraph.Version, Is.EqualTo(railVersion + 1));
            Assert.That(demolish.Log.Entries.Count, Is.EqualTo(1));

            GeneratedWorld replayed = CreateWorld();
            processor.Log.Replay(replayed);
            demolish.Log.Replay(replayed);
            AssertRunCleared(replayed, start);
        }

        [Test]
        public void DemolishLine_EmptyTilesOnLine_SkipsThemAndClearsTheRest()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            world.Map.SetRoadTypeId(At(start, 0), TileMap.BasicRoadTypeId);
            world.Map.SetRoadTypeId(At(start, _runLength - 1), TileMap.BasicRoadTypeId);
            world.RoadGraph.RebuildFromMap(world.Map);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new DemolishLineCommand(start, At(start, _runLength - 1)));

            Assert.That(result.IsAccepted, Is.True);
            AssertRunCleared(world, start);
        }

        [Test]
        public void DemolishLine_NothingToRemove_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            int roadVersion = world.RoadGraph.Version;
            int railVersion = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new DemolishLineCommand(start, At(start, _runLength - 1)));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("nothing to demolish"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.RoadGraph.Version, Is.EqualTo(roadVersion));
            Assert.That(world.RailGraph.Version, Is.EqualTo(railVersion));
        }

        [Test]
        public void DemolishLine_SingleTile_ClearsOnlyThatTile()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.Accept(processor, new BuildRoadLineCommand(start, At(start, _runLength - 1)));
            TileCoordinate tile = At(start, 2);

            CommandProcessingResult result = processor.Process(new Tick(2), new DemolishLineCommand(tile, tile));

            Assert.That(result.IsAccepted, Is.True);
            for (int i = 0; i < _runLength; i++)
            {
                int expected = i == 2 ? TileMap.NoRoadTypeId : TileMap.BasicRoadTypeId;
                Assert.That(world.Map.GetRoadTypeId(At(start, i)), Is.EqualTo(expected), "tile " + i);
            }
        }

        [Test]
        public void DemolishLine_VerticalDragTowardSmallerY_ClearsOnlyTheSnappedLine()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate top = FindFreeRun(world, horizontal: false);
            var bottom = new TileCoordinate(top.X, top.Y + _runLength - 1);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.Accept(processor, new BuildRailLineCommand(top, bottom));
            var aside = new TileCoordinate(top.X + 1, top.Y);
            bool asideHasRail = RailPlacement.CanBuild(world.Map, aside);
            if (asideHasRail)
            {
                RouteTestSupport.Accept(processor, new BuildRailLineCommand(aside, aside));
            }

            // Dragged up from the bottom tile and one tile to the side: Y moved further, so the line stays vertical.
            CommandProcessingResult result = processor.Process(
                new Tick(2),
                new DemolishLineCommand(bottom, new TileCoordinate(top.X + 1, top.Y)));

            Assert.That(result.IsAccepted, Is.True);
            for (int i = 0; i < _runLength; i++)
            {
                Assert.That(
                    world.Map.GetRailTypeId(new TileCoordinate(top.X, top.Y + i)),
                    Is.EqualTo(TileMap.NoRailTypeId),
                    "tile " + i);
            }

            if (asideHasRail)
            {
                Assert.That(world.Map.GetRailTypeId(aside), Is.EqualTo(TileMap.BasicRailTypeId));
            }
        }

        [Test]
        public void DemolishLine_StartOrEndOffMap_Rejects()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate start = FindFreeRun(world, horizontal: true);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RouteTestSupport.Accept(processor, new BuildRoadLineCommand(start, start));
            var offMap = new TileCoordinate(world.Map.Width, start.Y);

            CommandProcessingResult endOff = processor.Process(new Tick(2), new DemolishLineCommand(start, offMap));
            CommandProcessingResult startOff = processor.Process(new Tick(2), new DemolishLineCommand(offMap, start));

            Assert.That(endOff.IsAccepted, Is.False);
            Assert.That(endOff.RejectionReason, Does.Contain("outside the map"));
            Assert.That(startOff.IsAccepted, Is.False);
            Assert.That(world.Map.GetRoadTypeId(start), Is.EqualTo(TileMap.BasicRoadTypeId));
        }

        [Test]
        public void DemolishLine_StationsOnLine_LeaveRoutesAndVehiclesLikeSingleClicks()
        {
            StationWorld byClicks = StationWorld.Create();
            StationWorld byLine = StationWorld.Create();
            Assert.That(byLine.RoadStationTile(1), Is.EqualTo(byClicks.RoadStationTile(1)));

            TileCoordinate b = byLine.RoadStationTile(1);
            var end = new TileCoordinate(b.X + 3, b.Y);
            ClickEachTile(byClicks, b, end);
            RouteTestSupport.Accept(byLine.Processor, new DemolishLineCommand(b, end));

            Assert.That(byLine.World.Map.GetStationId(b), Is.EqualTo(TileMap.NoStationId));
            foreach (TileCoordinate tile in LineBuildPlan.SnapLine(b, end))
            {
                Assert.That(byLine.World.Map.GetStationId(tile), Is.EqualTo(byClicks.World.Map.GetStationId(tile)));
                Assert.That(byLine.World.Map.GetRoadTypeId(tile), Is.EqualTo(byClicks.World.Map.GetRoadTypeId(tile)));
                Assert.That(byLine.World.Map.GetRailTypeId(tile), Is.EqualTo(byClicks.World.Map.GetRailTypeId(tile)));
            }

            Assert.That(byLine.World.RoadStations.Count, Is.EqualTo(byClicks.World.RoadStations.Count));
            Assert.That(byLine.World.RailStations.Count, Is.EqualTo(byClicks.World.RailStations.Count));
            Assert.That(byLine.StationIds(byLine.ShortRoute), Is.EqualTo(byClicks.StationIds(byClicks.ShortRoute)));
            Assert.That(byLine.StationIds(byLine.LongRoute), Is.EqualTo(byClicks.StationIds(byClicks.LongRoute)));
            Assert.That(byLine.World.TryGetUnassignedRoadVehicle(byLine.ShortRouteVehicle, out _), Is.True);
            Assert.That(byClicks.World.TryGetUnassignedRoadVehicle(byClicks.ShortRouteVehicle, out _), Is.True);
            Assert.That(byLine.World.TryGetRoadVehicle(byLine.LongRouteVehicle, out RoadVehicle? kept), Is.True);
            Assert.That(kept!.OrderStationIds, Is.EqualTo(byLine.StationIds(byLine.LongRoute)));
        }

        [Test]
        public void DemolishLine_RoadAndRailStationsOnLine_RemovesBothStations()
        {
            StationWorld fixture = StationWorld.Create();
            TileCoordinate road = fixture.RoadStationTile(0);
            TileCoordinate rail = fixture.RailStationTile;

            RouteTestSupport.Accept(fixture.Processor, new DemolishLineCommand(road, rail));

            Assert.That(fixture.World.Map.GetStationId(road), Is.EqualTo(TileMap.NoStationId));
            Assert.That(fixture.World.Map.GetRailStationId(rail), Is.EqualTo(TileMap.NoRailStationId));
            Assert.That(fixture.World.RailStations, Is.Empty);
        }

        // What the Delete tool did before Issue #349: on each tile, the single-tile commands its targets name.
        private static void ClickEachTile(StationWorld fixture, TileCoordinate start, TileCoordinate end)
        {
            foreach (TileCoordinate tile in LineBuildPlan.SnapLine(start, end))
            {
                TileDemolishTargets targets = TileDemolishTargets.Resolve(fixture.World.Map, tile);
                if (targets.RoadStation)
                {
                    RouteTestSupport.Accept(fixture.Processor, new DemolishRoadStationCommand(tile));
                }

                if (targets.RailStation)
                {
                    RouteTestSupport.Accept(fixture.Processor, new DemolishRailStationCommand(tile));
                }

                if (targets.Road)
                {
                    RouteTestSupport.Accept(fixture.Processor, new DemolishRoadCommand(tile));
                }

                if (targets.Rail)
                {
                    RouteTestSupport.Accept(fixture.Processor, new DemolishRailCommand(tile));
                }
            }
        }

        private static TileCoordinate At(TileCoordinate start, int offset)
        {
            return new TileCoordinate(start.X + offset, start.Y);
        }

        private static void AssertRunCleared(GeneratedWorld world, TileCoordinate start)
        {
            for (int i = 0; i < _runLength; i++)
            {
                Assert.That(world.Map.GetRoadTypeId(At(start, i)), Is.EqualTo(TileMap.NoRoadTypeId), "road " + i);
                Assert.That(world.Map.GetRailTypeId(At(start, i)), Is.EqualTo(TileMap.NoRailTypeId), "rail " + i);
            }
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                5,
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
                    for (int i = 0; i < _runLength + 1 && free; i++)
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

        // Three road stations with two routes (a-b and a-b-c, one truck each) and one rail station in the same row
        // as the first road station, built in scan order so two worlds from the same seed are identical.
        private sealed class StationWorld
        {
            private readonly List<TileCoordinate> _roadStationTiles = new List<TileCoordinate>();

            private StationWorld(GeneratedWorld world)
            {
                World = world;
                Processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            }

            public GeneratedWorld World { get; }

            public SimulationCommandProcessor<GeneratedWorld> Processor { get; }

            public TileCoordinate RailStationTile { get; private set; }

            public int ShortRoute { get; private set; }

            public int LongRoute { get; private set; }

            public int ShortRouteVehicle { get; private set; }

            public int LongRouteVehicle { get; private set; }

            public static StationWorld Create()
            {
                var settings = new WorldGenerationSettings(
                    new TownPlacementSettings(MapSize.Small, 3, 16),
                    5,
                    new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
                var fixture = new StationWorld(WorldGenerator.Generate(settings, 61002UL));
                fixture.BuildStations();
                int[] ids = new int[3];
                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.That(fixture.World.TryGetRoadStationAt(fixture._roadStationTiles[i], out RoadStation? station), Is.True);
                    ids[i] = station!.Id;
                }

                fixture.ShortRouteVehicle = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, ids[0], ids[1]);
                fixture.LongRouteVehicle = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, ids[0], ids[1], ids[2]);
                fixture.ShortRoute = fixture.Vehicle(fixture.ShortRouteVehicle).RouteId;
                fixture.LongRoute = fixture.Vehicle(fixture.LongRouteVehicle).RouteId;
                return fixture;
            }

            public TileCoordinate RoadStationTile(int index)
            {
                return _roadStationTiles[index];
            }

            public int[] StationIds(int routeId)
            {
                Assert.That(World.TryGetRoute(routeId, out Route? route), Is.True);
                var ids = new int[route!.Stops.Count];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = route.Stops[i].StationId;
                }

                return ids;
            }

            private RoadVehicle Vehicle(int vehicleId)
            {
                Assert.That(World.TryGetRoadVehicle(vehicleId, out RoadVehicle? vehicle), Is.True);
                return vehicle!;
            }

            // The first road station goes on the first free tile next to a town in scan order and the rail station
            // on the next free tile of that row, so both lie on one line; the other road stations follow in scan order.
            private void BuildStations()
            {
                for (int y = 0; y < World.Map.Height && _roadStationTiles.Count < 3; y++)
                {
                    for (int x = 0; x < World.Map.Width && _roadStationTiles.Count < 3; x++)
                    {
                        var tile = new TileCoordinate(x, y);
                        if (!RoadStationPlacement.CanBuild(World.Map, tile))
                        {
                            continue;
                        }

                        RouteTestSupport.Accept(Processor, new BuildRoadStationCommand(tile));
                        _roadStationTiles.Add(tile);
                        if (_roadStationTiles.Count == 1)
                        {
                            BuildRailStationAfter(tile);
                        }
                    }
                }

                Assert.That(_roadStationTiles.Count, Is.EqualTo(3), "road stations built");
            }

            private void BuildRailStationAfter(TileCoordinate roadStation)
            {
                for (int x = roadStation.X + 1; x < World.Map.Width; x++)
                {
                    var tile = new TileCoordinate(x, roadStation.Y);
                    if (RailStationPlacement.CanBuild(World.Map, tile))
                    {
                        RouteTestSupport.Accept(Processor, new BuildRailStationCommand(tile));
                        RailStationTile = tile;
                        return;
                    }
                }

                Assert.Fail("No rail station tile in the first road station's row.");
            }
        }
    }
}
