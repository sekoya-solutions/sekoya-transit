#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Which kind of track a drag-built line places.
    /// </summary>
    public enum LineBuildKind
    {
        /// <summary>
        /// Basic road.
        /// </summary>
        Road,

        /// <summary>
        /// Basic rail track.
        /// </summary>
        Rail,
    }

    /// <summary>
    /// A straight line of tiles planned for a click-and-drag road or rail build (Issue #209), shared by
    /// the line commands and the presentation ghosts.
    /// </summary>
    public sealed class LineBuildPlan
    {
        private readonly TileCoordinate[] _tiles;
        private readonly bool[] _isValid;
        private readonly int _newTileCount;

        private LineBuildPlan(TileCoordinate[] tiles, bool[] isValid, int newTileCount)
        {
            _tiles = tiles;
            _isValid = isValid;
            _newTileCount = newTileCount;
        }

        /// <summary>
        /// Gets the line tiles in order from the start tile to the snapped end tile.
        /// </summary>
        public IReadOnlyList<TileCoordinate> Tiles => _tiles;

        /// <summary>
        /// Gets how many tiles would receive new road or rail.
        /// </summary>
        public int NewTileCount => _newTileCount;

        /// <summary>
        /// Gets whether the whole line is buildable: no obstacle and at least one new tile.
        /// </summary>
        public bool IsBuildable
        {
            get
            {
                if (_newTileCount == 0)
                {
                    return false;
                }

                for (int i = 0; i < _isValid.Length; i++)
                {
                    if (!_isValid[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Returns whether the tile at the index is shown valid: false for the first obstacle and every tile after it.
        /// </summary>
        public bool IsTileValid(int index)
        {
            return _isValid[index];
        }

        /// <summary>
        /// Returns the tiles of a straight line from the start toward the end, snapped to the dominant axis
        /// (X wins a tie). The start tile is always first; a zero-length drag yields just the start tile.
        /// </summary>
        public static TileCoordinate[] SnapLine(TileCoordinate start, TileCoordinate end)
        {
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;
            bool alongX = Math.Abs(dx) >= Math.Abs(dy);
            int length = (alongX ? Math.Abs(dx) : Math.Abs(dy)) + 1;
            int stepX = alongX ? Math.Sign(dx) : 0;
            int stepY = alongX ? 0 : Math.Sign(dy);
            var tiles = new TileCoordinate[length];
            for (int i = 0; i < length; i++)
            {
                tiles[i] = new TileCoordinate(start.X + (stepX * i), start.Y + (stepY * i));
            }

            return tiles;
        }

        /// <summary>
        /// Plans a line on the map. A tile is an obstacle when it is off the map or cannot take the track and
        /// does not already hold the same track; tiles that already hold it are kept and cost nothing.
        /// </summary>
        public static LineBuildPlan Create(TileMap map, LineBuildKind kind, TileCoordinate start, TileCoordinate end)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            TileCoordinate[] tiles = SnapLine(start, end);
            var isValid = new bool[tiles.Length];
            int newTiles = 0;
            bool blocked = false;
            for (int i = 0; i < tiles.Length; i++)
            {
                bool hasTrack = HasTrack(map, kind, tiles[i]);
                bool canBuild = !hasTrack && CanBuild(map, kind, tiles[i]);
                if (!hasTrack && !canBuild)
                {
                    blocked = true;
                }

                isValid[i] = !blocked;
                if (canBuild)
                {
                    newTiles++;
                }
            }

            return new LineBuildPlan(tiles, isValid, newTiles);
        }

        /// <summary>
        /// Returns the rejection reason for a plan that is not buildable, or null when it is.
        /// </summary>
        public string? GetRejectionReason(TileMap map, LineBuildKind kind)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            for (int i = 0; i < _tiles.Length; i++)
            {
                if (!_isValid[i])
                {
                    return map.Contains(_tiles[i])
                        ? "Line is blocked by a town, industry or station."
                        : "Line leaves the map.";
                }
            }

            if (_newTileCount > 0)
            {
                return null;
            }

            return kind == LineBuildKind.Road ? "Line already has a road." : "Line already has rail track.";
        }

        private static bool HasTrack(TileMap map, LineBuildKind kind, TileCoordinate tile)
        {
            if (!map.Contains(tile))
            {
                return false;
            }

            return kind == LineBuildKind.Road
                ? map.GetRoadTypeId(tile) != TileMap.NoRoadTypeId
                : map.GetRailTypeId(tile) != TileMap.NoRailTypeId;
        }

        private static bool CanBuild(TileMap map, LineBuildKind kind, TileCoordinate tile)
        {
            return kind == LineBuildKind.Road
                ? RoadPlacement.CanBuild(map, tile)
                : RailPlacement.CanBuild(map, tile);
        }
    }
}
