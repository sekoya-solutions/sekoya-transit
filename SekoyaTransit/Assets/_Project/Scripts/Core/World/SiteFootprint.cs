#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// The square block of tiles a town (3x3) or an industry (2x2) occupies. A site's anchor is the block's lowest-X,
    /// lowest-Y tile; every tile of the block carries the site's object ID, so building and station adjacency need no
    /// site-specific code.
    /// </summary>
    public static class SiteFootprint
    {
        /// <summary>
        /// The side length in tiles of an industry block (2x2 = 4 tiles).
        /// </summary>
        public const int IndustrySize = 2;

        /// <summary>
        /// The side length in tiles of a town block (3x3 = 9 tiles): towns are bigger than one industry building and
        /// leave room around them for stations.
        /// </summary>
        public const int TownSize = 3;

        /// <summary>
        /// The free tiles kept between a block and each map edge (Issue #221).
        /// </summary>
        public const int BorderMargin = 1;

        /// <summary>
        /// Returns whether a whole block of <paramref name="size"/> tiles anchored at <paramref name="anchor"/> fits inside
        /// the map, leaving <see cref="BorderMargin"/> free tiles to every edge.
        /// </summary>
        public static bool Fits(TileCoordinate anchor, int sideLength, int size)
        {
            return anchor.X >= BorderMargin
                && anchor.Y >= BorderMargin
                && anchor.X + size + BorderMargin <= sideLength
                && anchor.Y + size + BorderMargin <= sideLength;
        }

        /// <summary>
        /// Returns whether <paramref name="tile"/> is one of the block's tiles for the block anchored at
        /// <paramref name="anchor"/>.
        /// </summary>
        public static bool Contains(TileCoordinate anchor, TileCoordinate tile, int size)
        {
            return tile.X >= anchor.X
                && tile.Y >= anchor.Y
                && tile.X < anchor.X + size
                && tile.Y < anchor.Y + size;
        }

        /// <summary>
        /// Writes <paramref name="objectId"/> on every free tile of the block anchored at <paramref name="anchor"/>.
        /// Tiles outside the map and tiles already holding an object, a road, a rail, or a station are left alone, so a
        /// save from before the 2x2 or 3x3 footprint keeps its existing transport.
        /// </summary>
        public static void Stamp(TileMap map, TileCoordinate anchor, int objectId, int size)
        {
#pragma warning disable CA1510
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    var tile = new TileCoordinate(anchor.X + dx, anchor.Y + dy);
                    if (!map.Contains(tile) || !IsFree(map, tile))
                    {
                        continue;
                    }

                    map.SetObjectId(tile, objectId);
                }
            }
        }

        private static bool IsFree(TileMap map, TileCoordinate tile)
        {
            return map.GetObjectId(tile) == TileMap.NoObjectId
                && map.GetRoadTypeId(tile) == TileMap.NoRoadTypeId
                && map.GetRailTypeId(tile) == TileMap.NoRailTypeId
                && map.GetStationId(tile) == TileMap.NoStationId
                && map.GetRailStationId(tile) == TileMap.NoRailStationId;
        }
    }
}
