#nullable enable
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Spawns a placeholder road vehicle at an existing road station and debits its purchase price.
    /// </summary>
    public sealed class BuyRoadVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _stationId;
        private readonly EconomySettings _economy;

        /// <summary>
        /// Initializes a buy-vehicle command.
        /// </summary>
        /// <param name="stationId">The station where the vehicle should spawn.</param>
        /// <param name="economy">Purchase prices; placeholders when omitted.</param>
        public BuyRoadVehicleCommand(int stationId, EconomySettings? economy = null)
        {
            _stationId = stationId;
            _economy = economy ?? EconomySettings.CreatePlaceholder();
        }

        /// <summary>
        /// Gets the spawn station id.
        /// </summary>
        public int StationId
        {
            get
            {
                return _stationId;
            }
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRoadStation(_stationId, out RoadStation? station) || station == null)
            {
                return CommandValidationResult.Rejected("Road station does not exist.");
            }

            if (!state.RoadGraph.TryGetNodeId(station.Coordinate, out _))
            {
                return CommandValidationResult.Rejected("Road station is not present in the road graph.");
            }

            if (state.CompanyCash < _economy.RoadVehiclePurchase)
            {
                return CommandValidationResult.Rejected("Insufficient company cash.");
            }

            return CommandValidationResult.Valid;
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryDebitCompanyCash(_economy.RoadVehiclePurchase))
            {
                throw new System.InvalidOperationException("Company cash is insufficient to buy a road vehicle.");
            }

            state.AddRoadVehicle(_stationId);
        }
    }
}
