#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class ChunkedMapViewTests
    {
        [Test]
        public void Constructor_SmallWorld_CreatesSixteenChunkMeshes()
        {
            GeneratedWorld world = CreateWorld(MapSize.Small, 37101UL);
            using var view = new ChunkedMapView(world, null);

            Assert.That(view.Root.childCount, Is.EqualTo(16));
            for (int index = 0; index < view.Root.childCount; index++)
            {
                var filter = view.Root.GetChild(index).GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null);
                Assert.That(filter.sharedMesh, Is.Not.Null);

                // One diamond per tile, plus one edge-strip quad per tile on the map's front row (y = 0) and column (x = 0).
                int chunkX = index % 4;
                int chunkY = index / 4;
                int edgeQuads = (chunkY == 0 ? 32 : 0) + (chunkX == 0 ? 32 : 0);
                Assert.That(filter.sharedMesh.vertexCount, Is.EqualTo((32 * 32 * 4) + (edgeQuads * 4)));
            }
        }

        [Test]
        public void Constructor_WithMapArt_DrawsOneTrackQuadPerRoadOrRailLayer()
        {
            GeneratedWorld world = CreateWorld(MapSize.Small, 37103UL);
            var road = new TileCoordinate(40, 40);
            var crossing = new TileCoordinate(41, 40);
            world.Map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);
            world.Map.SetRoadTypeId(crossing, TileMap.BasicRoadTypeId);
            world.Map.SetRailTypeId(crossing, TileMap.BasicRailTypeId);
            var art = AssetDatabase.LoadAssetAtPath<MapArtSet>("Assets/_Project/Art/Map/MapArtSet.asset");
            Assert.That(art, Is.Not.Null);
            using var view = new ChunkedMapView(world, null, art);

            // Tiles (40, 40) and (41, 40) are in chunk (1, 1): the road, plus road and rails on the level crossing.
            Transform tracks = view.Root.GetChild(5).Find("Tracks");
            Assert.That(tracks, Is.Not.Null);
            Assert.That(tracks.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(3 * 4));
            Assert.That(tracks.GetComponent<MeshRenderer>().sortingOrder, Is.EqualTo(MapSortingOrder.Tracks));
        }

        [Test]
        public void MarkTileDirty_TileOnAChunkBorder_AlsoRebuildsTheNeighborChunk()
        {
            GeneratedWorld world = CreateWorld(MapSize.Small, 37104UL);
            using var view = new ChunkedMapView(world, null);

            // (31, 5) is the last column of chunk (0, 0); its east neighbor (32, 5) is in chunk (1, 0).
            view.MarkTileDirty(31, 5);

            Assert.That(view.RebuildDirtyChunks(), Is.EqualTo(2));
        }

        [Test]
        public void RebuildDirtyChunks_AfterMarkingOneTile_RebuildsExactlyOneChunk()
        {
            GeneratedWorld world = CreateWorld(MapSize.Small, 37102UL);
            using var view = new ChunkedMapView(world, null);
            Mesh firstMesh = view.Root.GetChild(0).GetComponent<MeshFilter>().sharedMesh;

            view.MarkTileDirty(0, 0);
            int rebuilt = view.RebuildDirtyChunks();

            Assert.That(rebuilt, Is.EqualTo(1));
            Mesh rebuiltMesh = view.Root.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
            Assert.That(rebuiltMesh, Is.Not.Null);
            Assert.That(ReferenceEquals(firstMesh, rebuiltMesh), Is.False);
        }

        [Test]
        public void Constructor_RejectsNullWorld()
        {
            Assert.That(
                () => new ChunkedMapView(null!, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private static GeneratedWorld CreateWorld(MapSize mapSize, ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(mapSize, 4, 24),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0),
                    new IndustryPlacementRule("mine", 1, Array.Empty<string>(), new[] { "metal" }, 2, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 2, 60),
                    new IndustryPlacementRule(
                        "furniture_factory",
                        3,
                        new[] { "planks", "metal" },
                        new[] { "furniture" },
                        1,
                        60),
                });
            return WorldGenerator.Generate(settings, seed);
        }
    }
}
