#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Removes a contract from the list without a reward (Issue #227): declines a proposal, cancels an accepted
    /// contract, or removes a failed one. The command names the status it expects, so a stale click on a contract
    /// that changed meanwhile (for example an accepted contract that just completed) is rejected.
    /// </summary>
    public sealed class DismissContractCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _contractId;
        private readonly ContractStatus _expectedStatus;

        /// <summary>
        /// Initializes a dismiss command. <paramref name="expectedStatus"/> must be proposal (decline), accepted
        /// (cancel), or failed (remove).
        /// </summary>
        public DismissContractCommand(int contractId, ContractStatus expectedStatus)
        {
            if (expectedStatus == ContractStatus.Completed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedStatus),
                    expectedStatus,
                    "A completed contract is claimed, not dismissed.");
            }

            _contractId = contractId;
            _expectedStatus = expectedStatus;
        }

        /// <summary>
        /// Gets the contract id to remove.
        /// </summary>
        public int ContractId
        {
            get
            {
                return _contractId;
            }
        }

        /// <summary>
        /// Gets the status the contract must have.
        /// </summary>
        public ContractStatus ExpectedStatus
        {
            get
            {
                return _expectedStatus;
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

            if (contract.Status != _expectedStatus)
            {
                return CommandValidationResult.Rejected("Contract is no longer " + _expectedStatus + ".");
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

            state.RemoveContract(_contractId);
        }
    }
}
