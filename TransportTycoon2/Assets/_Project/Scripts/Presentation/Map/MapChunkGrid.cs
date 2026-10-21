#nullable enable
using System;
using TransportTycoon.Core.World;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Chunk indexing for the ADR 0017 32×32 mesh map. Pure helpers with no Unity types.
    /// </summary>
    public static class MapChunkGrid
    {
        /// <summary>
        /// Gets the fixed chunk edge length in tiles.
        /// </summary>
        public const int ChunkSizeTiles = 32;

        /// <summary>
        /// Returns the number of chunks along one side of a supported square map.
        /// </summary>
        public static int GetChunkCountPerSide(MapSize mapSize)
        {
            int sideLength = (int)mapSize;
            if (sideLength != 128 && sideLength != 256 && sideLength != 512)
            {
                throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size must be 128, 256, or 512.");
            }

            if (sideLength % ChunkSizeTiles != 0)
            {
                throw new InvalidOperationException("Map side length must be divisible by the chunk size.");
            }

            return sideLength / ChunkSizeTiles;
        }

        /// <summary>
        /// Returns the total chunk count for a supported map size.
        /// </summary>
        public static int GetChunkCount(MapSize mapSize)
        {
            int perSide = GetChunkCountPerSide(mapSize);
            return checked(perSide * perSide);
        }

        /// <summary>
        /// Converts a tile coordinate to its chunk coordinates.
        /// </summary>
        public static void GetChunkCoordinates(int tileX, int tileY, out int chunkX, out int chunkY)
        {
            if (tileX < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileX));
            }

            if (tileY < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileY));
            }

            chunkX = tileX / ChunkSizeTiles;
            chunkY = tileY / ChunkSizeTiles;
        }

        /// <summary>
        /// Converts chunk coordinates to a dense row-major chunk index.
        /// </summary>
        public static int GetChunkIndex(int chunkX, int chunkY, int chunkCountPerSide)
        {
            if (chunkCountPerSide < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkCountPerSide));
            }

            if (chunkX < 0 || chunkX >= chunkCountPerSide)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkX));
            }

            if (chunkY < 0 || chunkY >= chunkCountPerSide)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkY));
            }

            return checked((chunkY * chunkCountPerSide) + chunkX);
        }

        /// <summary>
        /// Converts a tile coordinate to a dense row-major chunk index for the given map size.
        /// </summary>
        public static int GetChunkIndexForTile(int tileX, int tileY, MapSize mapSize)
        {
            int perSide = GetChunkCountPerSide(mapSize);
            int sideLength = (int)mapSize;
            if (tileX < 0 || tileX >= sideLength)
            {
                throw new ArgumentOutOfRangeException(nameof(tileX));
            }

            if (tileY < 0 || tileY >= sideLength)
            {
                throw new ArgumentOutOfRangeException(nameof(tileY));
            }

            GetChunkCoordinates(tileX, tileY, out int chunkX, out int chunkY);
            return GetChunkIndex(chunkX, chunkY, perSide);
        }
    }
}
