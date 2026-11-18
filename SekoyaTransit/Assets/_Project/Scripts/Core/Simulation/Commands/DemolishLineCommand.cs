#nullable enable
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Clears a straight line of tiles in one atomic step (click-and-drag Delete, Issue #349). Each tile is cleared
    /// as a single Delete click clears it: the station if there is one, otherwise the road and the rail. Tiles with
    /// nothing to remove are skipped; a line with nothing to remove is rejected and changes nothing.
    /// </summary>
    public sealed class DemolishLineCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly TileCoordinate _start;
        private readonly TileCoordinate _end;

        /// <summary>
        /// Initializes a demolish-line command.
        /// </summary>
        /// <param name="start">The tile where the drag started.</param>
        /// <param name="end">The tile where the drag ended; the line snaps to the dominant axis.</param>
        public DemolishLineCommand(TileCoordinate start, TileCoordinate end)
        {
            _start = start;
            _end = end;
        }

        /// <summary>
        /// Gets the first tile of the line.
        /// </summary>
        public TileCoordinate Start
        {
            get
            {
                return _start;
            }
        }

        /// <summary>
        /// Gets the tile the drag ended on, before snapping to the dominant axis.
        /// </summary>
        public TileCoordinate End
        {
            get
            {
                return _end;
            }
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            // The snapped line lies between the two ends on one axis, so both ends on the map keep it on the map.
            if (!state.Map.Contains(_start) || !state.Map.Contains(_end))
            {
                return CommandValidationResult.Rejected("Tile is outside the map.");
            }

            LineDemolishPlan plan = LineDemolishPlan.Create(state.Map, _start, _end);
            return plan.ClearedTileCount == 0
                ? CommandValidationResult.Rejected("Line has nothing to demolish.")
                : CommandValidationResult.Valid;
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            LineDemolishPlan plan = LineDemolishPlan.Create(state.Map, _start, _end);

            // Stations first, through the same removal as a single click (routes and vehicles are updated there).
            for (int i = 0; i < plan.Tiles.Count; i++)
            {
                TileDemolishTargets targets = plan.GetTargets(i);
                if (targets.RoadStation)
                {
                    state.RemoveRoadStationAt(plan.Tiles[i]);
                }

                if (targets.RailStation)
                {
                    state.RemoveRailStationAt(plan.Tiles[i]);
                }
            }

            // Then every road and rail tile, with one graph rebuild per network that changed.
            bool roadChanged = false;
            bool railChanged = false;
            for (int i = 0; i < plan.Tiles.Count; i++)
            {
                TileDemolishTargets targets = plan.GetTargets(i);
                if (targets.Road)
                {
                    state.Map.SetRoadTypeId(plan.Tiles[i], TileMap.NoRoadTypeId);
                    roadChanged = true;
                }

                if (targets.Rail)
                {
                    state.Map.SetRailTypeId(plan.Tiles[i], TileMap.NoRailTypeId);
                    railChanged = true;
                }
            }

            if (roadChanged)
            {
                RoadVehicleMovement.RebuildGraphKeepingVehicles(state);
            }

            if (railChanged)
            {
                RailVehicleMovement.RebuildGraphKeepingVehicles(state);
            }
        }
    }
}
