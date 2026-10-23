#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class WorldSaveTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void ApplyTransportChannels_CopiesRoadAndRailChannelsAndRebuildsGraphs()
        {
            const int sideLength = (int)MapSize.Small;
            int tileCount = sideLength * sideLength;
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            GeneratedWorld world = WorldGenerator.Generate(settings, 53UL);
            var roads = new int[tileCount];
            var rails = new int[tileCount];
            Array.Fill(roads, TileMap.NoRoadTypeId);
            Array.Fill(rails, TileMap.NoRailTypeId);
            roads[(2 * sideLength) + 2] = TileMap.BasicRoadTypeId;
            rails[(3 * sideLength) + 3] = TileMap.BasicRailTypeId;

            var snapshot = new SaveSnapshot(
                SaveSnapshot.CurrentFormatVersion,
                53UL,
                Tick.Zero,
                MapSize.Small,
                world.Map.TerrainTypeIds.ToArray(),
                world.Map.ObjectIds.ToArray(),
                world.Map.OwnerIds.ToArray(),
                roads,
                rails,
                world.TownAnchors,
                world.Industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>());

            int roadVersionBefore = world.RoadGraph.Version;
            int railVersionBefore = world.RailGraph.Version;

            WorldSave.ApplyTransportChannels(world, snapshot);

            Assert.That(world.Map.GetRoadTypeId(new TileCoordinate(2, 2)), Is.EqualTo(TileMap.BasicRoadTypeId));
            Assert.That(world.Map.GetRailTypeId(new TileCoordinate(3, 3)), Is.EqualTo(TileMap.BasicRailTypeId));
            Assert.That(world.RoadGraph.Version, Is.EqualTo(roadVersionBefore + 1));
            Assert.That(world.RailGraph.Version, Is.EqualTo(railVersionBefore + 1));
            Assert.That(world.RailGraph.TryGetNodeId(new TileCoordinate(3, 3), out _), Is.True);
        }
    }
}
