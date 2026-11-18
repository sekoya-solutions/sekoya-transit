#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A straight line of tiles planned for a click-and-drag Delete (Issue #349), shared by the line command and
    /// the presentation ghosts. The line snaps like a drag build (<see cref="LineBuildPlan.SnapLine"/>); each tile
    /// is cleared as a single Delete click clears it (<see cref="TileDemolishTargets"/>), and a tile with nothing
    /// to remove is skipped.
    /// </summary>
    public sealed class LineDemolishPlan
    {
        private readonly TileCoordinate[] _tiles;
        private readonly TileDemolishTargets[] _targets;
        private readonly int _clearedTileCount;

        private LineDemolishPlan(TileCoordinate[] tiles, TileDemolishTargets[] targets, int clearedTileCount)
        {
            _tiles = tiles;
            _targets = targets;
            _clearedTileCount = clearedTileCount;
        }

        /// <summary>
        /// Gets the line tiles in order from the start tile to the snapped end tile.
        /// </summary>
        public IReadOnlyList<TileCoordinate> Tiles => _tiles;

        /// <summary>
        /// Gets how many tiles of the line have something to remove.
        /// </summary>
        public int ClearedTileCount => _clearedTileCount;

        /// <summary>
        /// Returns what is removed on the tile at the index; nothing for a tile off the map or with nothing on it.
        /// </summary>
        public TileDemolishTargets GetTargets(int index)
        {
            return _targets[index];
        }

        /// <summary>
        /// Plans a Delete line on the map from the start toward the end, snapped to the dominant axis.
        /// </summary>
        public static LineDemolishPlan Create(TileMap map, TileCoordinate start, TileCoordinate end)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            TileCoordinate[] tiles = LineBuildPlan.SnapLine(start, end);
            var targets = new TileDemolishTargets[tiles.Length];
            int cleared = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                targets[i] = TileDemolishTargets.Resolve(map, tiles[i]);
                if (targets[i].Any)
                {
                    cleared++;
                }
            }

            return new LineDemolishPlan(tiles, targets, cleared);
        }
    }
}
