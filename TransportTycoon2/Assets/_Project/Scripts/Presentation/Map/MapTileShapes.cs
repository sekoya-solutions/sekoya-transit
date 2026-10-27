#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Picks the road or rail atlas cell for a tile from its four orthogonal neighbors (ADR 0032).
    /// Mask bits follow Kenney's direction letters: N = +Y (screen upper-left), E = +X (upper-right),
    /// S = -Y (lower-right), W = -X (lower-left).
    /// </summary>
    public static class MapTileShapes
    {
        public const int North = 1;
        public const int East = 2;
        public const int South = 4;
        public const int West = 8;

        /// <summary>
        /// Returns the sides whose neighbor carries road or a road station.
        /// </summary>
        public static int GetRoadMask(TileMap map, TileCoordinate coordinate)
        {
            int mask = 0;
            if (HasRoadLink(map, coordinate.X, coordinate.Y + 1))
            {
                mask |= North;
            }

            if (HasRoadLink(map, coordinate.X + 1, coordinate.Y))
            {
                mask |= East;
            }

            if (HasRoadLink(map, coordinate.X, coordinate.Y - 1))
            {
                mask |= South;
            }

            if (HasRoadLink(map, coordinate.X - 1, coordinate.Y))
            {
                mask |= West;
            }

            return mask;
        }

        /// <summary>
        /// Returns the sides whose neighbor carries rail or a rail station.
        /// </summary>
        public static int GetRailMask(TileMap map, TileCoordinate coordinate)
        {
            int mask = 0;
            if (HasRailLink(map, coordinate.X, coordinate.Y + 1))
            {
                mask |= North;
            }

            if (HasRailLink(map, coordinate.X + 1, coordinate.Y))
            {
                mask |= East;
            }

            if (HasRailLink(map, coordinate.X, coordinate.Y - 1))
            {
                mask |= South;
            }

            if (HasRailLink(map, coordinate.X - 1, coordinate.Y))
            {
                mask |= West;
            }

            return mask;
        }

        /// <summary>
        /// Road cell for a neighbor mask. A tile with no neighbor is drawn as a straight N-S road.
        /// </summary>
        public static int RoadCell(int mask)
        {
            switch (mask & 15)
            {
                case North:
                    return MapTileAtlas.RoadEndN;
                case East:
                    return MapTileAtlas.RoadEndE;
                case South:
                    return MapTileAtlas.RoadEndS;
                case West:
                    return MapTileAtlas.RoadEndW;
                case East | West:
                    return MapTileAtlas.RoadEW;
                case North | East:
                    return MapTileAtlas.RoadNE;
                case East | South:
                    return MapTileAtlas.RoadES;
                case South | West:
                    return MapTileAtlas.RoadSW;
                case North | West:
                    return MapTileAtlas.RoadNW;
                case North | East | South:
                    return MapTileAtlas.RoadNES;
                case East | South | West:
                    return MapTileAtlas.RoadESW;
                case North | South | West:
                    return MapTileAtlas.RoadNSW;
                case North | East | West:
                    return MapTileAtlas.RoadNEW;
                case North | East | South | West:
                    return MapTileAtlas.RoadCross;
                default:
                    return MapTileAtlas.RoadNS;
            }
        }

        /// <summary>
        /// Rail cell for a neighbor mask. Dead ends and lone tiles are drawn straight along their axis.
        /// </summary>
        public static int RailCell(int mask)
        {
            switch (mask & 15)
            {
                case East:
                case West:
                case East | West:
                    return MapTileAtlas.RailEW;
                case North | East:
                    return MapTileAtlas.RailNE;
                case East | South:
                    return MapTileAtlas.RailES;
                case South | West:
                    return MapTileAtlas.RailSW;
                case North | West:
                    return MapTileAtlas.RailNW;
                case North | East | South:
                    return MapTileAtlas.RailNES;
                case East | South | West:
                    return MapTileAtlas.RailESW;
                case North | South | West:
                    return MapTileAtlas.RailNSW;
                case North | East | West:
                    return MapTileAtlas.RailNEW;
                case North | East | South | West:
                    return MapTileAtlas.RailCross;
                default:
                    return MapTileAtlas.RailNS;
            }
        }

        /// <summary>
        /// Rail cell drawn over a road on a level crossing: rails without sleepers when the rail runs straight,
        /// otherwise the full rail shape.
        /// </summary>
        public static int LevelCrossingRailCell(int railMask)
        {
            int cell = RailCell(railMask);
            if (cell == MapTileAtlas.RailNS)
            {
                return MapTileAtlas.RailsOnlyNS;
            }

            if (cell == MapTileAtlas.RailEW)
            {
                return MapTileAtlas.RailsOnlyEW;
            }

            return cell;
        }

        private static bool HasRoadLink(TileMap map, int x, int y)
        {
            var neighbor = new TileCoordinate(x, y);
            return map.Contains(neighbor)
                && (map.GetRoadTypeId(neighbor) != TileMap.NoRoadTypeId
                    || map.GetStationId(neighbor) != TileMap.NoStationId);
        }

        private static bool HasRailLink(TileMap map, int x, int y)
        {
            var neighbor = new TileCoordinate(x, y);
            return map.Contains(neighbor)
                && (map.GetRailTypeId(neighbor) != TileMap.NoRailTypeId
                    || map.GetRailStationId(neighbor) != TileMap.NoRailStationId);
        }
    }
}
