#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Data-driven town population range, growth tiers, and supply/demand growth knobs.
    /// </summary>
    public sealed class TownNeedsSettings
    {
        /// <summary>
        /// Default number of towns that must plan each freight cargo as a need (Issue #193).
        /// </summary>
        public const int DefaultMinTownsPerNeededCargo = 3;

        private readonly ReadOnlyCollection<TownNeedTier> _tiers;
        private readonly ReadOnlyCollection<string> _names;

        /// <summary>
        /// Initializes town needs settings.
        /// </summary>
        public TownNeedsSettings(
            int minStartingPopulation,
            int maxStartingPopulation,
            int maxPopulation,
            IReadOnlyList<TownNeedTier> tiers,
            int demandPerNeedPerMonth = 10,
            int populationPerSatisfiedNeed = 20,
            int passengersPerPopulationUnit = 100,
            int mailPerPopulationUnit = 250,
            int passengerWaitingCapacity = 50,
            int minTownsPerNeededCargo = DefaultMinTownsPerNeededCargo,
            IReadOnlyList<string>? names = null)
        {
#pragma warning disable CA1510
            if (tiers is null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }
#pragma warning restore CA1510

            if (minStartingPopulation < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minStartingPopulation),
                    minStartingPopulation,
                    "Minimum starting population must be positive.");
            }

            if (maxStartingPopulation < minStartingPopulation)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxStartingPopulation),
                    maxStartingPopulation,
                    "Maximum starting population cannot be below the minimum.");
            }

            if (maxPopulation < maxStartingPopulation)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPopulation),
                    maxPopulation,
                    "Maximum population cannot be below the starting population range.");
            }

            if (demandPerNeedPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(demandPerNeedPerMonth),
                    demandPerNeedPerMonth,
                    "Demand per need per month must be positive.");
            }

            if (populationPerSatisfiedNeed < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(populationPerSatisfiedNeed),
                    populationPerSatisfiedNeed,
                    "Population per satisfied need must be positive.");
            }

            if (passengersPerPopulationUnit < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(passengersPerPopulationUnit),
                    passengersPerPopulationUnit,
                    "Passengers per population unit must be positive.");
            }

            if (mailPerPopulationUnit < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mailPerPopulationUnit),
                    mailPerPopulationUnit,
                    "Mail per population unit must be positive.");
            }

            if (passengerWaitingCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(passengerWaitingCapacity),
                    passengerWaitingCapacity,
                    "Passenger waiting capacity must be positive.");
            }

            if (minTownsPerNeededCargo < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minTownsPerNeededCargo),
                    minTownsPerNeededCargo,
                    "Minimum towns per needed cargo must be positive.");
            }

            if (tiers.Count == 0)
            {
                throw new ArgumentException("At least one growth tier is required.", nameof(tiers));
            }

            var copy = new TownNeedTier[tiers.Count];
            int previousMin = -1;
            for (int index = 0; index < tiers.Count; index++)
            {
                TownNeedTier tier = tiers[index];
                if (tier.MinPopulation <= previousMin)
                {
                    throw new ArgumentException(
                        "Growth tiers must be ordered by strictly increasing minPopulation.",
                        nameof(tiers));
                }

                previousMin = tier.MinPopulation;
                copy[index] = tier;
            }

            if (copy[0].MinPopulation != 0)
            {
                throw new ArgumentException("The first growth tier must start at population 0.", nameof(tiers));
            }

            MinStartingPopulation = minStartingPopulation;
            MaxStartingPopulation = maxStartingPopulation;
            MaxPopulation = maxPopulation;
            DemandPerNeedPerMonth = demandPerNeedPerMonth;
            PopulationPerSatisfiedNeed = populationPerSatisfiedNeed;
            PassengersPerPopulationUnit = passengersPerPopulationUnit;
            MailPerPopulationUnit = mailPerPopulationUnit;
            PassengerWaitingCapacity = passengerWaitingCapacity;
            MinTownsPerNeededCargo = minTownsPerNeededCargo;
            _tiers = Array.AsReadOnly(copy);
            _names = Array.AsReadOnly(CopyNames(names ?? Array.Empty<string>()));
        }

        /// <summary>
        /// Creates the gameplay-systems.md placeholder schedule used when content is unavailable.
        /// </summary>
        public static TownNeedsSettings CreatePlaceholder()
        {
            return new TownNeedsSettings(
                100,
                500,
                10000,
                new[]
                {
                    new TownNeedTier(0, 3, 0, 0),
                    new TownNeedTier(1000, 4, 0, 0),
                    new TownNeedTier(2000, 4, 1, 0),
                    new TownNeedTier(3000, 5, 1, 0),
                    new TownNeedTier(4000, 5, 2, 0),
                    new TownNeedTier(5000, 5, 2, 0),
                    new TownNeedTier(6000, 5, 2, 0),
                    new TownNeedTier(7000, 5, 2, 0),
                    new TownNeedTier(8000, 5, 3, 0),
                    new TownNeedTier(9000, 5, 3, 0),
                    new TownNeedTier(10000, 5, 3, 1),
                },
                demandPerNeedPerMonth: 10,
                populationPerSatisfiedNeed: 20,
                passengersPerPopulationUnit: 100,
                mailPerPopulationUnit: 250,
                passengerWaitingCapacity: 50);
        }

        /// <summary>
        /// Gets the inclusive minimum starting population.
        /// </summary>
        public int MinStartingPopulation { get; }

        /// <summary>
        /// Gets the inclusive maximum starting population.
        /// </summary>
        public int MaxStartingPopulation { get; }

        /// <summary>
        /// Gets the maximum supported town population.
        /// </summary>
        public int MaxPopulation { get; }

        /// <summary>
        /// Gets flat monthly demand units required to fully meet each need.
        /// </summary>
        public int DemandPerNeedPerMonth { get; }

        /// <summary>
        /// Gets inhabitants added at month end per fully met need.
        /// </summary>
        public int PopulationPerSatisfiedNeed { get; }

        /// <summary>
        /// Gets the population divisor for passengers generated each production pulse.
        /// </summary>
        public int PassengersPerPopulationUnit { get; }

        /// <summary>
        /// Gets the population divisor for mail generated each production pulse.
        /// </summary>
        public int MailPerPopulationUnit { get; }

        /// <summary>
        /// Gets the max passenger-class units that may wait at a town.
        /// </summary>
        public int PassengerWaitingCapacity { get; }

        /// <summary>
        /// Gets growth tiers ordered by ascending <see cref="TownNeedTier.MinPopulation"/>.
        /// </summary>
        public IReadOnlyList<TownNeedTier> Tiers
        {
            get
            {
                return _tiers;
            }
        }

        /// <summary>
        /// Returns the tier that applies to the given population (highest minPopulation ≤ population).
        /// </summary>
        /// <summary>
        /// Gets how many towns must plan each level 1–3 freight cargo as a need; map generation adds towns until the
        /// rule can hold (Issue #193).
        /// </summary>
        public int MinTownsPerNeededCargo { get; }

        /// <summary>
        /// Gets the unique town names (real-world capitals) map generation picks from (Issue #193). Empty for the
        /// placeholder, whose towns stay unnamed.
        /// </summary>
        public IReadOnlyList<string> Names
        {
            get
            {
                return _names;
            }
        }

        public TownNeedTier GetTierForPopulation(int population)
        {
            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Population cannot be negative.");
            }

            TownNeedTier selected = _tiers[0];
            for (int index = 1; index < _tiers.Count; index++)
            {
                if (_tiers[index].MinPopulation > population)
                {
                    break;
                }

                selected = _tiers[index];
            }

            return selected;
        }

        private static string[] CopyNames(IReadOnlyList<string> names)
        {
            var copy = new string[names.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Length; index++)
            {
                string name = names[index];
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"Town name {index} is empty.", nameof(names));
                }

                if (!seen.Add(name))
                {
                    throw new ArgumentException($"Duplicate town name '{name}'.", nameof(names));
                }

                copy[index] = name;
            }

            return copy;
        }
    }
}
