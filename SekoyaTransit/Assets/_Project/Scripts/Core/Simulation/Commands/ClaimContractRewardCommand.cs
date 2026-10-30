#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Pays the reward of a completed contract and removes it (Issue #227): the bonus is credited to the company and
    /// the target town grows by the configured population bonus.
    /// </summary>
    public sealed class ClaimContractRewardCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _contractId;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes a claim command for <paramref name="contractId"/>.
        /// </summary>
        public ClaimContractRewardCommand(int contractId, ContentCatalog catalog)
        {
            _contractId = contractId;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// Gets the contract id to claim.
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

            if (contract.Status != ContractStatus.Completed)
            {
                return CommandValidationResult.Rejected("Only a completed contract has a reward to claim.");
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

            Contracts.ClaimReward(state, _catalog, _contractId);
        }
    }
}
