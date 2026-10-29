#nullable enable
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Places basic rail track on a straight line of tiles in one atomic step (click and drag, Issue #209).
    /// </summary>
    public sealed class BuildRailLineCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly TileCoordinate _start;
        private readonly TileCoordinate _end;

        /// <summary>
        /// Initializes a build-rail-line command.
        /// </summary>
        /// <param name="start">The tile where the drag started.</param>
        /// <param name="end">The tile where the drag ended; the line snaps to the dominant axis.</param>
        public BuildRailLineCommand(TileCoordinate start, TileCoordinate end)
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

            if (!state.Map.Contains(_end))
            {
                return CommandValidationResult.Rejected("Tile is outside the map.");
            }

            LineBuildPlan plan = LineBuildPlan.Create(state.Map, LineBuildKind.Rail, _start, _end);
            string? reason = plan.GetRejectionReason(state.Map, LineBuildKind.Rail);
            return reason == null ? CommandValidationResult.Valid : CommandValidationResult.Rejected(reason);
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

            LineBuildPlan plan = LineBuildPlan.Create(state.Map, LineBuildKind.Rail, _start, _end);
            for (int i = 0; i < plan.Tiles.Count; i++)
            {
                TileCoordinate tile = plan.Tiles[i];
                if (state.Map.GetRailTypeId(tile) == TileMap.NoRailTypeId)
                {
                    state.Map.SetRailTypeId(tile, TileMap.BasicRailTypeId);
                }
            }

            RailVehicleMovement.RebuildGraphKeepingVehicles(state);
        }
    }
}
