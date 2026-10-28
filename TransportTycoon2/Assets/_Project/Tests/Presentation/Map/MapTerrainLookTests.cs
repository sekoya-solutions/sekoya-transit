#nullable enable
using NUnit.Framework;
using TransportTycoon.Presentation.Map;
using UnityEngine;

namespace TransportTycoon.Presentation.Tests.Map
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
    }
}
