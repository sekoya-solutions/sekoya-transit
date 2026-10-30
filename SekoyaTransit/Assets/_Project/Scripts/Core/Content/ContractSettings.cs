#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven contract generation, limits, and reward knobs (Issues #173 and #227).
    /// </summary>
    public sealed class ContractSettings
    {
        private readonly ContractLimits _small;
        private readonly ContractLimits _medium;
        private readonly ContractLimits _large;

        /// <summary>
        /// Initializes contract settings.
        /// </summary>
        public ContractSettings(
            ContractLimits small,
            ContractLimits medium,
            ContractLimits large,
            int initialProposals,
            int minAmount,
            int maxAmount,
            int durationMonths,
            decimal rewardMultiplier,
            int townPopulationBonus)
        {
#pragma warning disable CA1510
            if (small == null)
            {
                throw new ArgumentNullException(nameof(small));
            }

            if (medium == null)
            {
                throw new ArgumentNullException(nameof(medium));
            }

            if (large == null)
            {
                throw new ArgumentNullException(nameof(large));
            }
#pragma warning restore CA1510

            if (initialProposals < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialProposals),
                    initialProposals,
                    "Initial proposals cannot be negative.");
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

            _small = small;
            _medium = medium;
            _large = large;
            InitialProposals = initialProposals;
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
            return new ContractSettings(
                new ContractLimits(6, 3),
                new ContractLimits(8, 4),
                new ContractLimits(10, 5),
                3,
                50,
                200,
                12,
                1.0m,
                100);
        }

        /// <summary>
        /// Gets the number of proposals a new game starts with (capped by the map size's maximum).
        /// </summary>
        public int InitialProposals { get; }

        /// <summary>
        /// Gets the inclusive minimum units a contract asks for.
        /// </summary>
        public int MinAmount { get; }

        /// <summary>
        /// Gets the inclusive maximum units a contract asks for.
        /// </summary>
        public int MaxAmount { get; }

        /// <summary>
        /// Gets how many calendar months an accepted contract has to be delivered.
        /// </summary>
        public int DurationMonths { get; }

        /// <summary>
        /// Gets the multiplier applied to amount times cargo price for the one-off completion bonus.
        /// </summary>
        public decimal RewardMultiplier { get; }

        /// <summary>
        /// Gets the inhabitants added to a town when the reward of a contract targeting it is claimed.
        /// </summary>
        public int TownPopulationBonus { get; }

        /// <summary>
        /// Returns the contract limits of a map size.
        /// </summary>
        public ContractLimits GetLimits(MapSize mapSize)
        {
            switch (mapSize)
            {
                case MapSize.Small:
                    return _small;
                case MapSize.Medium:
                    return _medium;
                case MapSize.Large:
                    return _large;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Unsupported map size.");
            }
        }
    }
}
