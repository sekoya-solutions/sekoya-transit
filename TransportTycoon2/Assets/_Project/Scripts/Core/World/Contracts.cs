#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Generates, completes, and expires contracts: time-limited "bring N units of X to Y" goals that pay a
    /// one-off bonus and never penalize failure (Issue #173).
    /// </summary>
    public static class Contracts
    {
        /// <summary>
        /// PCG stream selector for contract rolls ("CONTRACT").
        /// </summary>
        public const ulong RandomSequence = 0x434F4E5452414354UL;

        /// <summary>
        /// Runs one contract step: completes contracts whose delivered units reached the amount, expires
        /// unfinished contracts past their deadline, then tops up open contracts on the first call of a game
        /// (or after a load) and whenever the calendar month changes. Publishes offered, completed, and expired
        /// events when <paramref name="events"/> is supplied.
        /// </summary>
        public static void AdvanceAll(
            GeneratedWorld world,
            ContentCatalog catalog,
            GameDate date,
            EventBus? events = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            ContractSettings settings = catalog.Contracts;
            CompleteReached(world, catalog, settings, date, events);
            ExpireOverdue(world, date, events);

            if (world.LastContractYear == date.Year && world.LastContractMonth == date.Month)
            {
                return;
            }

            world.SetLastContractMonth(date.Year, date.Month);
            TopUp(world, catalog, settings, date, events);
        }

        /// <summary>
        /// Returns the one-off completion bonus: amount times the cargo price times the reward multiplier,
        /// rounded down to whole money.
        /// </summary>
        public static long ComputeReward(ContractSettings settings, CargoDefinition cargo, int amount)
        {
#pragma warning disable CA1510
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (cargo == null)
            {
                throw new ArgumentNullException(nameof(cargo));
            }
#pragma warning restore CA1510

            decimal reward = (decimal)amount * cargo.PricePerUnit * settings.RewardMultiplier;
            return (long)Math.Floor(reward);
        }

        /// <summary>
        /// Returns whether a contract for the given cargo and target is still valid for <paramref name="catalog"/>
        /// and <paramref name="world"/> (known freight cargo and an existing target; an industry must still
        /// list the cargo as an input).
        /// </summary>
        public static bool IsValid(
            GeneratedWorld world,
            ContentCatalog catalog,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            if (!catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) ||
                cargo == null ||
                cargo.Class != CargoClass.Cargo)
            {
                return false;
            }

            switch (targetKind)
            {
                case DeliveryDestinationKind.Town:
                    return targetIndex >= 0 && targetIndex < world.Towns.Count;
                case DeliveryDestinationKind.Industry:
                    if (targetIndex < 0 || targetIndex >= world.Industries.Count ||
                        !catalog.TryGetIndustry(world.Industries[targetIndex].IndustryId, out IndustryDefinition? industry) ||
                        industry == null)
                    {
                        return false;
                    }

                    for (int index = 0; index < industry.Inputs.Count; index++)
                    {
                        if (string.Equals(industry.Inputs[index].CargoId, cargoId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static void CompleteReached(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractSettings settings,
            GameDate date,
            EventBus? events)
        {
            // Copy only when something completes, so ticks without a completion allocate nothing (Issue #70).
            if (!AnyReached(world.OpenContracts))
            {
                return;
            }

            // Copy the list: completion removes entries.
            Contract[] open = ToArray(world.OpenContracts);
            for (int index = 0; index < open.Length; index++)
            {
                Contract contract = open[index];
                if (contract.Delivered < contract.Amount)
                {
                    continue;
                }

                world.RemoveContract(contract.Id);
                long reward = 0L;
                if (catalog.TryGetCargo(contract.CargoId, out CargoDefinition? cargo) && cargo != null)
                {
                    reward = ComputeReward(settings, cargo, contract.Amount);
                }

                if (reward > 0L)
                {
                    world.CreditCompanyCash(reward, FinanceCategory.ContractBonus);
                }

                int populationBonus = contract.TargetKind == DeliveryDestinationKind.Town
                    ? ApplyTownBonus(world, catalog, settings, contract.TargetIndex)
                    : 0;
                if (events != null)
                {
                    events.Publish(
                        new ContractCompletedEvent(
                            contract.Id,
                            contract.CargoId,
                            contract.TargetKind,
                            contract.TargetIndex,
                            contract.Amount,
                            reward,
                            populationBonus,
                            date));
                }
            }
        }

        private static int ApplyTownBonus(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractSettings settings,
            int townIndex)
        {
            if (settings.TownPopulationBonus <= 0 || townIndex < 0 || townIndex >= world.Towns.Count)
            {
                return 0;
            }

            TownNeedsSettings needs = catalog.TownNeeds;
            Town town = world.Towns[townIndex];
            long grown = (long)town.Population + settings.TownPopulationBonus;
            int newPopulation = (int)Math.Min(grown, needs.MaxPopulation);
            if (newPopulation <= town.Population)
            {
                return 0;
            }

            int applied = newPopulation - town.Population;
            world.SetTownPopulation(townIndex, newPopulation, needs, catalog);
            VehicleRouteCargoFilter.RefreshAll(world, catalog);
            return applied;
        }

        private static void ExpireOverdue(GeneratedWorld world, GameDate date, EventBus? events)
        {
            // Copy only when something expires, so ordinary ticks allocate nothing (Issue #70).
            if (!AnyOverdue(world.OpenContracts, date))
            {
                return;
            }

            Contract[] open = ToArray(world.OpenContracts);
            for (int index = 0; index < open.Length; index++)
            {
                Contract contract = open[index];
                if (date <= contract.Deadline)
                {
                    continue;
                }

                world.RemoveContract(contract.Id);
                if (events != null)
                {
                    events.Publish(
                        new ContractExpiredEvent(
                            contract.Id,
                            contract.CargoId,
                            contract.TargetKind,
                            contract.TargetIndex,
                            contract.Amount,
                            contract.Delivered,
                            date));
                }
            }
        }

        private static void TopUp(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractSettings settings,
            GameDate date,
            EventBus? events)
        {
            while (world.OpenContracts.Count < settings.MaxOpenContracts)
            {
                List<Candidate> candidates = FindCandidates(world, catalog);
                if (candidates.Count == 0)
                {
                    return;
                }

                Pcg32Random random = world.ContractRandom;
                Candidate pick = candidates[random.NextInt32(0, candidates.Count)];
                int amount = random.NextInt32(settings.MinAmount, settings.MaxAmount + 1);
                Contract contract = world.OfferContract(
                    pick.CargoId,
                    pick.TargetKind,
                    pick.TargetIndex,
                    amount,
                    date,
                    date.AddMonths(settings.DurationMonths));
                if (events != null)
                {
                    events.Publish(
                        new ContractOfferedEvent(
                            contract.Id,
                            contract.CargoId,
                            contract.TargetKind,
                            contract.TargetIndex,
                            contract.Amount,
                            contract.Deadline));
                }
            }
        }

        // Eligible (cargo, target) pairs without an open contract: town needs first (town order), then
        // industry inputs (industry order). Passengers and mail are never offered.
        private static List<Candidate> FindCandidates(GeneratedWorld world, ContentCatalog catalog)
        {
            var candidates = new List<Candidate>();
            for (int townIndex = 0; townIndex < world.Towns.Count; townIndex++)
            {
                IReadOnlyList<TownNeed> needs = world.Towns[townIndex].Needs;
                for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                {
                    AddCandidate(
                        world,
                        catalog,
                        candidates,
                        needs[needIndex].CargoId,
                        DeliveryDestinationKind.Town,
                        townIndex);
                }
            }

            for (int industryIndex = 0; industryIndex < world.Industries.Count; industryIndex++)
            {
                if (!catalog.TryGetIndustry(world.Industries[industryIndex].IndustryId, out IndustryDefinition? industry) ||
                    industry == null)
                {
                    continue;
                }

                for (int inputIndex = 0; inputIndex < industry.Inputs.Count; inputIndex++)
                {
                    AddCandidate(
                        world,
                        catalog,
                        candidates,
                        industry.Inputs[inputIndex].CargoId,
                        DeliveryDestinationKind.Industry,
                        industryIndex);
                }
            }

            return candidates;
        }

        private static void AddCandidate(
            GeneratedWorld world,
            ContentCatalog catalog,
            List<Candidate> candidates,
            string cargoId,
            DeliveryDestinationKind targetKind,
            int targetIndex)
        {
            if (!catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) ||
                cargo == null ||
                cargo.Class != CargoClass.Cargo ||
                world.HasOpenContract(cargoId, targetKind, targetIndex))
            {
                return;
            }

            candidates.Add(new Candidate(cargoId, targetKind, targetIndex));
        }

        private static bool AnyReached(IReadOnlyList<Contract> contracts)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                if (contracts[index].Delivered >= contracts[index].Amount)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AnyOverdue(IReadOnlyList<Contract> contracts, GameDate date)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                if (date > contracts[index].Deadline)
                {
                    return true;
                }
            }

            return false;
        }

        private static Contract[] ToArray(IReadOnlyList<Contract> contracts)
        {
            var copy = new Contract[contracts.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = contracts[index];
            }

            return copy;
        }

        private readonly struct Candidate
        {
            public Candidate(string cargoId, DeliveryDestinationKind targetKind, int targetIndex)
            {
                CargoId = cargoId;
                TargetKind = targetKind;
                TargetIndex = targetIndex;
            }

            public string CargoId { get; }

            public DeliveryDestinationKind TargetKind { get; }

            public int TargetIndex { get; }
        }
    }
}
