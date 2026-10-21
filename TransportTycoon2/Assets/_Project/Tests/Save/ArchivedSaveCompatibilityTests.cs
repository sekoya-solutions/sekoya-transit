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
            _serializer = new JsonGzipSaveSerializer(
                new SaveMigrationPipeline(SaveSnapshot.CurrentFormatVersion, Array.Empty<ISaveMigration>()));
        }

        [Test]
        public void Deserialize_ArchivedVersionOneFixture_RestoresPinnedSnapshot()
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
            Array.Fill(expectedObjects, TileMap.NoObjectId);
            Array.Fill(expectedOwners, TileMap.UnownedId);

            TileCoordinate town = new TileCoordinate(2, 3);
            TileCoordinate industryCoordinate = new TileCoordinate(5, 7);
            int townIndex = town.Y * _smallMapSide + town.X;
            int industryIndex = industryCoordinate.Y * _smallMapSide + industryCoordinate.X;
            int markedTileIndex = 4 * _smallMapSide + 8;
            expectedObjects[townIndex] = 0;
            expectedObjects[industryIndex] = 1;
            expectedTerrain[markedTileIndex] = 2;
            expectedOwners[markedTileIndex] = 9;

            Assert.That(snapshot.FormatVersion, Is.EqualTo(1));
            Assert.That(snapshot.MapSeed, Is.EqualTo(1234567890123456789UL));
            Assert.That(snapshot.CurrentTick.Value, Is.EqualTo(987654321L));
            Assert.That(snapshot.MapSize, Is.EqualTo(MapSize.Small));
            CollectionAssert.AreEqual(expectedTerrain, snapshot.TerrainTypeIds.ToArray());
            CollectionAssert.AreEqual(expectedObjects, snapshot.ObjectIds.ToArray());
            CollectionAssert.AreEqual(expectedOwners, snapshot.OwnerIds.ToArray());
            CollectionAssert.AreEqual(new[] { town }, snapshot.TownAnchors);
            CollectionAssert.AreEqual(
                new[] { new PlacedIndustry("forest", 0, industryCoordinate) },
                snapshot.Industries);
        }
    }
}
