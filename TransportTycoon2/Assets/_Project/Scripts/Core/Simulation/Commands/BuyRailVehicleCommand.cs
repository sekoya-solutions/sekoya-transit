#nullable enable
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Spawns a locomotive-only rail vehicle at an existing rail station and debits its purchase price.
    /// </summary>
    public sealed class BuyRailVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _stationId;
        private readonly EconomySettings _economy;

        /// <summary>
        /// Initializes a buy-vehicle command.
        /// </summary>
        /// <param name="stationId">The station where the vehicle should spawn.</param>
        /// <param name="economy">Purchase prices; placeholders when omitted.</param>
        public BuyRailVehicleCommand(int stationId, EconomySettings? economy = null)
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

            if (!state.TryGetRailStation(_stationId, out RailStation? station) || station == null)
            {
                return CommandValidationResult.Rejected("Rail station does not exist.");
            }

            if (!state.RailGraph.TryGetNodeId(station.Coordinate, out _))
            {
                return CommandValidationResult.Rejected("Rail station is not present in the rail graph.");
            }

            if (state.CompanyCash < _economy.RailLocomotivePurchase)
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

            if (!state.TryDebitCompanyCash(_economy.RailLocomotivePurchase))
            {
                throw new System.InvalidOperationException("Company cash is insufficient to buy a rail vehicle.");
            }

            state.AddRailVehicle(_stationId);
        }
    }
}
