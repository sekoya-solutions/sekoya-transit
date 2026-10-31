#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Proposes, accepts, completes, fails, and pays contracts: "bring N units of X to town Y" goals the player
    /// chooses to take on, that pay a one-off bonus and never penalize failure (Issues #173 and #227).
    /// </summary>
    public static class Contracts
    {
        /// <summary>
        /// PCG stream selector for contract rolls ("CONTRACT").
        /// </summary>
        public const ulong RandomSequence = 0x434F4E5452414354UL;

        /// <summary>
        /// Runs one contract step: completes accepted contracts whose delivered units reached the amount, fails
        /// accepted contracts past their deadline, then proposes new contracts: the initial proposals on the first
        /// call of a new game, and at most one more whenever the calendar month changes while the list is below the
        /// map size's maximum. Publishes offered, completed, and failed events when <paramref name="events"/> is
        /// supplied.
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

            CompleteReached(world, date, events);
            FailOverdue(world, date, events);

            if (world.LastContractYear == date.Year && world.LastContractMonth == date.Month)
            {
                return;
            }

            world.SetLastContractMonth(date.Year, date.Month);
            ContractSettings settings = catalog.Contracts;
            ContractLimits limits = settings.GetLimits(world.Map.Size);
            int proposals = world.NextContractId == GeneratedWorld.FirstContractId ? settings.InitialProposals : 1;
            for (int count = 0; count < proposals && world.ContractList.Count < limits.MaxContracts; count++)
            {
                if (!Propose(world, catalog, settings, date, events))
                {
                    return;
                }
            }
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
        /// Returns whether a contract for the given cargo and town is valid for <paramref name="catalog"/> and
        /// <paramref name="world"/> (a known freight cargo and an existing town).
        /// </summary>
        public static bool IsValid(GeneratedWorld world, ContentCatalog catalog, string cargoId, int townIndex)
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

            return catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) &&
                cargo != null &&
                cargo.Class == CargoClass.Cargo &&
                townIndex >= 0 &&
                townIndex < world.Towns.Count;
        }

        /// <summary>
        /// Returns whether another contract can be accepted: the accepted count is below the map size's maximum.
        /// </summary>
        public static bool HasAcceptSlot(GeneratedWorld world, ContentCatalog catalog)
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

            return world.AcceptedContractCount < catalog.Contracts.GetLimits(world.Map.Size).MaxAccepted;
        }

        /// <summary>
        /// Accepts a proposal: its deadline becomes <paramref name="date"/> plus the configured duration. The caller
        /// validates the status and the accepted limit first.
        /// </summary>
        public static void Accept(GeneratedWorld world, ContentCatalog catalog, int contractId, GameDate date)
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

            Contract contract = world.FindContract(contractId)
                ?? throw new InvalidOperationException("Contract does not exist.");
            contract.Accept(date.AddMonths(catalog.Contracts.DurationMonths));
        }

        /// <summary>
        /// Pays a completed contract: credits the reward, adds the town's population bonus, and removes the contract.
        /// Returns the reward paid. The caller validates the status first.
        /// </summary>
        public static long ClaimReward(GeneratedWorld world, ContentCatalog catalog, int contractId)
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

            Contract contract = world.FindContract(contractId)
                ?? throw new InvalidOperationException("Contract does not exist.");
            ContractSettings settings = catalog.Contracts;
            world.RemoveContract(contractId);
            long reward = 0L;
            if (catalog.TryGetCargo(contract.CargoId, out CargoDefinition? cargo) && cargo != null)
            {
                reward = ComputeReward(settings, cargo, contract.Amount);
            }

            if (reward > 0L)
            {
                world.CreditCompanyCash(reward, FinanceCategory.ContractBonus);
            }

            ApplyTownBonus(world, catalog, contract.TownIndex);
            return reward;
        }

        private static void CompleteReached(GeneratedWorld world, GameDate date, EventBus? events)
        {
            IReadOnlyList<Contract> list = world.ContractList;
            for (int index = 0; index < list.Count; index++)
            {
                Contract contract = list[index];
                if (contract.Status != ContractStatus.Accepted || contract.Delivered < contract.Amount)
                {
                    continue;
                }

                contract.Complete(date);
                world.Statistics.AddContractCompleted();
                if (events != null)
                {
                    events.Publish(
                        new ContractCompletedEvent(
                            contract.Id,
                            contract.CargoId,
                            contract.TownIndex,
                            contract.Amount,
                            date));
                }
            }
        }

        /// <summary>
        /// Returns the inhabitants a town gains when the reward of a contract targeting it is claimed: the configured
        /// bonus, capped so the town stays within the maximum population (0 for an unknown town).
        /// </summary>
        public static int ComputePopulationBonus(GeneratedWorld world, ContentCatalog catalog, int townIndex)
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

            int bonus = catalog.Contracts.TownPopulationBonus;
            if (bonus <= 0 || townIndex < 0 || townIndex >= world.Towns.Count)
            {
                return 0;
            }

            long grown = (long)world.Towns[townIndex].Population + bonus;
            int newPopulation = (int)Math.Min(grown, catalog.TownNeeds.MaxPopulation);
            return Math.Max(0, newPopulation - world.Towns[townIndex].Population);
        }

        private static void ApplyTownBonus(GeneratedWorld world, ContentCatalog catalog, int townIndex)
        {
            int gain = ComputePopulationBonus(world, catalog, townIndex);
            if (gain <= 0)
            {
                return;
            }

            world.SetTownPopulation(townIndex, world.Towns[townIndex].Population + gain, catalog.TownNeeds, catalog);
            VehicleRouteCargoFilter.RefreshAll(world, catalog);
        }

        private static void FailOverdue(GeneratedWorld world, GameDate date, EventBus? events)
        {
            IReadOnlyList<Contract> list = world.ContractList;
            for (int index = 0; index < list.Count; index++)
            {
                Contract contract = list[index];
                if (contract.Status != ContractStatus.Accepted || date <= contract.Deadline)
                {
                    continue;
                }

                contract.Fail(date);
                if (events != null)
                {
                    events.Publish(
                        new ContractFailedEvent(
                            contract.Id,
                            contract.CargoId,
                            contract.TownIndex,
                            contract.Amount,
                            contract.Delivered,
                            date));
                }
            }
        }

        // Adds one proposal for a random (cargo, town) need that has no contract yet; false when none is left.
        private static bool Propose(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractSettings settings,
            GameDate date,
            EventBus? events)
        {
            List<Candidate> candidates = FindCandidates(world, catalog);
            if (candidates.Count == 0)
            {
                return false;
            }

            Pcg32Random random = world.ContractRandom;
            Candidate pick = candidates[random.NextInt32(0, candidates.Count)];
            int amount = random.NextInt32(settings.MinAmount, settings.MaxAmount + 1);
            Contract contract = world.OfferContract(pick.CargoId, pick.TownIndex, amount, date);
            if (events != null)
            {
                events.Publish(
                    new ContractOfferedEvent(contract.Id, contract.CargoId, contract.TownIndex, contract.Amount));
            }

            return true;
        }

        // Eligible (cargo, town) pairs without a contract: the towns' needs, in town order. Passengers and mail are
        // never offered.
        private static List<Candidate> FindCandidates(GeneratedWorld world, ContentCatalog catalog)
        {
            var candidates = new List<Candidate>();
            for (int townIndex = 0; townIndex < world.Towns.Count; townIndex++)
            {
                IReadOnlyList<TownNeed> needs = world.Towns[townIndex].Needs;
                for (int needIndex = 0; needIndex < needs.Count; needIndex++)
                {
                    string cargoId = needs[needIndex].CargoId;
                    if (catalog.TryGetCargo(cargoId, out CargoDefinition? cargo) &&
                        cargo != null &&
                        cargo.Class == CargoClass.Cargo &&
                        !world.HasContract(cargoId, townIndex))
                    {
                        candidates.Add(new Candidate(cargoId, townIndex));
                    }
                }
            }

            return candidates;
        }

        private readonly struct Candidate
        {
            public Candidate(string cargoId, int townIndex)
            {
                CargoId = cargoId;
                TownIndex = townIndex;
            }

            public string CargoId { get; }

            public int TownIndex { get; }
        }
    }
}
