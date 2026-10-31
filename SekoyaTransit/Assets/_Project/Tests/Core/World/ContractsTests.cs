#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.Tests.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class ContractsTests
    {
        private const ulong _seed = 73001UL;
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly GameDate _january = new GameDate(1900, 1, 15);
        private static readonly GameDate _lateJanuary = new GameDate(1900, 1, 28);
        private static readonly GameDate _february = new GameDate(1900, 2, 1);

        [Test]
        public void AdvanceAll_FirstCall_ProposesTheInitialProposals()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 3));

            Contracts.AdvanceAll(world, catalog, _january);

            Assert.That(world.ContractList, Has.Count.EqualTo(3));
            for (int index = 0; index < world.ContractList.Count; index++)
            {
                Assert.That(world.ContractList[index].Status, Is.EqualTo(ContractStatus.Proposal));
            }
        }

        [Test]
        public void AdvanceAll_InitialProposals_AreCappedByTheMaximumForTheMapSize()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 2, maxAccepted: 1, initialProposals: 3));

            Contracts.AdvanceAll(world, catalog, _january);

            Assert.That(world.ContractList, Has.Count.EqualTo(2));
        }

        [Test]
        public void AdvanceAll_SameMonth_ProposesNothingAndANewMonthProposesAtMostOne()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 1));
            Contracts.AdvanceAll(world, catalog, _january);
            Assert.That(world.ContractList, Has.Count.EqualTo(1));

            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            Assert.That(world.ContractList, Has.Count.EqualTo(1));

            Contracts.AdvanceAll(world, catalog, _february);
            Assert.That(world.ContractList, Has.Count.EqualTo(2));

            // A gap of several months still adds one contract only.
            Contracts.AdvanceAll(world, catalog, new GameDate(1900, 6, 1));
            Assert.That(world.ContractList, Has.Count.EqualTo(3));
        }

        [Test]
        public void AdvanceAll_ListAtTheMaximum_ProposesNothing()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 2, maxAccepted: 1, initialProposals: 2));
            Contracts.AdvanceAll(world, catalog, _january);

            Contracts.AdvanceAll(world, catalog, _february);

            Assert.That(world.ContractList, Has.Count.EqualTo(2));
        }

        [Test]
        public void AdvanceAll_ZeroMaximum_DisablesContracts()
        {
            GeneratedWorld world = CreateWorld(3);

            Contracts.AdvanceAll(world, CreateCatalog(MakeSettings(maxContracts: 0, maxAccepted: 0)), _january);

            Assert.That(world.ContractList, Is.Empty);
            Assert.That(world.NextContractId, Is.EqualTo(GeneratedWorld.FirstContractId));
        }

        [Test]
        public void AdvanceAll_FewerNeedsThanSlots_ProposesFewerWithoutError()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 10, maxAccepted: 5, initialProposals: 10));

            Assert.DoesNotThrow(() => Contracts.AdvanceAll(world, catalog, _january));

            // Only the three towns' needs are eligible; industries are never targets.
            Assert.That(world.ContractList, Has.Count.EqualTo(3));
        }

        [Test]
        public void AdvanceAll_NeverDuplicatesANeedAndNeverPicksPassengers()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 10, maxAccepted: 5, initialProposals: 10));

            Contracts.AdvanceAll(world, catalog, _january);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < world.ContractList.Count; index++)
            {
                Contract contract = world.ContractList[index];
                Assert.That(contract.CargoId, Is.Not.EqualTo("passengers"));
                Assert.That(seen.Add(contract.CargoId + "|" + contract.TownIndex), Is.True);
                Assert.That(Contracts.IsValid(world, catalog, contract.CargoId, contract.TownIndex), Is.True);
            }
        }

        [Test]
        public void AdvanceAll_ExistingContractOfAnyStatusBlocksItsNeed()
        {
            GeneratedWorld world = CreateWorld(2);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 20, maxAccepted: 5, initialProposals: 1));
            int needs = 0;
            for (int town = 0; town < world.Towns.Count; town++)
            {
                needs += world.Towns[town].Needs.Count;
            }

            for (int month = 1; month <= 12; month++)
            {
                Contracts.AdvanceAll(world, catalog, new GameDate(1900, month, 1));
            }

            // Every need got exactly one contract; the following months found nothing left to propose.
            Assert.That(world.ContractList, Has.Count.EqualTo(needs));
        }

        [Test]
        public void AdvanceAll_ProposalsRollTheAmountAndHaveNoDeadlineYet()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(
                MakeSettings(maxContracts: 4, maxAccepted: 2, initialProposals: 4, minAmount: 10, maxAmount: 12));

            Contracts.AdvanceAll(world, catalog, _january);

            for (int index = 0; index < world.ContractList.Count; index++)
            {
                Contract contract = world.ContractList[index];
                Assert.That(contract.Id, Is.EqualTo(index + 1));
                Assert.That(contract.Amount, Is.InRange(10, 12));
                Assert.That(contract.Delivered, Is.Zero);
                Assert.That(contract.StartDate, Is.EqualTo(_january));
                Assert.That(contract.Deadline, Is.EqualTo(_january));
            }

            Assert.That(world.NextContractId, Is.EqualTo(4));
        }

        [Test]
        public void AdvanceAll_IdsNeverRepeatAfterAContractIsRemoved()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 2));
            Contracts.AdvanceAll(world, catalog, _january);
            Process(world, new DismissContractCommand(world.ContractList[0].Id, ContractStatus.Proposal));

            Contracts.AdvanceAll(world, catalog, _february);

            Assert.That(world.ContractList[0].Id, Is.EqualTo(2));
            Assert.That(world.ContractList[1].Id, Is.EqualTo(3));
        }

        [Test]
        public void Accept_StartsTheDeadlineAtTheAcceptDate()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, durationMonths: 14));
            Contract contract = Offer(world, "wood", 0, 100);

            Process(world, new AcceptContractCommand(contract.Id, _february, catalog));

            Assert.That(contract.Status, Is.EqualTo(ContractStatus.Accepted));
            Assert.That(contract.StartDate, Is.EqualTo(_january));
            Assert.That(contract.Deadline, Is.EqualTo(new GameDate(1901, 4, 1)));
        }

        [Test]
        public void Accept_RejectedWhenTheAcceptedLimitIsReachedOrTheStatusIsWrong()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, maxAccepted: 2));
            Contract first = Offer(world, "wood", 0, 100);
            Contract second = Offer(world, "wood", 1, 100);
            Contract third = Offer(world, "wood", 2, 100);
            Process(world, new AcceptContractCommand(first.Id, _january, catalog));
            Process(world, new AcceptContractCommand(second.Id, _january, catalog));

            Assert.That(Contracts.HasAcceptSlot(world, catalog), Is.False);
            CommandValidationResult full = new AcceptContractCommand(third.Id, _january, catalog).Validate(world);
            Assert.That(full.IsValid, Is.False);
            Assert.That(
                new AcceptContractCommand(first.Id, _january, catalog).Validate(world).IsValid,
                Is.False,
                "an accepted contract cannot be accepted again");
            Assert.That(
                new AcceptContractCommand(99, _january, catalog).Validate(world).IsValid,
                Is.False,
                "an unknown contract cannot be accepted");

            // Cancelling one frees a slot.
            Process(world, new DismissContractCommand(first.Id, ContractStatus.Accepted));
            Assert.That(new AcceptContractCommand(third.Id, _january, catalog).Validate(world).IsValid, Is.True);
        }

        [Test]
        public void Accept_LimitFollowsTheMapSize()
        {
            GeneratedWorld world = CreateWorld(3);
            var settings = new ContractSettings(
                new ContractLimits(6, 1),
                new ContractLimits(8, 2),
                new ContractLimits(10, 3),
                3,
                50,
                200,
                12,
                1.0m,
                100);
            ContentCatalog catalog = CreateCatalog(settings);
            Contract first = Offer(world, "wood", 0, 100);
            Contract second = Offer(world, "wood", 1, 100);
            Process(world, new AcceptContractCommand(first.Id, _january, catalog));

            // The test world is a small map: one accepted contract at most.
            Assert.That(world.Map.Size, Is.EqualTo(MapSize.Small));
            Assert.That(new AcceptContractCommand(second.Id, _january, catalog).Validate(world).IsValid, Is.False);
        }

        [Test]
        public void Delivery_OnlyAnAcceptedContractForTheCargoAndTownCounts()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract woodToTown0 = OfferAccepted(world, catalog, "wood", 0, 100);
            Contract planksToTown0 = OfferAccepted(world, catalog, "planks", 0, 100);
            Contract woodToTown1 = OfferAccepted(world, catalog, "wood", 1, 100);
            Contract proposedWoodToTown2 = Offer(world, "wood", 2, 100);

            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 7);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[2]), "wood", 5);

            Assert.That(woodToTown0.Delivered, Is.EqualTo(7L));
            Assert.That(planksToTown0.Delivered, Is.Zero);
            Assert.That(woodToTown1.Delivered, Is.Zero);
            Assert.That(proposedWoodToTown2.Delivered, Is.Zero, "a proposal never counts deliveries");
        }

        [Test]
        public void Delivery_AwayFromAnySiteOrNonPositiveAmountIsIgnored()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 100);

            world.RecordContractDelivery(new TileCoordinate(0, 0), "wood", 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 0);

            Assert.That(contract.Delivered, Is.Zero);
        }

        [Test]
        public void Delivery_IndustryDeliveriesNeverCountForTownContracts()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 100);

            world.RecordContractDelivery(
                EmptyNeighbor(world, world.Industries[SawmillIndex(world)].Coordinate),
                "wood",
                9);

            Assert.That(contract.Delivered, Is.Zero);
        }

        [Test]
        public void Completion_MarksTheContractCompletedWithoutPayingUntilClaimed()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 1, rewardMultiplier: 1.5m));
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 7);
            long cashBefore = world.CompanyCash;
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 7);

            Contracts.AdvanceAll(world, catalog, _lateJanuary);

            Assert.That(contract.Status, Is.EqualTo(ContractStatus.Completed));
            Assert.That(contract.SettledDate, Is.EqualTo(_lateJanuary));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.Statistics.ContractsCompleted, Is.EqualTo(1L));
            Assert.That(world.AcceptedContractCount, Is.Zero, "a completed contract frees its accepted slot");
            Assert.That(world.ContractList, Has.Count.EqualTo(1), "it keeps its list slot until claimed");
        }

        [Test]
        public void Completion_FurtherDeliveriesNoLongerCount()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 5);
            TileCoordinate station = EmptyNeighbor(world, world.TownAnchors[0]);
            world.RecordContractDelivery(station, "wood", 20);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            long deliveredAtCompletion = contract.Delivered;

            world.RecordContractDelivery(station, "wood", 20);
            Contracts.AdvanceAll(world, catalog, _february);

            Assert.That(contract.Delivered, Is.EqualTo(deliveredAtCompletion));
            Assert.That(world.Statistics.ContractsCompleted, Is.EqualTo(1L));
        }

        [Test]
        public void Claim_PaysTheBonusOnceAndRemovesTheContract()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 1, rewardMultiplier: 1.5m));
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 7);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 7);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            long cashBefore = world.CompanyCash;
            var claim = new ClaimContractRewardCommand(contract.Id, catalog);

            Process(world, claim);

            // 7 units x price 3 x 1.5 = 31.5, rounded down.
            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(31L));
            Assert.That(world.ContractList, Is.Empty);
            Assert.That(world.Finance.Current.Get(FinanceCategory.ContractBonus), Is.EqualTo(31L));
            Assert.That(world.Statistics.LifetimeIncome, Is.EqualTo(31L));
            Assert.That(claim.Validate(world).IsValid, Is.False, "the reward cannot be claimed twice");
        }

        [Test]
        public void Claim_RejectedUnlessTheContractIsCompleted()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract proposal = Offer(world, "wood", 0, 5);
            Contract accepted = OfferAccepted(world, catalog, "wood", 1, 5);

            Assert.That(new ClaimContractRewardCommand(proposal.Id, catalog).Validate(world).IsValid, Is.False);
            Assert.That(new ClaimContractRewardCommand(accepted.Id, catalog).Validate(world).IsValid, Is.False);
            Assert.That(new ClaimContractRewardCommand(99, catalog).Validate(world).IsValid, Is.False);
        }

        [Test]
        public void Claim_AddsThePopulationBonusToTheTargetTownOnly()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, townBonus: 100));
            int before = world.Towns[0].Population;
            int otherBefore = world.Towns[1].Population;
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 5);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            Assert.That(world.Towns[0].Population, Is.EqualTo(before), "completing alone does not grow the town");

            Process(world, new ClaimContractRewardCommand(contract.Id, catalog));

            Assert.That(world.Towns[0].Population, Is.EqualTo(before + 100));
            Assert.That(world.Towns[1].Population, Is.EqualTo(otherBefore));
        }

        [Test]
        public void Claim_TownBonusIsCappedAtMaximumPopulation()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, townBonus: 100));
            int max = catalog.TownNeeds.MaxPopulation;
            world.SetTownPopulation(0, max - 30, catalog.TownNeeds, catalog);
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 5);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);

            Process(world, new ClaimContractRewardCommand(contract.Id, catalog));

            Assert.That(world.Towns[0].Population, Is.EqualTo(max));
        }

        [Test]
        public void ComputePopulationBonus_IsTheConfiguredBonusCappedAtTheMaximumPopulation()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, townBonus: 100));
            int max = catalog.TownNeeds.MaxPopulation;
            world.SetTownPopulation(1, max - 30, catalog.TownNeeds, catalog);
            world.SetTownPopulation(2, max, catalog.TownNeeds, catalog);

            Assert.That(Contracts.ComputePopulationBonus(world, catalog, 0), Is.EqualTo(100));
            Assert.That(Contracts.ComputePopulationBonus(world, catalog, 1), Is.EqualTo(30));
            Assert.That(Contracts.ComputePopulationBonus(world, catalog, 2), Is.Zero);
            Assert.That(Contracts.ComputePopulationBonus(world, catalog, 99), Is.Zero);
        }

        [Test]
        public void Failure_HappensOnlyAfterTheDeadlineWithoutPenalty()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 1));
            Contract contract = OfferAccepted(world, catalog, "wood", 0, 100);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 40);
            long cashBefore = world.CompanyCash;
            int populationBefore = world.Towns[0].Population;

            Contracts.AdvanceAll(world, catalog, contract.Deadline);
            Assert.That(contract.Status, Is.EqualTo(ContractStatus.Accepted));

            var afterDeadline = new GameDate(contract.Deadline.Year, contract.Deadline.Month, contract.Deadline.Day + 1);
            Contracts.AdvanceAll(world, catalog, afterDeadline);

            Assert.That(contract.Status, Is.EqualTo(ContractStatus.Failed));
            Assert.That(contract.SettledDate, Is.EqualTo(afterDeadline));
            Assert.That(contract.Delivered, Is.EqualTo(40L));
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.Towns[0].Population, Is.EqualTo(populationBefore));
            Assert.That(world.ContractList, Has.Count.EqualTo(1), "a failed contract keeps its list slot");
            Assert.That(world.AcceptedContractCount, Is.Zero);
        }

        [Test]
        public void Failure_ProposalsNeverExpire()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 0));
            Contract proposal = Offer(world, "wood", 0, 100);

            Contracts.AdvanceAll(world, catalog, new GameDate(1950, 1, 1));

            Assert.That(proposal.Status, Is.EqualTo(ContractStatus.Proposal));
        }

        [Test]
        public void Dismiss_DeclinesCancelsAndRemovesWithoutAReward()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract proposal = Offer(world, "wood", 0, 100);
            Contract accepted = OfferAccepted(world, catalog, "wood", 1, 100);
            Contract failed = OfferAccepted(world, catalog, "wood", 2, 100);
            Contracts.AdvanceAll(world, catalog, new GameDate(1902, 1, 1));
            Assert.That(failed.Status, Is.EqualTo(ContractStatus.Failed));
            Assert.That(accepted.Status, Is.EqualTo(ContractStatus.Failed));
            long cashBefore = world.CompanyCash;

            Process(world, new DismissContractCommand(proposal.Id, ContractStatus.Proposal));
            Process(world, new DismissContractCommand(accepted.Id, ContractStatus.Failed));
            Process(world, new DismissContractCommand(failed.Id, ContractStatus.Failed));

            Assert.That(world.ContractList, Is.Empty);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
        }

        [Test]
        public void Dismiss_CancelsARunningContract()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract accepted = OfferAccepted(world, catalog, "wood", 1, 100);

            Process(world, new DismissContractCommand(accepted.Id, ContractStatus.Accepted));

            Assert.That(world.ContractList, Is.Empty);
        }

        [Test]
        public void Dismiss_StaleStatusOrUnknownContractIsRejected()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            Contract accepted = OfferAccepted(world, catalog, "wood", 0, 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 5);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);

            Assert.That(accepted.Status, Is.EqualTo(ContractStatus.Completed));
            Assert.That(
                new DismissContractCommand(accepted.Id, ContractStatus.Accepted).Validate(world).IsValid,
                Is.False,
                "cancelling a contract that just completed must not throw the reward away");
            Assert.That(
                new DismissContractCommand(99, ContractStatus.Proposal).Validate(world).IsValid,
                Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DismissContractCommand(accepted.Id, ContractStatus.Completed));
        }

        [Test]
        public void Events_AreEachPublishedOnceAtTheRightMoment()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 2, durationMonths: 1));
            var bus = new EventBus();
            var offered = new List<ContractOfferedEvent>();
            var completed = new List<ContractCompletedEvent>();
            var failed = new List<ContractFailedEvent>();
            bus.Subscribe<ContractOfferedEvent>(offered.Add);
            bus.Subscribe<ContractCompletedEvent>(completed.Add);
            bus.Subscribe<ContractFailedEvent>(failed.Add);

            Contracts.AdvanceAll(world, catalog, _january, bus);
            bus.DispatchPending();
            Assert.That(offered, Has.Count.EqualTo(2));
            Assert.That(completed, Is.Empty);
            Assert.That(failed, Is.Empty);
            Contract finished = world.ContractList[0];
            Contract abandoned = world.ContractList[1];
            Assert.That(offered[0].ContractId, Is.EqualTo(finished.Id));
            Assert.That(offered[0].TownIndex, Is.EqualTo(finished.TownIndex));
            Process(world, new AcceptContractCommand(finished.Id, _january, catalog));
            Process(world, new AcceptContractCommand(abandoned.Id, _january, catalog));

            world.RecordContractDelivery(StationTileFor(world, finished), finished.CargoId, finished.Amount);
            Contracts.AdvanceAll(world, catalog, _lateJanuary, bus);
            bus.DispatchPending();
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].ContractId, Is.EqualTo(finished.Id));
            Assert.That(failed, Is.Empty);

            Contracts.AdvanceAll(world, catalog, new GameDate(1900, 3, 1), bus);
            bus.DispatchPending();
            Assert.That(failed, Has.Count.EqualTo(1));
            Assert.That(failed[0].ContractId, Is.EqualTo(abandoned.Id));
            Assert.That(completed, Has.Count.EqualTo(1));

            // Settled contracts publish nothing again.
            Contracts.AdvanceAll(world, catalog, new GameDate(1900, 3, 2), bus);
            bus.DispatchPending();
            Assert.That(failed, Has.Count.EqualTo(1));
            Assert.That(completed, Has.Count.EqualTo(1));
        }

        [Test]
        public void Completion_RunsBeforeAchievementsSoTheCountIsVisibleOnTheSameTick()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(
                MakeSettings(maxContracts: 6),
                new[]
                {
                    new AchievementDefinition(
                        "contracts",
                        AchievementKind.ContractsCompleted,
                        new[]
                        {
                            new AchievementMilestone(1L, "a.contracts.bronze"),
                            new AchievementMilestone(2L, "a.contracts.silver"),
                            new AchievementMilestone(3L, "a.contracts.gold")
                        },
                        "a.contracts")
                });
            OfferAccepted(world, catalog, "wood", 0, 7);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 7);

            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            Achievements.AdvanceAll(world, catalog, _lateJanuary);

            Assert.That(world.IsAchievementCompleted("contracts.bronze"), Is.True);
            Assert.That(world.Statistics.ContractsCompleted, Is.EqualTo(1L));
        }

        [Test]
        public void IdenticalRuns_ProduceIdenticalContractsAndStateHash()
        {
            string first = HashState(RunScenario());
            string second = HashState(RunScenario());
            GeneratedWorld other = RunScenario();
            other.OfferContract("wood", 0, 5, _january);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(HashState(other), Is.Not.EqualTo(first));
        }

        [Test]
        public void StateHash_IncludesGeneratorState()
        {
            GeneratedWorld a = RunScenario();
            GeneratedWorld b = RunScenario();
            Pcg32Random advanced = GeneratedWorld.CreateContractRandom(_seed);
            advanced.NextUInt32();
            b.RestoreContracts(
                a.ContractList,
                a.NextContractId,
                advanced.CapturedState,
                CreateCatalog(MakeSettings(maxContracts: 6)));

            Assert.That(HashState(b), Is.Not.EqualTo(HashState(a)));
        }

        [Test]
        public void SaveRoundTrip_RestoresContractsIdsAndGeneratorSoTheFutureMatches()
        {
            GeneratedWorld original = RunScenario();
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            SaveSnapshot snapshot = SaveSnapshot.Capture(_seed, Tick.Zero, original);
            GeneratedWorld restored = CreateWorld(3);

            int dropped = restored.RestoreContracts(
                snapshot.ContractList,
                snapshot.NextContractId,
                snapshot.ContractRandomState,
                catalog);

            Assert.That(dropped, Is.Zero);
            Assert.That(HashState(restored), Is.EqualTo(HashState(original)));
            Contracts.AdvanceAll(original, catalog, new GameDate(1900, 6, 1));
            Contracts.AdvanceAll(restored, catalog, new GameDate(1900, 6, 1));
            Assert.That(HashState(restored), Is.EqualTo(HashState(original)));
        }

        [Test]
        public void Restore_KeepsEveryStatusAndDropsContractsWhoseCargoOrTownIsNoLongerValid()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6));
            var saved = new List<Contract>
            {
                MakeContract(1, "wood", 0, ContractStatus.Proposal),
                MakeContract(2, "oldCargo", 1, ContractStatus.Proposal),
                MakeContract(3, "wood", 99, ContractStatus.Accepted),
                MakeContract(4, "passengers", 2, ContractStatus.Failed),
                MakeContract(5, "wood", 1, ContractStatus.Completed),
                MakeContract(6, "wood", 2, ContractStatus.Failed),
                MakeContract(7, "wood", 2, ContractStatus.Accepted)
            };

            int dropped = world.RestoreContracts(
                saved,
                8,
                GeneratedWorld.CreateContractRandom(_seed).CapturedState,
                catalog);

            Assert.That(dropped, Is.EqualTo(4));
            Assert.That(world.ContractList, Has.Count.EqualTo(3));
            Assert.That(world.ContractList[0].Status, Is.EqualTo(ContractStatus.Proposal));
            Assert.That(world.ContractList[1].Status, Is.EqualTo(ContractStatus.Completed));
            Assert.That(world.ContractList[2].Id, Is.EqualTo(6));
            Assert.That(world.ContractList[2].Status, Is.EqualTo(ContractStatus.Failed));
            Assert.That(world.NextContractId, Is.EqualTo(8));
        }

        [Test]
        public void Restore_AfterAllContractsWereDropped_ProposesOneAtTheNextMonth()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 3));
            world.RestoreContracts(
                new List<Contract> { MakeContract(1, "oldCargo", 0, ContractStatus.Proposal) },
                2,
                GeneratedWorld.CreateContractRandom(_seed).CapturedState,
                catalog);

            Contracts.AdvanceAll(world, catalog, _january);

            // A restored game is not a new game: no initial proposals, one new proposal for the month.
            Assert.That(world.ContractList, Has.Count.EqualTo(1));
            Assert.That(world.ContractList[0].Id, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_RejectsInconsistentStatusFields()
        {
            Assert.Throws<ArgumentException>(
                () => new Contract(1, "wood", 0, 5, 1L, ContractStatus.Proposal, _january, _january, _january));
            Assert.Throws<ArgumentException>(
                () => new Contract(1, "wood", 0, 5, 0L, ContractStatus.Proposal, _january, _february, _january));
            Assert.Throws<ArgumentException>(
                () => new Contract(1, "wood", 0, 5, 0L, ContractStatus.Accepted, _january, _february, _february));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Contract(1, "wood", 0, 5, 0L, (ContractStatus)9, _january, _january, _january));
            Assert.DoesNotThrow(
                () => new Contract(1, "wood", 0, 5, 5L, ContractStatus.Completed, _january, _february, _february));
        }

        [Test]
        public void GameDate_AddMonths_KeepsOrClampsTheDay()
        {
            Assert.That(new GameDate(1900, 1, 15).AddMonths(12), Is.EqualTo(new GameDate(1901, 1, 15)));
            Assert.That(new GameDate(1900, 12, 31).AddMonths(2), Is.EqualTo(new GameDate(1901, 2, 28)));
            Assert.That(new GameDate(1900, 5, 31).AddMonths(0), Is.EqualTo(new GameDate(1900, 5, 31)));
        }

        private static GeneratedWorld RunScenario()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 6, initialProposals: 2));
            Contracts.AdvanceAll(world, catalog, _january);
            Contract first = world.ContractList[0];
            Process(world, new AcceptContractCommand(first.Id, _january, catalog));
            world.RecordContractDelivery(StationTileFor(world, first), first.CargoId, first.Amount);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            Contracts.AdvanceAll(world, catalog, _february);
            return world;
        }

        private static void Process(GeneratedWorld world, ISimulationCommand<GeneratedWorld> command)
        {
            CommandProcessingResult result = new SimulationCommandProcessor<GeneratedWorld>(world)
                .Process(Tick.Zero, command);
            Assert.That(result.IsAccepted, Is.True, result.RejectionReason);
        }

        private static string HashState(GeneratedWorld world)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)2);
                writer.Write(world.NextContractId);
                writer.Write(world.ContractRandomState.State);
                writer.Write(world.ContractRandomState.Increment);
                writer.Write(world.ContractList.Count);
                for (int index = 0; index < world.ContractList.Count; index++)
                {
                    Contract contract = world.ContractList[index];
                    writer.Write(contract.Id);
                    writer.Write(contract.CargoId);
                    writer.Write(contract.TownIndex);
                    writer.Write(contract.Amount);
                    writer.Write(contract.Delivered);
                    writer.Write((int)contract.Status);
                    WriteDate(writer, contract.StartDate);
                    WriteDate(writer, contract.Deadline);
                    WriteDate(writer, contract.SettledDate);
                }

                writer.Flush();
                return DeterministicStateHasher.ToHexString(DeterministicStateHasher.ComputeSha256(stream.ToArray()));
            }
        }

        private static void WriteDate(BinaryWriter writer, GameDate date)
        {
            writer.Write(date.Year);
            writer.Write(date.Month);
            writer.Write(date.Day);
        }

        private static Contract MakeContract(int id, string cargoId, int townIndex, ContractStatus status)
        {
            var start = new GameDate(1900, 1, 1);
            var end = new GameDate(1901, 1, 1);
            switch (status)
            {
                case ContractStatus.Proposal:
                    return new Contract(id, cargoId, townIndex, 50, 0L, status, start, start, start);
                case ContractStatus.Accepted:
                    return new Contract(id, cargoId, townIndex, 50, 10L, status, start, end, start);
                case ContractStatus.Completed:
                    return new Contract(id, cargoId, townIndex, 50, 50L, status, start, end, end);
                default:
                    return new Contract(id, cargoId, townIndex, 50, 10L, status, start, end, end);
            }
        }

        private static Contract Offer(GeneratedWorld world, string cargoId, int townIndex, int amount)
        {
            return world.OfferContract(cargoId, townIndex, amount, _january);
        }

        private static Contract OfferAccepted(
            GeneratedWorld world,
            ContentCatalog catalog,
            string cargoId,
            int townIndex,
            int amount)
        {
            Contract contract = Offer(world, cargoId, townIndex, amount);
            Contracts.Accept(world, catalog, contract.Id, _january);
            return contract;
        }

        private static int SawmillIndex(GeneratedWorld world)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (string.Equals(world.Industries[index].IndustryId, "sawmill", StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new InvalidOperationException("The sawmill was not placed.");
        }

        private static TileCoordinate StationTileFor(GeneratedWorld world, Contract contract)
        {
            return EmptyNeighbor(world, world.TownAnchors[contract.TownIndex]);
        }

        private static TileCoordinate EmptyNeighbor(GeneratedWorld world, TileCoordinate site)
        {
            var offsets = new[]
            {
                new TileCoordinate(0, -1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(-1, 0)
            };
            for (int index = 0; index < offsets.Length; index++)
            {
                var candidate = new TileCoordinate(site.X + offsets[index].X, site.Y + offsets[index].Y);
                if (world.Map.Contains(candidate) && world.Map.GetObjectId(candidate) == TileMap.NoObjectId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty neighbor next to the site.");
        }

        // The same limits for every map size keep the tests independent of the generated size.
        private static ContractSettings MakeSettings(
            int maxContracts,
            int? maxAccepted = null,
            int initialProposals = 0,
            int minAmount = 50,
            int maxAmount = 200,
            int durationMonths = 12,
            decimal rewardMultiplier = 1.0m,
            int townBonus = 100)
        {
            var limits = new ContractLimits(maxContracts, maxAccepted ?? maxContracts);
            return new ContractSettings(
                limits,
                limits,
                limits,
                initialProposals,
                minAmount,
                maxAmount,
                durationMonths,
                rewardMultiplier,
                townBonus);
        }

        private static ContentCatalog CreateCatalog(
            ContractSettings contracts,
            IReadOnlyList<AchievementDefinition>? achievements = null)
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 3, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 5, 2),
                new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 2, 0)
            };
            var industries = new[]
            {
                new IndustryDefinition(
                    "forest",
                    "industry.forest",
                    1,
                    Array.Empty<CargoAmountDefinition>(),
                    new[] { new CargoAmountDefinition("wood", 4) }),
                new IndustryDefinition(
                    "sawmill",
                    "industry.sawmill",
                    2,
                    new[] { new CargoAmountDefinition("wood", 2) },
                    new[] { new CargoAmountDefinition("planks", 1) })
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                demandPerNeedPerMonth: 10,
                allNeedsMetGrowthMultiplier: 1);
            return new ContentCatalog(
                cargo,
                industries,
                townNeeds,
                achievements: achievements,
                contracts: contracts);
        }

        private static GeneratedWorld CreateWorld(int towns)
        {
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxContracts: 0));
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, towns, 16),
                4,
                rules);
            return WorldGenerator.Generate(settings, _seed, catalog.TownNeeds, catalog);
        }
    }
}
