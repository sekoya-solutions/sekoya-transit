#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RailStationCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void BuildRailStation_AdjacentToTown_AppliesAndRecordsForReplay()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindStationCandidate(world);
            int versionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(1), new BuildRailStationCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRailStationId(tile), Is.EqualTo(1));
            Assert.That(world.RailStations, Has.Count.EqualTo(1));
            Assert.That(world.RailStations[0].Id, Is.EqualTo(0));
            Assert.That(world.RailStations[0].Coordinate, Is.EqualTo(tile));
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(world.RailGraph.TryGetNodeId(tile, out _), Is.True);
            Assert.That(processor.Log.Entries, Has.Count.EqualTo(1));

            GeneratedWorld replayed = CreateWorld();
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRailStationId(tile), Is.EqualTo(1));
            Assert.That(replayed.RailStations, Has.Count.EqualTo(1));
            Assert.That(replayed.RailGraph.TryGetNodeId(tile, out _), Is.True);
        }

        [Test]
        public void BuildRailStation_NotAdjacentOrOccupied_RejectsWithoutMutation()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate townTile = world.TownAnchors[0];
            TileCoordinate far = FindFarEmptyTile(world);
            TileCoordinate candidate = FindStationCandidate(world);
            world.Map.SetRailTypeId(candidate, TileMap.BasicRailTypeId);
            world.RailGraph.RebuildFromMap(world.Map);
            int versionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult onTown = processor.Process(new Tick(1), new BuildRailStationCommand(townTile));
            CommandProcessingResult onFar = processor.Process(new Tick(1), new BuildRailStationCommand(far));
            CommandProcessingResult onRoad = processor.Process(new Tick(1), new BuildRailStationCommand(candidate));

            Assert.That(onTown.IsAccepted, Is.False);
            Assert.That(onTown.RejectionReason, Does.Contain("occupied"));
            Assert.That(onFar.IsAccepted, Is.False);
            Assert.That(onFar.RejectionReason, Does.Contain("adjacent"));
            Assert.That(onRoad.IsAccepted, Is.False);
            Assert.That(onRoad.RejectionReason, Does.Contain("rail"));
            Assert.That(processor.Log.Entries, Is.Empty);
            Assert.That(world.RailStations, Is.Empty);
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore));
        }

        [Test]
        public void DemolishRailStation_ExistingStation_ClearsAndReplays()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate tile = FindStationCandidate(world);
            var buildProcessor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(buildProcessor.Process(new Tick(1), new BuildRailStationCommand(tile)).IsAccepted, Is.True);
            int versionBefore = world.RailGraph.Version;
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(new Tick(2), new DemolishRailStationCommand(tile));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Map.GetRailStationId(tile), Is.EqualTo(TileMap.NoRailStationId));
            Assert.That(world.RailStations, Is.Empty);
            Assert.That(world.RailGraph.Version, Is.EqualTo(versionBefore + 1));
            Assert.That(world.RailGraph.TryGetNodeId(tile, out _), Is.False);

            GeneratedWorld replayed = CreateWorld();
            Assert.That(
                new SimulationCommandProcessor<GeneratedWorld>(replayed)
                    .Process(new Tick(1), new BuildRailStationCommand(tile))
                    .IsAccepted,
                Is.True);
            processor.Log.Replay(replayed);
            Assert.That(replayed.Map.GetRailStationId(tile), Is.EqualTo(TileMap.NoRailStationId));
            Assert.That(replayed.RailStations, Is.Empty);
        }

        [Test]
        public void BuildRailStation_ThenAdjacentRoad_CreatesLengthOneGraphEdge()
        {
            GeneratedWorld world = CreateWorld();
            TileCoordinate stationTile = FindStationCandidate(world);
            TileCoordinate roadTile = FindOrthogonalRoadCandidate(world, stationTile);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(stationTile)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(2), new BuildRailCommand(roadTile)).IsAccepted, Is.True);

            Assert.That(world.RailGraph.TryGetNodeId(stationTile, out int stationNodeId), Is.True);
            Assert.That(world.RailGraph.TryGetNodeId(roadTile, out int roadNodeId), Is.True);
            Assert.That(world.RailGraph.GetIncidentEdgeIds(stationNodeId), Has.Count.EqualTo(1));
            int edgeId = world.RailGraph.GetIncidentEdgeIds(stationNodeId)[0];
            Assert.That(world.RailGraph.TryGetEdge(edgeId, out RailGraphEdge? edge), Is.True);
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
                        || world.Map.GetRailTypeId(coordinate) != TileMap.NoRailTypeId
                        || world.Map.GetStationId(coordinate) != TileMap.NoStationId
                        || world.Map.GetRailStationId(coordinate) != TileMap.NoRailStationId)
                    {
                        continue;
                    }

                    if (!RailStationPlacement.HasOrthogonalTownOrIndustryNeighbor(world.Map, coordinate))
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
                if (RailPlacement.CanBuild(world.Map, neighbor))
                {
                    return neighbor;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for rail track was found.");
        }

        private static TileCoordinate? TryFindOrthogonalEmptyNeighbor(GeneratedWorld world, TileCoordinate origin)
        {
            int[] offsetX = { 0, 1, 0, -1 };
            int[] offsetY = { -1, 0, 1, 0 };
            for (int direction = 0; direction < 4; direction++)
            {
                var neighbor = new TileCoordinate(origin.X + offsetX[direction], origin.Y + offsetY[direction]);
                if (RailStationPlacement.CanBuild(world.Map, neighbor))
                {
                    return neighbor;
                }
            }

            return null;
        }
    }
}
