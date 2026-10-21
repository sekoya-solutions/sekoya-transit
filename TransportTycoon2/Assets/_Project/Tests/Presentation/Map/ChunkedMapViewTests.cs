#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;
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
                Assert.That(filter.sharedMesh.vertexCount, Is.EqualTo(32 * 32 * 4));
            }
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
