#nullable enable
using NUnit.Framework;
using SekoyaTransit.Presentation.Map;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class MapTerrainLookTests
    {
        [Test]
        public void CreateGridTexture_IsClampedSquareWithMipMaps()
        {
            Texture2D texture = MapTerrainLook.CreateGridTexture();
            try
            {
                Assert.That(texture.width, Is.EqualTo(MapTerrainLook.GridTextureSize));
                Assert.That(texture.height, Is.EqualTo(MapTerrainLook.GridTextureSize));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ColorAt_SameSeedAndTile_IsAlwaysTheSame()
        {
            for (int tile = 0; tile < 200; tile++)
            {
                Color32 first = MapTerrainLook.ColorAt(424242UL, tile * 3, tile * 7);
                Color32 second = MapTerrainLook.ColorAt(424242UL, tile * 3, tile * 7);
                Assert.That(second, Is.EqualTo(first));
            }
        }

        [Test]
        public void KindAt_DifferentSeeds_GiveDifferentPatches()
        {
            int differences = 0;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (MapTerrainLook.KindAt(1UL, x, y) != MapTerrainLook.KindAt(2UL, x, y))
                    {
                        differences++;
                    }
                }
            }

            Assert.That(differences, Is.GreaterThan(0));
        }

        // Owner decision for Issue #69: small decorative patches on roughly 10-15 % of the tiles, mostly grass.
        [TestCase(37101UL)]
        [TestCase(90210UL)]
        [TestCase(5UL)]
        public void KindAt_MediumMap_SandAndEarthCoverRoughlyTenToFifteenPercent(ulong seed)
        {
            const int side = 256;
            int sand = 0;
            int earth = 0;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    TerrainKind kind = MapTerrainLook.KindAt(seed, x, y);
                    if (kind == TerrainKind.Sand)
                    {
                        sand++;
                    }
                    else if (kind == TerrainKind.Earth)
                    {
                        earth++;
                    }
                }
            }

            float share = (float)(sand + earth) / (side * side);
            Assert.That(share, Is.InRange(0.08f, 0.18f), $"sand {sand}, earth {earth}");
            Assert.That(sand, Is.GreaterThan(0));
            Assert.That(earth, Is.GreaterThan(0));
        }

        [Test]
        public void ColorAt_ShadeJitter_StaysNearTheBaseColor()
        {
            for (int tile = 0; tile < 500; tile++)
            {
                int x = tile % 40;
                int y = tile / 40;
                Color32 color = MapTerrainLook.ColorAt(7UL, x, y);
                Color32 baseColor = MapTerrainLook.KindAt(7UL, x, y) switch
                {
                    TerrainKind.Sand => MapTerrainLook.Sand,
                    TerrainKind.Earth => MapTerrainLook.Earth,
                    _ => MapTerrainLook.Grass,
                };
                Assert.That(Mathf.Abs(color.g - baseColor.g), Is.LessThanOrEqualTo(6));
            }
        }

        [Test]
        public void GroundCell_SameSeedAndTile_IsDeterministicAndOfTheTilesKind()
        {
            for (int tile = 0; tile < 2000; tile++)
            {
                int x = tile % 50;
                int y = tile / 50;
                int cell = MapTerrainLook.GroundCell(11UL, x, y);
                Assert.That(MapTerrainLook.GroundCell(11UL, x, y), Is.EqualTo(cell));
                int first;
                int count;
                switch (MapTerrainLook.KindAt(11UL, x, y))
                {
                    case TerrainKind.Sand:
                        first = MapGroundAtlas.SandFirst;
                        count = MapGroundAtlas.SandCount;
                        break;
                    case TerrainKind.Earth:
                        first = MapGroundAtlas.EarthFirst;
                        count = MapGroundAtlas.EarthCount;
                        break;
                    default:
                        first = MapGroundAtlas.GrassFirst;
                        count = MapGroundAtlas.GrassCount;
                        break;
                }

                Assert.That(cell, Is.InRange(first, first + count - 1));
            }
        }

        [Test]
        public void GroundCell_Grass_UsesEveryVariant()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int tile = 0; tile < 20000; tile++)
            {
                if (MapTerrainLook.KindAt(3UL, tile % 200, tile / 200) == TerrainKind.Grass)
                {
                    seen.Add(MapTerrainLook.GroundCell(3UL, tile % 200, tile / 200));
                }
            }

            Assert.That(seen.Count, Is.EqualTo(MapGroundAtlas.GrassCount));
        }

        [Test]
        public void FringeMask_OnTheHighestKindOrOwnKind_IsZero()
        {
            for (int tile = 0; tile < 4000; tile++)
            {
                int x = tile % 64;
                int y = tile / 64;
                TerrainKind kind = MapTerrainLook.KindAt(9UL, x, y);
                if (kind == TerrainKind.Grass)
                {
                    Assert.That(MapTerrainLook.FringeMask(9UL, x, y, TerrainKind.Grass), Is.EqualTo(0));
                    Assert.That(MapTerrainLook.FringeMask(9UL, x, y, TerrainKind.Earth), Is.EqualTo(0));
                }
                else if (kind == TerrainKind.Earth)
                {
                    Assert.That(MapTerrainLook.FringeMask(9UL, x, y, TerrainKind.Earth), Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void FringeMask_BesideAHigherKind_SetsTheBitOfThatNeighbour()
        {
            int fringed = 0;
            for (int tile = 0; tile < 16384; tile++)
            {
                int x = tile % 128;
                int y = tile / 128;
                if (MapTerrainLook.KindAt(5UL, x, y) == TerrainKind.Grass)
                {
                    continue;
                }

                int mask = MapTerrainLook.FringeMask(5UL, x, y, TerrainKind.Grass);
                Assert.That((mask & MapTileShapes.North) != 0, Is.EqualTo(MapTerrainLook.KindAt(5UL, x, y + 1) == TerrainKind.Grass));
                Assert.That((mask & MapTileShapes.East) != 0, Is.EqualTo(MapTerrainLook.KindAt(5UL, x + 1, y) == TerrainKind.Grass));
                Assert.That((mask & MapTileShapes.South) != 0, Is.EqualTo(MapTerrainLook.KindAt(5UL, x, y - 1) == TerrainKind.Grass));
                Assert.That((mask & MapTileShapes.West) != 0, Is.EqualTo(MapTerrainLook.KindAt(5UL, x - 1, y) == TerrainKind.Grass));
                fringed += mask != 0 ? 1 : 0;
            }

            Assert.That(fringed, Is.GreaterThan(0));
        }

        [Test]
        public void GroundAtlas_UvRects_StayInsideTheTexture()
        {
            for (int cell = 0; cell < MapGroundAtlas.CellCount; cell++)
            {
                Rect uv = MapGroundAtlas.GetUvRect(cell);
                Assert.That(uv.xMin, Is.GreaterThan(0f));
                Assert.That(uv.xMax, Is.LessThan(1f));
                Assert.That(uv.yMin, Is.GreaterThan(0f));
                Assert.That(uv.yMax, Is.LessThan(1f));
            }
        }
    }
}
