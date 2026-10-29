#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Tracks which map chunks need a mesh rebuild after tile data changes.
    /// </summary>
    public sealed class DirtyChunkTracker
    {
        private readonly MapSize _mapSize;
        private readonly int _chunkCountPerSide;
        private readonly HashSet<int> _dirtyChunkIndexes = new HashSet<int>();

        /// <summary>
        /// Initializes a tracker for a supported map size.
        /// </summary>
        public DirtyChunkTracker(MapSize mapSize)
        {
            _mapSize = mapSize;
            _chunkCountPerSide = MapChunkGrid.GetChunkCountPerSide(mapSize);
        }

        /// <summary>
        /// Gets the number of currently dirty chunks.
        /// </summary>
        public int DirtyCount
        {
            get
            {
                return _dirtyChunkIndexes.Count;
            }
        }

        /// <summary>
        /// Marks the chunk that owns the given tile as dirty.
        /// </summary>
        public void MarkTileDirty(int tileX, int tileY)
        {
            int chunkIndex = MapChunkGrid.GetChunkIndexForTile(tileX, tileY, _mapSize);
            _dirtyChunkIndexes.Add(chunkIndex);
        }

        /// <summary>
        /// Marks every chunk dirty.
        /// </summary>
        public void MarkAllDirty()
        {
            int total = MapChunkGrid.GetChunkCount(_mapSize);
            for (int index = 0; index < total; index++)
            {
                _dirtyChunkIndexes.Add(index);
            }
        }

        /// <summary>
        /// Copies dirty chunk indexes into <paramref name="destination"/> and clears the dirty set.
        /// </summary>
        /// <returns>The number of dirty chunks drained.</returns>
        public int DrainDirtyChunkIndexes(List<int> destination)
        {
#pragma warning disable CA1510 // Match Core null-check style for Unity C# 9.
            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
#pragma warning restore CA1510

            destination.Clear();
            foreach (int chunkIndex in _dirtyChunkIndexes)
            {
                destination.Add(chunkIndex);
            }

            destination.Sort();
            _dirtyChunkIndexes.Clear();
            return destination.Count;
        }

        /// <summary>
        /// Gets the chunk count along one side for this tracker.
        /// </summary>
        public int ChunkCountPerSide
        {
            get
            {
                return _chunkCountPerSide;
            }
        }
    }
}
