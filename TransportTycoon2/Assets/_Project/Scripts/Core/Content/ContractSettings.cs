#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Data-driven contract generation and reward knobs (Issue #173).
    /// </summary>
    public sealed class ContractSettings
    {
        /// <summary>
        /// Initializes contract settings.
        /// </summary>
        public ContractSettings(
            int maxOpenContracts,
            int minAmount,
            int maxAmount,
            int durationMonths,
            decimal rewardMultiplier,
            int townPopulationBonus)
        {
            if (maxOpenContracts < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxOpenContracts),
                    maxOpenContracts,
                    "Maximum open contracts cannot be negative.");
            }

            if (minAmount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minAmount),
                    minAmount,
                    "Minimum contract amount must be at least 1.");
            }

            if (maxAmount < minAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxAmount),
                    maxAmount,
                    "Maximum contract amount cannot be below the minimum.");
            }

            if (durationMonths < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationMonths),
                    durationMonths,
                    "Contract duration must be at least 1 month.");
            }

            if (rewardMultiplier < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rewardMultiplier),
                    rewardMultiplier,
                    "Reward multiplier cannot be negative.");
            }

            if (townPopulationBonus < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(townPopulationBonus),
                    townPopulationBonus,
                    "Town population bonus cannot be negative.");
            }

            MaxOpenContracts = maxOpenContracts;
            MinAmount = minAmount;
            MaxAmount = maxAmount;
            DurationMonths = durationMonths;
            RewardMultiplier = rewardMultiplier;
            TownPopulationBonus = townPopulationBonus;
        }

        /// <summary>
        /// Creates the placeholder values used when content is unavailable.
        /// </summary>
        public static ContractSettings CreatePlaceholder()
        {
            return new ContractSettings(3, 50, 200, 12, 1.0m, 100);
        }

        /// <summary>
        /// Gets the maximum number of simultaneously open contracts.
        /// </summary>
        public int MaxOpenContracts { get; }

        /// <summary>
        /// Gets the inclusive minimum units a contract asks for.
        /// </summary>
        public int MinAmount { get; }

        /// <summary>
        /// Gets the inclusive maximum units a contract asks for.
        /// </summary>
        public int MaxAmount { get; }

        /// <summary>
        /// Gets how many calendar months a contract stays open.
        /// </summary>
        public int DurationMonths { get; }

        /// <summary>
        /// Gets the multiplier applied to amount times cargo price for the one-off completion bonus.
        /// </summary>
        public decimal RewardMultiplier { get; }

        /// <summary>
        /// Gets the inhabitants added to a town when a contract targeting it completes.
        /// </summary>
        public int TownPopulationBonus { get; }
    }
}
