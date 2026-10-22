#nullable enable

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Shared V1 road placement rules for commands and presentation ghosts.
    /// </summary>
    public static class RoadPlacement
    {
        /// <summary>
        /// Returns whether a basic road may be built on the tile.
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

            return map.GetObjectId(coordinate) == TileMap.NoObjectId
                && map.GetRoadTypeId(coordinate) == TileMap.NoRoadTypeId
                && map.GetStationId(coordinate) == TileMap.NoStationId;
        }

        /// <summary>
        /// Returns whether an existing road may be demolished on the tile.
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

            return map.GetRoadTypeId(coordinate) != TileMap.NoRoadTypeId;
        }
    }
}
