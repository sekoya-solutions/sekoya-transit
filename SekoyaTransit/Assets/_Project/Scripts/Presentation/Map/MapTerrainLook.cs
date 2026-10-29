#nullable enable
using SekoyaTransit.Core.Simulation;
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Decorative terrain colors (ADR 0032): mostly grass with small sand and earth patches. Derived from the world
    /// seed and tile coordinate only, so the same seed always looks the same; nothing is stored or simulated.
    /// Colors are the top faces of Kenney Isometric Landscape tiles 067, 059, and 083.
    /// </summary>
    public static class MapTerrainLook
    {
        public static readonly Color32 Grass = new Color32(138, 181, 73, 255);
        public static readonly Color32 Sand = new Color32(216, 209, 158, 255);
        public static readonly Color32 Earth = new Color32(186, 140, 93, 255);

        /// <summary>Lower-left (W) map edge strip, the lit side of a Kenney block.</summary>
        public static readonly Color32 EdgeLeft = new Color32(176, 126, 80, 255);

        /// <summary>Lower-right (S) map edge strip, the shaded side of a Kenney block.</summary>
        public static readonly Color32 EdgeRight = new Color32(150, 104, 64, 255);

        // Patch size: noise is sampled every 1/8 lattice unit per tile, so patches span a few tiles.
        private const int NoiseTilesPerLatticeUnit = 8;
        private const double SandThreshold = 0.3;
        private const double EarthThreshold = 0.3;
        private const ulong SandSeedSalt = 0x53414E44UL;
        private const ulong EarthSeedSalt = 0x45415254UL;
        private const ulong ShadeSeedSalt = 0x53484144UL;

        // Per-tile brightness jitter (+/- 2.5 %) so neighboring tiles differ slightly, on top of the grid lines.
        private const float ShadeAmplitude = 0.025f;

        /// <summary>
        /// Side of the square tile grid texture (Issue #193): one ground diamond maps the whole texture, so its one
        /// pixel border draws a thin line along each tile edge.
        /// </summary>
        public const int GridTextureSize = 64;

        /// <summary>Brightness of the grid line relative to the tile color (multiplied with the vertex color).</summary>
        public const float GridLineShade = 0.8f;

        /// <summary>
        /// Builds the tile grid texture: white inside, <see cref="GridLineShade"/> gray on the one pixel border. Mip
        /// maps fade the lines when the camera zooms out. The caller owns (and destroys) the texture.
        /// </summary>
        public static Texture2D CreateGridTexture()
        {
            const int size = GridTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "MapTileGrid",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            var pixels = new Color32[size * size];
            var line = new Color(GridLineShade, GridLineShade, GridLineShade, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    pixels[(y * size) + x] = border ? (Color32)line : new Color32(255, 255, 255, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        public static TerrainKind KindAt(ulong mapSeed, int tileX, int tileY)
        {
            if (Noise(mapSeed ^ SandSeedSalt, tileX, tileY) > SandThreshold)
            {
                return TerrainKind.Sand;
            }

            if (Noise(mapSeed ^ EarthSeedSalt, tileX, tileY) > EarthThreshold)
            {
                return TerrainKind.Earth;
            }

            return TerrainKind.Grass;
        }

        public static Color32 ColorAt(ulong mapSeed, int tileX, int tileY)
        {
            Color32 baseColor;
            switch (KindAt(mapSeed, tileX, tileY))
            {
                case TerrainKind.Sand:
                    baseColor = Sand;
                    break;
                case TerrainKind.Earth:
                    baseColor = Earth;
                    break;
                default:
                    baseColor = Grass;
                    break;
            }

            float shade = 1f + (ShadeAmplitude * ((2f * Hash01(mapSeed ^ ShadeSeedSalt, tileX, tileY)) - 1f));
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(baseColor.r * shade), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(baseColor.g * shade), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(baseColor.b * shade), 0, 255),
                255);
        }

        private static double Noise(ulong seed, int tileX, int tileY)
        {
            long step = FixedPoint.OneRawValue / NoiseTilesPerLatticeUnit;
            FixedPoint value = GradientNoise2D.Sample(
                seed,
                FixedPoint.FromRaw(tileX * step),
                FixedPoint.FromRaw(tileY * step));
            return (double)value.RawValue / FixedPoint.OneRawValue;
        }

        private static float Hash01(ulong seed, int tileX, int tileY)
        {
            // SplitMix64 finalizer over the seed and packed coordinate.
            ulong z = seed + ((ulong)(uint)tileX << 32) + (uint)tileY + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (float)(z >> 40) / (1UL << 24);
        }
    }

    public enum TerrainKind
    {
        Grass = 0,
        Sand = 1,
        Earth = 2,
    }
}
