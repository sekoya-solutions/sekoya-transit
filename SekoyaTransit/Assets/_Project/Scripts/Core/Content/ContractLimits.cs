#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// How many contracts one map size holds and how many of them can be accepted at once (Issue #227).
    /// </summary>
    public sealed class ContractLimits
    {
        /// <summary>
        /// Initializes the limits. The accepted maximum cannot exceed the total maximum.
        /// </summary>
        public ContractLimits(int maxContracts, int maxAccepted)
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

            MaxContracts = maxContracts;
            MaxAccepted = maxAccepted;
        }

        /// <summary>
        /// Gets the maximum number of contracts in the list, whatever their status.
        /// </summary>
        public int MaxContracts { get; }

        /// <summary>
        /// Gets the maximum number of accepted contracts at once.
        /// </summary>
        public int MaxAccepted { get; }
    }
}
