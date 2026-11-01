#nullable enable
using System;
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Layout of the original ground atlas (Issue #272): square cells drawn in tile space (the GPU turns each into a
    /// diamond), 128 pixels with an 8 pixel padding so mip levels do not bleed. Cells 0-15 are base terrain variants,
    /// 16 + mask the grass fringe and 32 + mask the earth fringe (mask bits as in <see cref="MapTileShapes"/>).
    /// Cells are numbered row by row from the texture's bottom-left corner.
    /// </summary>
    public static class MapGroundAtlas
    {
        public const int CellSize = 128;
        public const int Padding = 8;
        public const int Columns = 8;
        public const int Rows = 6;
        public const int Stride = CellSize + (2 * Padding);
        public const int TextureWidth = Columns * Stride;
        public const int TextureHeight = Rows * Stride;

        public const int GrassFirst = 0;
        public const int GrassCount = 8;
        public const int SandFirst = 8;
        public const int SandCount = 4;
        public const int EarthFirst = 12;
        public const int EarthCount = 4;
        public const int GrassFringeBase = 16;
        public const int EarthFringeBase = 32;

        public const int CellCount = Columns * Rows;

        /// <summary>Pixel rectangle of a cell (padding excluded), origin at the texture's bottom-left.</summary>
        public static RectInt GetPixelRect(int cell)
        {
            if (cell < 0 || cell >= CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Ground atlas cell is out of range.");
            }

            return new RectInt(((cell % Columns) * Stride) + Padding, ((cell / Columns) * Stride) + Padding, CellSize, CellSize);
        }

        /// <summary>Normalized texture coordinates of a cell.</summary>
        public static Rect GetUvRect(int cell)
        {
            RectInt pixels = GetPixelRect(cell);
            return new Rect(
                (float)pixels.x / TextureWidth,
                (float)pixels.y / TextureHeight,
                (float)pixels.width / TextureWidth,
                (float)pixels.height / TextureHeight);
        }
    }
}
