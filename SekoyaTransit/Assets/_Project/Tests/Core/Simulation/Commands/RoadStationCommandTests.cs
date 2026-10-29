#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RoadStationCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuildRoadStation_AdjacentToTown_AppliesAndRecordsForReplay()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindStationCandidate(world);
            int versionBefore = world.RoadGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRoadStationCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetStationId(tile), Is.EqualTo(1));
            Assert.That(world.RoadStations, Has.Count.EqualTo(1));
            Assert.That(world.RoadStations[0].Id, Is.EqualTo(0));
            Assert.That(world.RoadStations[0].Coordinate, Is.EqualTo(tile));
            Assert.That(world.RoadGraph.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(world.RoadGraph.TryGetNodeId(tile, out _), Is.True);
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));

            GeneratedWorld replayed = CreateWorld();
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetStationId(tile), Is.EqualTo(1));
            Assert.That(replayed.RoadStations, Has.Count.EqualTo(1));
            Assert.That(replayed.RoadGraph.TryGetNodeId(tile, out _), Is.True);
        }

        [Test]
        public void BuildRoadStation_NotAdjacentOrOccupied_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townTile = world.TownAnchors[0];
            TileCoordinate far = FindFarEmptyTile(world);
            TileCoordinate candidate = FindStationCandidate(world);
            world.Map.SetRoadTypeId(candidate, TileMap.BasicRoadTypeId);
            world.RoadGraph.RebuildFromMap(world.Map);
            int versionBefore = world.RoadGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult onTown = processor.Process(new Tick(1), new BuildRoadStationCommand(townTile));
            CommandProcessingResult onFar = processor.Process(new Tick(1), new BuildRoadStationCommand(far));
            CommandProcessingResult onRoad = processor.Process(new Tick(1), new BuildRoadStationCommand(candidate));

            Assert.That(onTown.IsAccepted, Is.False);
            Assert.That(onTown.RejectionReason, Does.Contain("occupied"));
            Assert.That(onFar.IsAccepted, Is.False);
            Assert.That(onFar.RejectionReason, Does.Contain("adjacent"));
            Assert.That(onRoad.IsAccepted, Is.False);
            Assert.That(onRoad.RejectionReason, Does.Contain("road"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.RoadStations, Is.Empty);
            Assert.That(world.RoadGraph.Version, Is.EqualTo(versionBefore));
        }

        [Test]
        public void BuildRoadStation_OnRailTrack_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate candidate = FindStationCandidate(world);
            world.Map.SetRailTypeId(candidate, TileMap.BasicRailTypeId);
            world.RailGraph.RebuildFromMap(world.Map);
            int roadVersionBefore = world.RoadGraph.Version;
            int railVersionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult onRail = processor.Process(new Tick(1), new BuildRoadStationCommand(candidate));

            Assert.That(onRail.IsAccepted, Is.False);
            Assert.That(onRail.RejectionReason, Does.Contain("rail track"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.RoadStations, Is.Empty);
            Assert.That(world.Map.GetStationId(candidate), Is.EqualTo(TileMap.NoStationId));
            Assert.That(world.RoadGraph.Version, Is.EqualTo(roadVersionBefore));
            Assert.That(world.RailGraph.Version, Is.EqualTo(railVersionBefore));
        }

        [Test]
        public void DemolishRoadStation_ExistingStation_ClearsAndReplays()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindStationCandidate(world);
            var buildProcessor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(buildProcessor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
            int versionBefore = world.RoadGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(2), new DemolishRoadStationCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetStationId(tile), Is.EqualTo(TileMap.NoStationId));
            Assert.That(world.RoadStations, Is.Empty);
            Assert.That(world.RoadGraph.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(world.RoadGraph.TryGetNodeId(tile, out _), Is.False);

            GeneratedWorld replayed = CreateWorld();
            Assert.That(
                new SimulationCommandProcessor<GeneratedWorld>(replayed)
                    .Process(new Tick(1), new BuildRoadStationCommand(tile))
                    .IsAccepted,
                Is.True);
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetStationId(tile), Is.EqualTo(TileMap.NoStationId));
            Assert.That(replayed.RoadStations, Is.Empty);
        }

        [Test]
        public void BuildRoadStation_ThenAdjacentRoad_CreatesLengthOneGraphEdge()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate stationTile = FindStationCandidate(world);
            TileCoordinate roadTile = FindOrthogonalRoadCandidate(world, stationTile);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(stationTile)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new BuildRoadCommand(roadTile)).IsAccepted, Is.True);

            Assert.That(world.RoadGraph.TryGetNodeId(stationTile, out int stationNodeId), Is.True);
            Assert.That(world.RoadGraph.TryGetNodeId(roadTile, out int roadNodeId), Is.True);
            Assert.That(world.RoadGraph.GetIncidentEdgeIds(stationNodeId), Has.Count.EqualTo(1));
            int edgeId = world.RoadGraph.GetIncidentEdgeIds(stationNodeId)[0];
            Assert.That(world.RoadGraph.TryGetEdge(edgeId, out RoadGraphEdge? edge), Is.True);
            Assert.That(edge!.Length, Is.EqualTo(1));
            Assert.That(
                (edge.NodeIdA == stationNodeId && edge.NodeIdB == roadNodeId)
                || (edge.NodeIdA == roadNodeId && edge.NodeIdB == stationNodeId),
                Is.True);
        }

        private static GeneratedWorld CreateWorld()
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, 44001UL);
        }

        private static TileCoordinate FindStationCandidate(GeneratedWorld world)
        {
            for (int townIndex = 0; townIndex < world.TownAnchors.Count; townIndex++)
            {
                TileCoordinate town = world.TownAnchors[townIndex];
                TileCoordinate? candidate = TryFindOrthogonalEmptyNeighbor(world, town);
                if (candidate.HasValue)
                {
                    return candidate.Value;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor of a town was found.");
        }

        private static TileCoordinate FindFarEmptyTile(GeneratedWorld world)
        {
            for (int y = 0; y < world.Map.Height; y++)
            {
                for (int x = 0; x < world.Map.Width; x++)
                {
                    var coordinate = new TileCoordinate(x, y);
                    if (world.Map.GetObjectId(coordinate) != TileMap.NoObjectId
                        || world.Map.GetRoadTypeId(coordinate) != TileMap.NoRoadTypeId
                        || world.Map.GetStationId(coordinate) != TileMap.NoStationId)
                    {
                        continue;
                    }

                    if (!RoadStationPlacement.HasOrthogonalTownOrIndustryNeighbor(world.Map, coordinate))
                    {
                        return coordinate;
                    }
                }
            }

            throw new InvalidOperationException("No far empty tile was found.");
        }

        private static TileCoordinate FindOrthogonalRoadCandidate(GeneratedWorld world, TileCoordinate origin)
        {
            int[] offsetX = { 0, 1, 0, -1 };
            int[] offsetY = { -1, 0, 1, 0 };
            for (int direction = 0; direction < 4; direction++)
            {
                var neighbor = new TileCoordinate(origin.X + offsetX[direction], origin.Y + offsetY[direction]);
                if (RoadPlacement.CanBuild(world.Map, neighbor))
                {
                    return neighbor;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for a road was found.");
        }

        private static TileCoordinate? TryFindOrthogonalEmptyNeighbor(GeneratedWorld world, TileCoordinate origin)
        {
            int[] offsetX = { 0, 1, 0, -1 };
            int[] offsetY = { -1, 0, 1, 0 };
            for (int direction = 0; direction < 4; direction++)
            {
                var neighbor = new TileCoordinate(origin.X + offsetX[direction], origin.Y + offsetY[direction]);
                if (RoadStationPlacement.CanBuild(world.Map, neighbor))
                {
                    return neighbor;
                }
            }

            return null;
        }
    }
}
