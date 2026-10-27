#nullable enable
using TransportTycoon.Core.Simulation;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
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

        // Per-tile brightness jitter (+/- 2.5 %) so tile borders still read without grid lines.
        private const float ShadeAmplitude = 0.025f;

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
