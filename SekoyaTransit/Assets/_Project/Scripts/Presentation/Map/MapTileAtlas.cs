#nullable enable
using System;
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Layout of the baked road and rail atlas (ADR 0032): a grid of tile diamonds, each 128 x 64 pixels with a
    /// transparent padding so neighboring cells do not bleed when filtered. Shared by the bake tool and the map mesh.
    /// Cells are numbered row by row from the texture's bottom-left corner.
    /// </summary>
    public static class MapTileAtlas
    {
        public const int CellWidth = 128;
        public const int CellHeight = 64;
        public const int Padding = 4;
        public const int Columns = 8;
        public const int Rows = 4;
        public const int StrideX = CellWidth + (2 * Padding);
        public const int StrideY = CellHeight + (2 * Padding);
        public const int TextureWidth = Columns * StrideX;
        public const int TextureHeight = Rows * StrideY;

        public const int RoadNS = 0;
        public const int RoadEW = 1;
        public const int RoadNES = 2;
        public const int RoadESW = 3;
        public const int RoadNSW = 4;
        public const int RoadNEW = 5;
        public const int RoadCross = 6;
        public const int RoadNE = 7;
        public const int RoadES = 8;
        public const int RoadSW = 9;
        public const int RoadNW = 10;
        public const int RoadEndN = 11;
        public const int RoadEndE = 12;
        public const int RoadEndS = 13;
        public const int RoadEndW = 14;

        public const int RailNS = 16;
        public const int RailEW = 17;
        public const int RailNES = 18;
        public const int RailESW = 19;
        public const int RailNSW = 20;
        public const int RailNEW = 21;
        public const int RailCross = 22;
        public const int RailNE = 23;
        public const int RailES = 24;
        public const int RailSW = 25;
        public const int RailNW = 26;
        public const int RailsOnlyNS = 27;
        public const int RailsOnlyEW = 28;

        public const int CellCount = Columns * Rows;

        /// <summary>
        /// Pixel rectangle of a cell's diamond (padding excluded), origin at the texture's bottom-left.
        /// </summary>
        public static RectInt GetPixelRect(int cell)
        {
            if (cell < 0 || cell >= CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Atlas cell is out of range.");
            }

            int column = cell % Columns;
            int row = cell / Columns;
            return new RectInt((column * StrideX) + Padding, (row * StrideY) + Padding, CellWidth, CellHeight);
        }

        /// <summary>
        /// Normalized texture coordinates of a cell's diamond.
        /// </summary>
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
