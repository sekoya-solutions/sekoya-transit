#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveSnapshotTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Capture_GeneratedWorld_PreservesSeedTickAndWorldChannels()
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                industryRules);
            GeneratedWorld world = WorldGenerator.Generate(settings, 424242UL);

            SaveSnapshot snapshot = SaveSnapshot.Capture(424242UL, new Tick(9876), world);

            Assert.That(snapshot.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(snapshot.MapSeed, Is.EqualTo(424242UL));
            Assert.That(snapshot.CurrentTick.Value, Is.EqualTo(9876));
            Assert.That(snapshot.MapSize, Is.EqualTo(world.Map.Size));
            CollectionAssert.AreEqual(world.Map.TerrainTypeIds.ToArray(), snapshot.TerrainTypeIds.ToArray());
            CollectionAssert.AreEqual(world.Map.ObjectIds.ToArray(), snapshot.ObjectIds.ToArray());
            CollectionAssert.AreEqual(world.Map.OwnerIds.ToArray(), snapshot.OwnerIds.ToArray());
            CollectionAssert.AreEqual(world.Map.RoadTypeIds.ToArray(), snapshot.RoadTypeIds.ToArray());
            CollectionAssert.AreEqual(world.Map.RailTypeIds.ToArray(), snapshot.RailTypeIds.ToArray());
            CollectionAssert.AreEqual(world.TownAnchors, snapshot.TownAnchors);
            CollectionAssert.AreEqual(world.Industries, snapshot.Industries);
            CollectionAssert.AreEqual(world.RoadStations, snapshot.RoadStations);
            Assert.That(snapshot.HasRoadVehicles, Is.True);
            Assert.That(snapshot.RoadVehicles, Is.Empty);
            Assert.That(snapshot.HasRailStations, Is.True);
            Assert.That(snapshot.RailStations, Is.Empty);
            Assert.That(snapshot.HasRailVehicles, Is.True);
            Assert.That(snapshot.RailVehicles, Is.Empty);
            Assert.That(snapshot.CompanyCash, Is.EqualTo(GeneratedWorld.PlaceholderStartingCash));
        }

        [Test]
        public void Constructor_InputArraysMutatedAfterward_SnapshotRemainsUnchanged()
        {
            const int sideLength = (int)MapSize.Small;
            int tileCount = sideLength * sideLength;
            var terrain = new int[tileCount];
            var objects = new int[tileCount];
            var owners = new int[tileCount];
            var roads = new int[tileCount];
            var rails = new int[tileCount];
            Array.Fill(objects, TileMap.NoObjectId);
            Array.Fill(owners, TileMap.UnownedId);
            Array.Fill(roads, TileMap.NoRoadTypeId);
            Array.Fill(rails, TileMap.NoRailTypeId);
            var towns = new List<TileCoordinate> { new TileCoordinate(3, 4) };
            objects[(4 * sideLength) + 3] = 0;
            roads[(4 * sideLength) + 3] = TileMap.BasicRoadTypeId;
            var industries = new List<PlacedIndustry>();

            var snapshot = new SaveSnapshot(
                SaveSnapshot.CurrentFormatVersion,
                12UL,
                Tick.Zero,
                MapSize.Small,
                terrain,
                objects,
                owners,
                roads,
                rails,
                towns,
                industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>());

            terrain[(4 * sideLength) + 3] = 8;
            objects[(4 * sideLength) + 3] = 9;
            roads[(4 * sideLength) + 3] = 99;
            towns[0] = new TileCoordinate(6, 7);

            Assert.That(snapshot.TerrainTypeIds[(4 * sideLength) + 3], Is.EqualTo(0));
            Assert.That(snapshot.ObjectIds[(4 * sideLength) + 3], Is.EqualTo(0));
            Assert.That(snapshot.RoadTypeIds[(4 * sideLength) + 3], Is.EqualTo(TileMap.BasicRoadTypeId));
            Assert.That(snapshot.TownAnchors[0], Is.EqualTo(new TileCoordinate(3, 4)));
        }

        [Test]
        public void Constructor_TownAnchorDoesNotMatchObjectId_Throws()
        {
            const int sideLength = (int)MapSize.Small;
            int tileCount = sideLength * sideLength;
            var objects = new int[tileCount];
            Array.Fill(objects, TileMap.NoObjectId);

            var roads = new int[tileCount];
            var rails = new int[tileCount];
            Array.Fill(roads, TileMap.NoRoadTypeId);
            Array.Fill(rails, TileMap.NoRailTypeId);

            Assert.Throws<ArgumentException>(() => new SaveSnapshot(
                SaveSnapshot.CurrentFormatVersion,
                12UL,
                Tick.Zero,
                MapSize.Small,
                new int[tileCount],
                objects,
                new int[tileCount],
                roads,
                rails,
                new[] { new TileCoordinate(3, 4) },
                Array.Empty<PlacedIndustry>(),
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>()));
        }
    }
}
