#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Playtest cheat (Issue #316): adds money to the company, recorded as <see cref="FinanceCategory.Cheats"/> income so
    /// the finance ledger still explains every cash change. It does not count as delivery or contract income.
    /// </summary>
    public sealed class CheatAddCashCommand : ISimulationCommand<GeneratedWorld>
    {
        /// <summary>
        /// The amount the cash cheat key adds.
        /// </summary>
        public const long DefaultAmount = 10_000L;

        /// <summary>
        /// Initializes a cheat that adds <paramref name="amount"/> to company cash.
        /// </summary>
        public CheatAddCashCommand(long amount = DefaultAmount)
        {
            if (amount <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cheat cash amount must be positive.");
            }

            Amount = amount;
        }

        /// <summary>
        /// Gets the amount added to company cash.
        /// </summary>
        public long Amount { get; }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (state.CompanyCash > long.MaxValue - Amount)
            {
                return CommandValidationResult.Rejected("Company cash would overflow.");
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

            state.CreditCompanyCash(Amount, FinanceCategory.Cheats);
        }
    }
}
