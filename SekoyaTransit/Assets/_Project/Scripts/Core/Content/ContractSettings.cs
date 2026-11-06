#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven contract generation, limits, deadline, and reward knobs (Issues #173, #227, #278 and #312).
    /// </summary>
    public sealed class ContractSettings
    {
        private readonly ContractLimits _small;
        private readonly ContractLimits _medium;
        private readonly ContractLimits _large;
        private static readonly int[] _placeholderDurations = { 2, 4, 6 };

        private readonly int[] _durationMonthsByLevel;

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
            IReadOnlyList<int> durationMonthsByLevel,
            int initialDurationMonths,
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

#pragma warning disable CA1510
            if (durationMonthsByLevel == null)
            {
                throw new ArgumentNullException(nameof(durationMonthsByLevel));
            }
#pragma warning restore CA1510

            if (durationMonthsByLevel.Count != TownNeedPlan.MaxLevel)
            {
                throw new ArgumentException(
                    "Contract durations must list one value per manufacturing level (1-3).",
                    nameof(durationMonthsByLevel));
            }

            for (int level = 0; level < durationMonthsByLevel.Count; level++)
            {
                if (durationMonthsByLevel[level] < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(durationMonthsByLevel),
                        durationMonthsByLevel[level],
                        "Contract duration must be at least 1 month.");
                }
            }

            if (initialDurationMonths < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialDurationMonths),
                    initialDurationMonths,
                    "Initial contract duration must be at least 1 month.");
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
            _durationMonthsByLevel = new[]
            {
                durationMonthsByLevel[0], durationMonthsByLevel[1], durationMonthsByLevel[2]
            };
            InitialDurationMonths = initialDurationMonths;
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
                _placeholderDurations,
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
        /// Gets how many calendar months the contracts a new game accepts automatically have to be delivered,
        /// counted from the start date (Issue #312).
        /// </summary>
        public int InitialDurationMonths { get; }

        /// <summary>
        /// Gets the multiplier applied to amount times cargo price for the one-off completion bonus.
        /// </summary>
        public decimal RewardMultiplier { get; }

        /// <summary>
        /// Gets the inhabitants added to a town when the reward of a contract targeting it is claimed.
        /// </summary>
        public int TownPopulationBonus { get; }

        /// <summary>
        /// Returns how many calendar months an accepted contract has to be delivered for a cargo of the given
        /// manufacturing level (clamped to levels 1-3).
        /// </summary>
        public int GetDurationMonths(int manufacturingLevel)
        {
            int level = Math.Max(1, Math.Min(TownNeedPlan.MaxLevel, manufacturingLevel));
            return _durationMonthsByLevel[level - 1];
        }

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
