#nullable enable
using TransportTycoon.Core.World;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// What the single Delete tool removes on a tile (Issue #181): the station if there is one, otherwise the road
    /// and the rail (both when both exist). Core never allows a station on a road or rail tile.
    /// </summary>
    public readonly struct TileDemolishTargets
    {
        private TileDemolishTargets(bool roadStation, bool railStation, bool road, bool rail)
        {
            RoadStation = roadStation;
            RailStation = railStation;
            Road = road;
            Rail = rail;
        }

        /// <summary>
        /// Gets whether a road station is removed.
        /// </summary>
        public bool RoadStation { get; }

        /// <summary>
        /// Gets whether a rail station is removed.
        /// </summary>
        public bool RailStation { get; }

        /// <summary>
        /// Gets whether the road is removed.
        /// </summary>
        public bool Road { get; }

        /// <summary>
        /// Gets whether the rail is removed.
        /// </summary>
        public bool Rail { get; }

        /// <summary>
        /// Gets whether the tile has anything to remove.
        /// </summary>
        public bool Any => RoadStation || RailStation || Road || Rail;

        /// <summary>
        /// Resolves the removals for the tile. Out-of-map tiles have none.
        /// </summary>
        public static TileDemolishTargets Resolve(TileMap map, TileCoordinate coordinate)
        {
            bool roadStation = RoadStationPlacement.CanDemolish(map, coordinate);
            bool railStation = RailStationPlacement.CanDemolish(map, coordinate);
            if (roadStation || railStation)
            {
                return new TileDemolishTargets(roadStation, railStation, road: false, rail: false);
            }

            return new TileDemolishTargets(
                roadStation: false,
                railStation: false,
                RoadPlacement.CanDemolish(map, coordinate),
                RailPlacement.CanDemolish(map, coordinate));
        }
    }
}
