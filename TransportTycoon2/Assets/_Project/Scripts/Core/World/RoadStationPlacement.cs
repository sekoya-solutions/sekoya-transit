#nullable enable

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Shared V1 road-station placement rules for commands and presentation ghosts.
    /// </summary>
    public static class RoadStationPlacement
    {
        private static readonly int[] _offsetX = { 0, 1, 0, -1 };
        private static readonly int[] _offsetY = { -1, 0, 1, 0 };

        /// <summary>
        /// Returns whether a 1x1 road station may be built on the tile.
        /// </summary>
        public static bool CanBuild(TileMap map, TileCoordinate coordinate)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new System.ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            if (!map.Contains(coordinate))
            {
                return false;
            }

            // Station tile is a clean platform: no object, road, rail, or either station type.
            // Matching road attaches on an adjacent tile (not required at build time).
            if (map.GetObjectId(coordinate) != TileMap.NoObjectId
                || map.GetRoadTypeId(coordinate) != TileMap.NoRoadTypeId
                || map.GetRailTypeId(coordinate) != TileMap.NoRailTypeId
                || map.GetStationId(coordinate) != TileMap.NoStationId
                || map.GetRailStationId(coordinate) != TileMap.NoRailStationId)
            {
                return false;
            }

            return HasOrthogonalTownOrIndustryNeighbor(map, coordinate);
        }

        /// <summary>
        /// Returns whether an existing road station may be demolished on the tile.
        /// </summary>
        public static bool CanDemolish(TileMap map, TileCoordinate coordinate)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new System.ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            if (!map.Contains(coordinate))
            {
                return false;
            }

            return map.GetStationId(coordinate) != TileMap.NoStationId;
        }

        /// <summary>
        /// Returns whether any orthogonal neighbor holds a town or industry object.
        /// </summary>
        public static bool HasOrthogonalTownOrIndustryNeighbor(TileMap map, TileCoordinate coordinate)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new System.ArgumentNullException(nameof(map));
            }
#pragma warning restore CA1510

            for (int direction = 0; direction < 4; direction++)
            {
                var neighbor = new TileCoordinate(
                    coordinate.X + _offsetX[direction],
                    coordinate.Y + _offsetY[direction]);
                if (map.Contains(neighbor) && map.GetObjectId(neighbor) != TileMap.NoObjectId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
