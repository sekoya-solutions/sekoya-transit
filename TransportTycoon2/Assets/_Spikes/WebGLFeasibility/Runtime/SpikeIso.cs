#nullable enable
using UnityEngine;

namespace TransportTycoon.Spikes.WebGL
{
    public enum SpikeMapMode
    {
        Mesh,
        Tilemap,
    }

    // Shared isometric conventions: one tile is a 1 x 0.5 world-unit diamond.
    public static class SpikeIso
    {
        public const int MapSize = 512;
        public const int ChunkSize = 32;
        public const int TextureWidth = 64;
        public const int TextureHeight = 32;
        public const float PixelsPerUnit = 64f;

        public static readonly Color32[] Palette =
        {
            new Color32(76, 154, 62, 255),
            new Color32(94, 170, 70, 255),
            new Color32(60, 135, 55, 255),
            new Color32(150, 160, 70, 255),
            new Color32(120, 100, 70, 255),
            new Color32(90, 140, 120, 255),
            new Color32(110, 150, 60, 255),
            new Color32(140, 125, 85, 255),
        };

        public static Vector3 TileToWorld(float x, float y)
        {
            return new Vector3((x - y) * 0.5f, (x + y) * 0.25f, 0f);
        }

        public static int TerrainAt(int x, int y)
        {
            int h = ((x >> 3) * 73856093) ^ ((y >> 3) * 19349663);
            h ^= h >> 13;
            return (h & 0x7FFFFFFF) % Palette.Length;
        }

        public static Texture2D CreateDiamondTexture()
        {
            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[TextureWidth * TextureHeight];
            for (int py = 0; py < TextureHeight; py++)
            {
                for (int px = 0; px < TextureWidth; px++)
                {
                    float dx = Mathf.Abs(px + 0.5f - TextureWidth * 0.5f) / (TextureWidth * 0.5f);
                    float dy = Mathf.Abs(py + 0.5f - TextureHeight * 0.5f) / (TextureHeight * 0.5f);
                    bool inside = dx + dy <= 1f;
                    pixels[py * TextureWidth + px] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
