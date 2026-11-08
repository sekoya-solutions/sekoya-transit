#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// How many contracts one map size holds, how many of them can be accepted at once (Issue #227), and how many new
    /// ones are proposed each month (Issue #340).
    /// </summary>
    public sealed class ContractLimits
    {
        /// <summary>
        /// Initializes the limits. The accepted maximum cannot exceed the total maximum, and at least one contract is
        /// proposed per month.
        /// </summary>
        public ContractLimits(int maxContracts, int maxAccepted, int proposalsPerMonth)
        {
            if (maxContracts < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxContracts),
                    maxContracts,
                    "Maximum contracts cannot be negative.");
            }

            if (maxAccepted < 0 || maxAccepted > maxContracts)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAccepted),
                    maxAccepted,
                    "Maximum accepted contracts must be between 0 and the maximum contracts.");
            }

            if (proposalsPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(proposalsPerMonth),
                    proposalsPerMonth,
                    "Proposals per month must be at least 1.");
            }

            MaxContracts = maxContracts;
            MaxAccepted = maxAccepted;
            ProposalsPerMonth = proposalsPerMonth;
        }

        /// <summary>
        /// Gets the maximum number of contracts in the list, whatever their status.
        /// </summary>
        public int MaxContracts { get; }

        /// <summary>
        /// Gets the maximum number of accepted contracts at once.
        /// </summary>
        public int MaxAccepted { get; }

        /// <summary>
        /// Gets how many new contracts are proposed at each calendar month change, while the list has room and open
        /// needs remain.
        /// </summary>
        public int ProposalsPerMonth { get; }
    }
}
