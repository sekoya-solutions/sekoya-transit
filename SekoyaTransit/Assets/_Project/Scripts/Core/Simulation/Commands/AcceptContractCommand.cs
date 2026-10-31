#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Accepts a contract proposal (Issue #227): its deadline starts now and deliveries count from now on. Refused when
    /// the accepted limit of the map size is reached.
    /// </summary>
    public sealed class AcceptContractCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _contractId;
        private readonly GameDate _date;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes an accept command for <paramref name="contractId"/> as of <paramref name="date"/>.
        /// </summary>
        public AcceptContractCommand(int contractId, GameDate date, ContentCatalog catalog)
        {
            _contractId = contractId;
            _date = date;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// Gets the contract id to accept.
        /// </summary>
        public int ContractId
        {
            get
            {
                return _contractId;
            }
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            Contract? contract = state.FindContract(_contractId);
            if (contract == null)
            {
                return CommandValidationResult.Rejected("Contract does not exist.");
            }

            if (contract.Status != ContractStatus.Proposal)
            {
                return CommandValidationResult.Rejected("Only a proposal can be accepted.");
            }

            if (!Contracts.HasAcceptSlot(state, _catalog))
            {
                return CommandValidationResult.Rejected("The maximum number of accepted contracts is reached.");
            }

            return CommandValidationResult.Valid;
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            Contracts.Accept(state, _catalog, _contractId, _date);
        }
    }
}
