#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Save.Tests
{
    [TestFixture]
    public sealed class ArchivedSaveCompatibilityTests
    {
        private const int _smallMapSide = (int)MapSize.Small;
        private JsonGzipSaveSerializer _serializer = null!;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline());
        }

        [Test]
        public void Deserialize_ArchivedVersionOneFixture_MigratesToCurrentWithEmptyRoads()
        {
            string fixturePath = Path.Combine(
                Application.dataPath,
                "_Project",
                "Tests",
                "Save",
                "Fixtures",
                "Issue34V1.bytes");

            Assert.That(File.Exists(fixturePath), Is.True, "The archived version-1 fixture is missing.");

            SaveSnapshot snapshot = _serializer.Deserialize(File.ReadAllBytes(fixturePath));
            var expectedTerrain = new int[_smallMapSide * _smallMapSide];
            var expectedObjects = new int[_smallMapSide * _smallMapSide];
            var expectedOwners = new int[_smallMapSide * _smallMapSide];
            var expectedRoads = new int[_smallMapSide * _smallMapSide];
            var expectedRails = new int[_smallMapSide * _smallMapSide];
            Array.Fill(expectedObjects, TileMap.NoObjectId);
            Array.Fill(expectedOwners, TileMap.UnownedId);
            Array.Fill(expectedRoads, TileMap.NoRoadTypeId);
            Array.Fill(expectedRails, TileMap.NoRailTypeId);

            TileCoordinate town = new TileCoordinate(2, 3);
            TileCoordinate industryCoordinate = new TileCoordinate(5, 7);
            int townIndex = town.Y * _smallMapSide + town.X;
            int industryIndex = industryCoordinate.Y * _smallMapSide + industryCoordinate.X;
            int markedTileIndex = 4 * _smallMapSide + 8;
            expectedObjects[townIndex] = 0;
            expectedObjects[industryIndex] = 1;
            expectedTerrain[markedTileIndex] = 2;
            expectedOwners[markedTileIndex] = 9;

            Assert.That(snapshot.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(snapshot.MapSeed, Is.EqualTo(1234567890123456789UL));
            Assert.That(snapshot.CurrentTick.Value, Is.EqualTo(987654321L));
            Assert.That(snapshot.MapSize, Is.EqualTo(MapSize.Small));
            CollectionAssert.AreEqual(expectedTerrain, snapshot.TerrainTypeIds.ToArray());
            CollectionAssert.AreEqual(expectedObjects, snapshot.ObjectIds.ToArray());
            CollectionAssert.AreEqual(expectedOwners, snapshot.OwnerIds.ToArray());
            CollectionAssert.AreEqual(expectedRoads, snapshot.RoadTypeIds.ToArray());
            CollectionAssert.AreEqual(expectedRails, snapshot.RailTypeIds.ToArray());
            CollectionAssert.AreEqual(new[] { town }, snapshot.TownAnchors);
            Assert.That(snapshot.HasTowns, Is.True);
            Assert.That(snapshot.Towns, Has.Count.EqualTo(1));
            Assert.That(snapshot.Towns[0].Coordinate, Is.EqualTo(town));
            Assert.That(snapshot.Towns[0].Population, Is.EqualTo(GeneratedWorld.MigratedTownPopulation));
            Assert.That(snapshot.Towns[0].Needs, Is.Empty);
            CollectionAssert.AreEqual(
                new[] { new PlacedIndustry("forest", 0, industryCoordinate) },
                snapshot.Industries);
            Assert.That(snapshot.RoadStations, Is.Empty);
            Assert.That(snapshot.HasRoadVehicles, Is.True);
            Assert.That(snapshot.RoadVehicles, Is.Empty);
            Assert.That(snapshot.HasRailStations, Is.True);
            Assert.That(snapshot.RailStations, Is.Empty);
            Assert.That(snapshot.HasRailVehicles, Is.True);
            Assert.That(snapshot.RailVehicles, Is.Empty);
            Assert.That(snapshot.HasCompanyCash, Is.True);
            Assert.That(snapshot.CompanyCash, Is.EqualTo(GeneratedWorld.PlaceholderStartingCash));
            Assert.That(snapshot.LastUpkeepYear, Is.EqualTo(0));
            Assert.That(snapshot.LastUpkeepMonth, Is.EqualTo(0));
            Assert.That(snapshot.HasUnlockedVehicleTypes, Is.True);
            Assert.That(snapshot.UnlockedVehicleTypeIds, Is.Empty);
            Assert.That(snapshot.Industries[0].Cargo.IsEmpty, Is.True);
            Assert.That(snapshot.Industries[0].MonthProduced, Is.EqualTo(0L));
            Assert.That(snapshot.Industries[0].MonthTransported, Is.EqualTo(0L));
            Assert.That(snapshot.Industries[0].StatsYear, Is.EqualTo(0));
            Assert.That(snapshot.Industries[0].StatsMonth, Is.EqualTo(0));
        }
    }
}
