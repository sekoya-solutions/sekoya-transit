#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Tests.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
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
        public void AdvanceAll_FirstCall_TopsUpToTheMaximum()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 3));

            Contracts.AdvanceAll(world, catalog, _january);

            Assert.That(world.OpenContracts, Has.Count.EqualTo(3));
        }

        [Test]
        public void AdvanceAll_SameMonth_DoesNotTopUpAgainButNewMonthDoes()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 3));
            Contracts.AdvanceAll(world, catalog, _january);
            Contract done = world.OpenContracts[0];
            world.RecordContractDelivery(StationTileFor(world, done), done.CargoId, done.Amount);

            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            Assert.That(world.OpenContracts, Has.Count.EqualTo(2));

            Contracts.AdvanceAll(world, catalog, _february);
            Assert.That(world.OpenContracts, Has.Count.EqualTo(3));
        }

        [Test]
        public void TopUp_NeverExceedsTheMaximumOrWorldWithNoSlots()
        {
            GeneratedWorld world = CreateWorld(3);

            Contracts.AdvanceAll(world, CreateCatalog(MakeSettings(maxOpen: 0)), _january);

            Assert.That(world.OpenContracts, Is.Empty);
            Assert.That(world.NextContractId, Is.EqualTo(GeneratedWorld.FirstContractId));
        }

        [Test]
        public void TopUp_FewerTargetsThanSlots_CreatesFewerWithoutError()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 10));

            Assert.DoesNotThrow(() => Contracts.AdvanceAll(world, catalog, _january));

            // Three towns that need wood plus the sawmill that accepts wood.
            Assert.That(world.OpenContracts, Has.Count.EqualTo(4));
        }

        [Test]
        public void TopUp_NeverDuplicatesATargetAndNeverPicksPassengers()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 10));

            Contracts.AdvanceAll(world, catalog, _january);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < world.OpenContracts.Count; index++)
            {
                Contract contract = world.OpenContracts[index];
                Assert.That(contract.CargoId, Is.Not.EqualTo("passengers"));
                Assert.That(
                    seen.Add(contract.CargoId + "|" + contract.TargetKind + "|" + contract.TargetIndex),
                    Is.True);
                Assert.That(
                    Contracts.IsValid(world, catalog, contract.CargoId, contract.TargetKind, contract.TargetIndex),
                    Is.True);
            }
        }

        [Test]
        public void TopUp_RollsAmountInRangeSetsDeadlineAndNumbersSequentially()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 4, minAmount: 10, maxAmount: 12, durationMonths: 14));

            Contracts.AdvanceAll(world, catalog, _january);

            for (int index = 0; index < world.OpenContracts.Count; index++)
            {
                Contract contract = world.OpenContracts[index];
                Assert.That(contract.Id, Is.EqualTo(index + 1));
                Assert.That(contract.Amount, Is.InRange(10, 12));
                Assert.That(contract.Delivered, Is.Zero);
                Assert.That(contract.StartDate, Is.EqualTo(_january));
                Assert.That(contract.Deadline, Is.EqualTo(new GameDate(1901, 3, 15)));
            }

            Assert.That(world.NextContractId, Is.EqualTo(5));
        }

        [Test]
        public void TopUp_IdsNeverRepeatAcrossCompletionAndRefill()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 2));
            Contracts.AdvanceAll(world, catalog, _january);
            Contract done = world.OpenContracts[0];
            world.RecordContractDelivery(StationTileFor(world, done), done.CargoId, done.Amount);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);

            Contracts.AdvanceAll(world, catalog, _february);

            Assert.That(world.OpenContracts[0].Id, Is.EqualTo(2));
            Assert.That(world.OpenContracts[1].Id, Is.EqualTo(3));
        }

        [Test]
        public void Delivery_OnlyTheContractCargoToTheContractTargetCounts()
        {
            GeneratedWorld world = CreateWorld(3);
            Contract woodToTown0 = Offer(world, "wood", DeliveryDestinationKind.Town, 0, 100);
            Contract planksToTown0 = Offer(world, "planks", DeliveryDestinationKind.Town, 0, 100);
            Contract woodToTown1 = Offer(world, "wood", DeliveryDestinationKind.Town, 1, 100);
            Contract woodToSawmill = Offer(world, "wood", DeliveryDestinationKind.Industry, SawmillIndex(world), 100);

            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 7);

            Assert.That(woodToTown0.Delivered, Is.EqualTo(7L));
            Assert.That(planksToTown0.Delivered, Is.Zero);
            Assert.That(woodToTown1.Delivered, Is.Zero);
            Assert.That(woodToSawmill.Delivered, Is.Zero);

            world.RecordContractDelivery(
                EmptyNeighbor(world, world.Industries[SawmillIndex(world)].Coordinate),
                "wood",
                3);

            Assert.That(woodToSawmill.Delivered, Is.EqualTo(3L));
            Assert.That(woodToTown0.Delivered, Is.EqualTo(7L));
        }

        [Test]
        public void Delivery_AwayFromAnySiteOrNonPositiveAmountIsIgnored()
        {
            GeneratedWorld world = CreateWorld(3);
            Contract contract = Offer(world, "wood", DeliveryDestinationKind.Town, 0, 100);

            world.RecordContractDelivery(new TileCoordinate(0, 0), "wood", 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 0);

            Assert.That(contract.Delivered, Is.Zero);
        }

        [Test]
        public void Completion_PaysTheBonusOnceAndRemovesTheContract()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0, rewardMultiplier: 1.5m));
            Contract contract = Offer(world, "wood", DeliveryDestinationKind.Industry, SawmillIndex(world), 7);
            long cashBefore = world.CompanyCash;
            world.RecordContractDelivery(
                EmptyNeighbor(world, world.Industries[SawmillIndex(world)].Coordinate),
                "wood",
                7);

            Contracts.AdvanceAll(world, catalog, _january);

            // 7 units x price 3 x 1.5 = 31.5, rounded down.
            Assert.That(world.CompanyCash - cashBefore, Is.EqualTo(31L));
            Assert.That(world.OpenContracts, Is.Empty);
            Assert.That(contract.Id, Is.EqualTo(1));
        }

        [Test]
        public void Completion_OverDeliveryIsNotRewardedTwice()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0));
            int sawmill = SawmillIndex(world);
            TileCoordinate station = EmptyNeighbor(world, world.Industries[sawmill].Coordinate);
            Offer(world, "wood", DeliveryDestinationKind.Industry, sawmill, 5);
            world.RecordContractDelivery(station, "wood", 20);
            Contracts.AdvanceAll(world, catalog, _january);
            long cashAfterReward = world.CompanyCash;

            world.RecordContractDelivery(station, "wood", 20);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);

            Assert.That(world.CompanyCash, Is.EqualTo(cashAfterReward));
            Assert.That(world.OpenContracts, Is.Empty);
        }

        [Test]
        public void Completion_TownContractAddsPopulationBonus()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0, townBonus: 100));
            int before = world.Towns[0].Population;
            int otherBefore = world.Towns[1].Population;
            Offer(world, "wood", DeliveryDestinationKind.Town, 0, 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 5);

            Contracts.AdvanceAll(world, catalog, _january);

            Assert.That(world.Towns[0].Population, Is.EqualTo(before + 100));
            Assert.That(world.Towns[1].Population, Is.EqualTo(otherBefore));
        }

        [Test]
        public void Completion_TownBonusIsCappedAtMaximumPopulation()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0, townBonus: 100));
            int max = catalog.TownNeeds.MaxPopulation;
            world.SetTownPopulation(0, max - 30, catalog.TownNeeds, catalog);
            Offer(world, "wood", DeliveryDestinationKind.Town, 0, 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 5);
            var bus = new EventBus();
            var completed = new List<ContractCompletedEvent>();
            bus.Subscribe<ContractCompletedEvent>(completed.Add);

            Contracts.AdvanceAll(world, catalog, _january, bus);
            bus.DispatchPending();

            Assert.That(world.Towns[0].Population, Is.EqualTo(max));
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].PopulationBonus, Is.EqualTo(30));
        }

        [Test]
        public void Completion_IndustryContractGivesNoPopulationBonus()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0, townBonus: 100));
            var populations = new int[world.Towns.Count];
            for (int index = 0; index < populations.Length; index++)
            {
                populations[index] = world.Towns[index].Population;
            }

            int sawmill = SawmillIndex(world);
            Offer(world, "wood", DeliveryDestinationKind.Industry, sawmill, 5);
            world.RecordContractDelivery(EmptyNeighbor(world, world.Industries[sawmill].Coordinate), "wood", 5);

            Contracts.AdvanceAll(world, catalog, _january);

            for (int index = 0; index < populations.Length; index++)
            {
                Assert.That(world.Towns[index].Population, Is.EqualTo(populations[index]));
            }
        }

        [Test]
        public void Expiry_RemovesTheContractWithoutPenaltyOnlyAfterTheDeadline()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0));
            Contract contract = Offer(world, "wood", DeliveryDestinationKind.Town, 0, 100);
            world.RecordContractDelivery(EmptyNeighbor(world, world.TownAnchors[0]), "wood", 40);
            long cashBefore = world.CompanyCash;
            int populationBefore = world.Towns[0].Population;

            Contracts.AdvanceAll(world, catalog, contract.Deadline);
            Assert.That(world.OpenContracts, Has.Count.EqualTo(1));

            Contracts.AdvanceAll(world, catalog, new GameDate(contract.Deadline.Year, contract.Deadline.Month, contract.Deadline.Day + 1));

            Assert.That(world.OpenContracts, Is.Empty);
            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.Towns[0].Population, Is.EqualTo(populationBefore));
        }

        [Test]
        public void Events_AreEachPublishedOnceAtTheRightMoment()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 2, durationMonths: 1));
            var bus = new EventBus();
            var offered = new List<ContractOfferedEvent>();
            var completed = new List<ContractCompletedEvent>();
            var expired = new List<ContractExpiredEvent>();
            bus.Subscribe<ContractOfferedEvent>(offered.Add);
            bus.Subscribe<ContractCompletedEvent>(completed.Add);
            bus.Subscribe<ContractExpiredEvent>(expired.Add);

            Contracts.AdvanceAll(world, catalog, _january, bus);
            bus.DispatchPending();
            Assert.That(offered, Has.Count.EqualTo(2));
            Assert.That(completed, Is.Empty);
            Assert.That(expired, Is.Empty);
            Contract finished = world.OpenContracts[0];
            Contract abandoned = world.OpenContracts[1];
            Assert.That(offered[0].ContractId, Is.EqualTo(finished.Id));
            Assert.That(offered[1].Deadline, Is.EqualTo(abandoned.Deadline));

            world.RecordContractDelivery(StationTileFor(world, finished), finished.CargoId, finished.Amount);
            Contracts.AdvanceAll(world, catalog, _lateJanuary, bus);
            bus.DispatchPending();
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].ContractId, Is.EqualTo(finished.Id));
            Assert.That(completed[0].Reward, Is.GreaterThan(0L));
            Assert.That(expired, Is.Empty);

            Contracts.AdvanceAll(world, catalog, new GameDate(1900, 3, 1), bus);
            bus.DispatchPending();
            Assert.That(expired, Has.Count.EqualTo(1));
            Assert.That(expired[0].ContractId, Is.EqualTo(abandoned.Id));
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(offered, Has.Count.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void Completion_RunsBeforeAchievementsSoRewardsCountOnTheSameTick()
        {
            GeneratedWorld world = CreateWorld(3);
            long goal = world.CompanyCash + 21L;
            ContentCatalog catalog = CreateCatalog(
                MakeSettings(maxOpen: 0),
                new[] { new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, goal) });
            int sawmill = SawmillIndex(world);
            Offer(world, "wood", DeliveryDestinationKind.Industry, sawmill, 7);
            world.RecordContractDelivery(EmptyNeighbor(world, world.Industries[sawmill].Coordinate), "wood", 7);

            Contracts.AdvanceAll(world, catalog, _january);
            Achievements.AdvanceAll(world, catalog, _january);

            Assert.That(world.IsAchievementCompleted("cash"), Is.True);
        }

        [Test]
        public void IdenticalRuns_ProduceIdenticalContractsAndStateHash()
        {
            string first = HashState(RunScenario());
            string second = HashState(RunScenario());
            GeneratedWorld other = RunScenario();
            other.OfferContract("wood", DeliveryDestinationKind.Town, 0, 5, _january, _february);

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
                a.OpenContracts,
                a.NextContractId,
                advanced.CapturedState,
                CreateCatalog(MakeSettings(maxOpen: 3)));

            Assert.That(HashState(b), Is.Not.EqualTo(HashState(a)));
        }

        [Test]
        public void SaveRoundTrip_RestoresContractsIdsAndGeneratorSoTheFutureMatches()
        {
            GeneratedWorld original = RunScenario();
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 3));
            SaveSnapshot snapshot = SaveSnapshot.Capture(_seed, Tick.Zero, original);
            GeneratedWorld restored = CreateWorld(3);

            int dropped = restored.RestoreContracts(
                snapshot.OpenContracts,
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
        public void Restore_DropsContractsWhoseCargoOrTargetIsNoLongerValid()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 3));
            var saved = new List<Contract>
            {
                MakeContract(1, "wood", DeliveryDestinationKind.Town, 0),
                MakeContract(2, "oldCargo", DeliveryDestinationKind.Town, 1),
                MakeContract(3, "wood", DeliveryDestinationKind.Town, 99),
                MakeContract(4, "planks", DeliveryDestinationKind.Industry, SawmillIndex(world)),
                MakeContract(5, "passengers", DeliveryDestinationKind.Town, 2),
                MakeContract(6, "wood", DeliveryDestinationKind.Industry, SawmillIndex(world))
            };

            int dropped = world.RestoreContracts(
                saved,
                7,
                GeneratedWorld.CreateContractRandom(_seed).CapturedState,
                catalog);

            Assert.That(dropped, Is.EqualTo(4));
            Assert.That(world.OpenContracts, Has.Count.EqualTo(2));
            Assert.That(world.OpenContracts[0].Id, Is.EqualTo(1));
            Assert.That(world.OpenContracts[1].Id, Is.EqualTo(6));
            Assert.That(world.NextContractId, Is.EqualTo(7));
        }

        [Test]
        public void Restore_DroppedContractsAreNotCountedAndFirstTickTopsUp()
        {
            GeneratedWorld world = CreateWorld(3);
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 3));
            world.RestoreContracts(
                new List<Contract> { MakeContract(1, "oldCargo", DeliveryDestinationKind.Town, 0) },
                2,
                GeneratedWorld.CreateContractRandom(_seed).CapturedState,
                catalog);

            Contracts.AdvanceAll(world, catalog, _january);

            Assert.That(world.OpenContracts, Has.Count.EqualTo(3));
            Assert.That(world.OpenContracts[0].Id, Is.EqualTo(2));
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
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 3));
            Contracts.AdvanceAll(world, catalog, _january);
            Contract first = world.OpenContracts[0];
            world.RecordContractDelivery(StationTileFor(world, first), first.CargoId, first.Amount);
            Contracts.AdvanceAll(world, catalog, _lateJanuary);
            Contracts.AdvanceAll(world, catalog, _february);
            return world;
        }

        private static string HashState(GeneratedWorld world)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)1);
                writer.Write(world.NextContractId);
                writer.Write(world.ContractRandomState.State);
                writer.Write(world.ContractRandomState.Increment);
                writer.Write(world.OpenContracts.Count);
                for (int index = 0; index < world.OpenContracts.Count; index++)
                {
                    Contract contract = world.OpenContracts[index];
                    writer.Write(contract.Id);
                    writer.Write(contract.CargoId);
                    writer.Write((int)contract.TargetKind);
                    writer.Write(contract.TargetIndex);
                    writer.Write(contract.Amount);
                    writer.Write(contract.Delivered);
                    writer.Write(contract.StartDate.Year);
                    writer.Write(contract.StartDate.Month);
                    writer.Write(contract.StartDate.Day);
                    writer.Write(contract.Deadline.Year);
                    writer.Write(contract.Deadline.Month);
                    writer.Write(contract.Deadline.Day);
                }

                writer.Flush();
                return DeterministicStateHasher.ToHexString(DeterministicStateHasher.ComputeSha256(stream.ToArray()));
            }
        }

        private static Contract MakeContract(int id, string cargoId, DeliveryDestinationKind kind, int targetIndex)
        {
            return new Contract(
                id,
                cargoId,
                kind,
                targetIndex,
                50,
                0L,
                new GameDate(1900, 1, 1),
                new GameDate(1901, 1, 1));
        }

        private static Contract Offer(
            GeneratedWorld world,
            string cargoId,
            DeliveryDestinationKind kind,
            int targetIndex,
            int amount)
        {
            return world.OfferContract(cargoId, kind, targetIndex, amount, _january, new GameDate(1901, 1, 15));
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
            TileCoordinate site = contract.TargetKind == DeliveryDestinationKind.Town
                ? world.TownAnchors[contract.TargetIndex]
                : world.Industries[contract.TargetIndex].Coordinate;
            return EmptyNeighbor(world, site);
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

        private static ContractSettings MakeSettings(
            int maxOpen,
            int minAmount = 50,
            int maxAmount = 200,
            int durationMonths = 12,
            decimal rewardMultiplier = 1.0m,
            int townBonus = 100)
        {
            return new ContractSettings(maxOpen, minAmount, maxAmount, durationMonths, rewardMultiplier, townBonus);
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
                populationPerSatisfiedNeed: 20);
            return new ContentCatalog(
                cargo,
                industries,
                townNeeds,
                achievements: achievements,
                contracts: contracts);
        }

        private static GeneratedWorld CreateWorld(int towns)
        {
            ContentCatalog catalog = CreateCatalog(MakeSettings(maxOpen: 0));
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
