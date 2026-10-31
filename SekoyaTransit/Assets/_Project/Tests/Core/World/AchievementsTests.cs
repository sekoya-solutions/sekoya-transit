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
    public sealed class AchievementsTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };
        private static readonly GameDate _firstDate = new GameDate(1901, 2, 3);
        private static readonly GameDate _laterDate = new GameDate(1905, 7, 9);

        [Test]
        public void LargestTown_CompletesAtGoalAndNotBefore()
        {
            GeneratedWorld world = CreateWorld();
            int largest = LargestPopulation(world);
            ContentCatalog catalog = CreateCatalog(
                Track("town", AchievementKind.LargestTown, largest - 1L, largest, largest + 1L));

            int completed = Achievements.AdvanceAll(world, catalog, _firstDate);

            Assert.That(completed, Is.EqualTo(2));
            Assert.That(world.IsAchievementCompleted("town.bronze"), Is.True);
            Assert.That(world.IsAchievementCompleted("town.silver"), Is.True);
            Assert.That(world.IsAchievementCompleted("town.gold"), Is.False);
        }

        [Test]
        public void LifetimeIncome_CountsDeliveriesAndContractBonusesButNotStartCashOrSales()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(Track("income", AchievementKind.LifetimeIncome, 100L, 200L, 300L));

            world.CreditCompanyCash(1000L, FinanceCategory.VehicleSales);
            world.CreditCompanyCash(60L, FinanceCategory.DeliveryIncome);
            world.CreditCompanyCash(39L, FinanceCategory.ContractBonus);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.Zero);

            world.CreditCompanyCash(1L, FinanceCategory.DeliveryIncome);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.Statistics.LifetimeIncome, Is.EqualTo(100L));
            Assert.That(world.IsAchievementCompleted("income.bronze"), Is.True);
            Assert.That(world.IsAchievementCompleted("income.silver"), Is.False);
        }

        [Test]
        public void LifetimeIncome_NeverDecreasesWhenCashIsSpent()
        {
            GeneratedWorld world = CreateWorld();
            world.CreditCompanyCash(500L, FinanceCategory.DeliveryIncome);

            world.DebitCompanyCashClamped(world.CompanyCash, FinanceCategory.RunningCosts);

            Assert.That(world.Statistics.LifetimeIncome, Is.EqualTo(500L));
        }

        [Test]
        public void GetProgress_ReportsCurrentValuePerKindWithoutChangingState()
        {
            GeneratedWorld world = CreateWorld();
            world.CreditCompanyCash(42L, FinanceCategory.DeliveryIncome);
            world.Statistics.AddContractCompleted();
            world.Statistics.AddRoadTiles(11L);
            world.Statistics.AddRailTiles(13L);
            world.RecordCargoDelivered("wood", DeliveryDestinationKind.Industry, 7);
            world.RecordCargoDelivered("wood", DeliveryDestinationKind.Town, 2);
            ContentCatalog catalog = CreateCatalog();

            Assert.That(Progress(world, catalog, AchievementKind.LifetimeIncome), Is.EqualTo(42L));
            Assert.That(Progress(world, catalog, AchievementKind.ContractsCompleted), Is.EqualTo(1L));
            Assert.That(Progress(world, catalog, AchievementKind.VehicleTypesUnlocked), Is.Zero);
            Assert.That(Progress(world, catalog, AchievementKind.RoadVehicleCount), Is.Zero);
            Assert.That(Progress(world, catalog, AchievementKind.TrainCount), Is.Zero);
            Assert.That(Progress(world, catalog, AchievementKind.RoadTilesTravelled), Is.EqualTo(11L));
            Assert.That(Progress(world, catalog, AchievementKind.RailTilesTravelled), Is.EqualTo(13L));
            Assert.That(Progress(world, catalog, AchievementKind.LargestTown), Is.EqualTo(LargestPopulation(world)));
            Assert.That(
                Achievements.GetProgress(world, catalog, CargoTrack("wood_sold", "wood", 1L, 2L, 3L)),
                Is.EqualTo(9L));
            Assert.That(world.AchievementCompletions, Is.Empty);
        }

        [Test]
        public void VehicleCounts_SplitRoadVehiclesAndTrainsAndIgnoreWagons()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                Track("trucks", AchievementKind.RoadVehicleCount, 2L, 3L, 4L),
                Track("trains", AchievementKind.TrainCount, 1L, 2L, 3L));
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate roadTile = FindEmptyTile(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(roadTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(roadTile, out RoadStation? roadStation), Is.True);
            TileCoordinate railTile = FindEmptyTile(world, world.TownAnchors[1]);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(railTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(railTile, out RailStation? railStation), Is.True);
            TileCoordinate secondRoadTile = FindEmptyTile(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(2), new BuildRoadStationCommand(secondRoadTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(secondRoadTile, out RoadStation? secondRoadStation), Is.True);
            TileCoordinate secondRailTile = FindEmptyTile(world, world.TownAnchors[1]);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(secondRailTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(secondRailTile, out RailStation? secondRailStation), Is.True);

            RouteTestSupport.PlaceRoadVehicle(processor, world, catalog, roadStation!.Id, secondRoadStation!.Id);
            RouteTestSupport.PlaceRoadVehicle(processor, world, catalog, roadStation.Id, secondRoadStation.Id);
            int trainId = RouteTestSupport.PlaceRailVehicle(processor, world, catalog, railStation!.Id, secondRailStation!.Id);
            Assert.That(processor.Process(new Tick(6), new AddRailWagonCommand(trainId, catalog)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(7), new AddRailWagonCommand(trainId, catalog)).IsAccepted, Is.True);

            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(2));
            Assert.That(world.IsAchievementCompleted("trucks.bronze"), Is.True);
            Assert.That(world.IsAchievementCompleted("trucks.silver"), Is.False);
            Assert.That(world.IsAchievementCompleted("trains.bronze"), Is.True);
            Assert.That(world.IsAchievementCompleted("trains.silver"), Is.False);
        }

        [Test]
        public void VehicleTypesUnlocked_CountsPaidUnlocks()
        {
            GeneratedWorld world = CreateWorld();
            var unlockable = new VehicleDefinition(
                "truck_mk2",
                "vehicle.truck_mk2",
                VehicleMode.Road,
                12000L,
                250L,
                1910,
                50000L,
                false,
                cargoCapacity: 12,
                speedThousandths: 300);
            var vehicles = new List<VehicleDefinition>(ContentCatalog.CreatePlaceholderVehicles()) { unlockable };
            var catalog = new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: vehicles,
                achievements: new[] { Track("models", AchievementKind.VehicleTypesUnlocked, 1L, 2L, 3L) });
            world.CreditCompanyCash(unlockable.UnlockCost, FinanceCategory.VehicleSales);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(new Tick(1), new UnlockVehicleTypeCommand(unlockable.Id, 1950, catalog)).IsAccepted,
                Is.True);

            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("models.bronze"), Is.True);
        }

        [Test]
        public void CargoSold_CountsTownAndIndustryDeliveriesOfThatCargoOnly()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                CargoTrack("bread_sold", "bread", 5L, 8L, 20L),
                CargoTrack("wood_sold", "wood", 1L, 2L, 3L));

            world.RecordCargoDelivered("bread", DeliveryDestinationKind.Town, 4);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.Zero);

            world.RecordCargoDelivered("bread", DeliveryDestinationKind.Industry, 4);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(2));
            Assert.That(world.IsAchievementCompleted("bread_sold.silver"), Is.True);
            Assert.That(world.IsAchievementCompleted("wood_sold.bronze"), Is.False);
        }

        [Test]
        public void ScalingTrack_UsesTheMapSizeMultiplierAndOthersDoNot()
        {
            GeneratedWorld world = CreateWorld();
            AchievementDefinition scaled = Track("road", AchievementKind.RoadTilesTravelled, 100L, 200L, 300L, scales: true);
            AchievementDefinition fixedGoal = Track("rail", AchievementKind.RailTilesTravelled, 100L, 200L, 300L);
            ContentCatalog catalog = CreateCatalog(new AchievementGoalScale(50, 100, 200), scaled, fixedGoal);

            Assert.That(Achievements.GetGoal(world, catalog, scaled, AchievementTier.Silver), Is.EqualTo(100L));
            Assert.That(Achievements.GetGoal(world, catalog, fixedGoal, AchievementTier.Silver), Is.EqualTo(200L));

            world.Statistics.AddRoadTiles(50L);
            world.Statistics.AddRailTiles(50L);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("road.bronze"), Is.True);
            Assert.That(world.IsAchievementCompleted("rail.bronze"), Is.False);
        }

        [Test]
        public void GlobalTrack_GoalsArePercentagesOfOtherMilestonesRoundedUp()
        {
            GeneratedWorld world = CreateWorld();
            AchievementDefinition global = Track("global", AchievementKind.AchievementsUnlocked, 20L, 50L, 100L);
            ContentCatalog catalog = CreateCatalog(
                global,
                Track("income", AchievementKind.LifetimeIncome, 1L, 2L, 3L),
                Track("contracts", AchievementKind.ContractsCompleted, 1L, 2L, 3L),
                Track("road", AchievementKind.RoadTilesTravelled, 1L, 2L, 3L));

            Assert.That(Achievements.CountMilestones(catalog), Is.EqualTo(9));
            Assert.That(Achievements.GetGoal(world, catalog, global, AchievementTier.Bronze), Is.EqualTo(2L));
            Assert.That(Achievements.GetGoal(world, catalog, global, AchievementTier.Silver), Is.EqualTo(5L));
            Assert.That(Achievements.GetGoal(world, catalog, global, AchievementTier.Gold), Is.EqualTo(9L));
        }

        [Test]
        public void GlobalTrack_CountsMilestonesCompletedInTheSameCallAndIgnoresItsOwn()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                Track("global", AchievementKind.AchievementsUnlocked, 20L, 50L, 100L),
                Track("income", AchievementKind.LifetimeIncome, 1L, 2L, 3L),
                Track("contracts", AchievementKind.ContractsCompleted, 1L, 2L, 3L));
            world.CreditCompanyCash(2L, FinanceCategory.DeliveryIncome);

            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(3));
            Assert.That(world.IsAchievementCompleted("global.bronze"), Is.True);
            Assert.That(world.IsAchievementCompleted("global.silver"), Is.False);
            Assert.That(Achievements.GetProgress(world, catalog, catalog.Achievements[0]), Is.EqualTo(2L));
        }

        [Test]
        public void Completion_IsPermanentAndKeepsFirstDate()
        {
            GeneratedWorld world = CreateWorld();
            int largest = LargestPopulation(world);
            ContentCatalog catalog = CreateCatalog(Track("town", AchievementKind.LargestTown, largest, largest + 1L, largest + 2L));
            Achievements.AdvanceAll(world, catalog, _firstDate);

            for (int town = 0; town < world.Towns.Count; town++)
            {
                world.SetTownPopulation(town, 1, catalog.TownNeeds, catalog);
            }

            int completed = Achievements.AdvanceAll(world, catalog, _laterDate);

            Assert.That(completed, Is.Zero);
            Assert.That(world.IsAchievementCompleted("town.bronze"), Is.True);
            Assert.That(world.AchievementCompletions, Has.Count.EqualTo(1));
            Assert.That(world.AchievementCompletions[0].Date, Is.EqualTo(_firstDate));
        }

        [Test]
        public void Completion_PaysNothingAndDoesNotEndGame()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(Track("town", AchievementKind.LargestTown, 1L, 2L, 3L));
            long cashBefore = world.CompanyCash;

            Achievements.AdvanceAll(world, catalog, _firstDate);

            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.IsBankrupt, Is.False);
        }

        [Test]
        public void AdvanceAll_PublishesOneEventPerMilestone()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(Track("income", AchievementKind.LifetimeIncome, 1L, 2L, 3L));
            var events = new EventBus();
            var received = new List<AchievementCompletedEvent>();
            events.Subscribe<AchievementCompletedEvent>(received.Add);
            world.CreditCompanyCash(2L, FinanceCategory.DeliveryIncome);

            Achievements.AdvanceAll(world, catalog, _firstDate, events);
            Achievements.AdvanceAll(world, catalog, _laterDate, events);
            EventDispatchResult result = events.DispatchPending();

            Assert.That(result.EventsProcessed, Is.EqualTo(2));
            Assert.That(received[0].MilestoneId, Is.EqualTo("income.bronze"));
            Assert.That(received[0].TrackId, Is.EqualTo("income"));
            Assert.That(received[0].Tier, Is.EqualTo(AchievementTier.Bronze));
            Assert.That(received[0].NameKey, Is.EqualTo("income.n0"));
            Assert.That(received[1].Tier, Is.EqualTo(AchievementTier.Silver));
            Assert.That(received[1].Date, Is.EqualTo(_firstDate));
        }

        [Test]
        public void AdvanceAll_CompletedIdMissingFromContent_IsKeptAndNotCounted()
        {
            GeneratedWorld world = CreateWorld();
            Achievements.AdvanceAll(world, CreateCatalog(Track("old", AchievementKind.LargestTown, 1L, 2L, 3L)), _firstDate);
            ContentCatalog newer = CreateCatalog(
                Track("global", AchievementKind.AchievementsUnlocked, 50L, 75L, 100L),
                Track("income", AchievementKind.LifetimeIncome, 1L, 2L, 3L));

            int completed = Achievements.AdvanceAll(world, newer, _laterDate);

            Assert.That(completed, Is.Zero);
            Assert.That(world.IsAchievementCompleted("old.bronze"), Is.True);
            Assert.That(Achievements.GetProgress(world, newer, newer.Achievements[0]), Is.Zero);
        }

        [Test]
        public void Capture_RoundTripsCompletionsCountersAndStatisticsThroughSnapshot()
        {
            GeneratedWorld world = CreateWorld();
            Achievements.AdvanceAll(world, CreateCatalog(Track("town", AchievementKind.LargestTown, 1L, 2L, 3L)), _firstDate);
            world.RecordCargoDelivered("wood", DeliveryDestinationKind.Industry, 7);
            world.CreditCompanyCash(30L, FinanceCategory.ContractBonus);
            world.Statistics.AddContractCompleted();
            world.Statistics.AddRoadTiles(5L);
            world.Statistics.AddRailTiles(6L);

            SaveSnapshot snapshot = SaveSnapshot.Capture(46001UL, new Tick(5), world);
            CompanyStatistics statistics = snapshot.CompanyStatistics;

            Assert.That(snapshot.HasAchievementProgress, Is.True);
            Assert.That(snapshot.HasCompanyStatistics, Is.True);
            CollectionAssert.AreEqual(world.AchievementCompletions, snapshot.AchievementCompletions);
            CollectionAssert.AreEqual(world.CargoDeliveryCounters, snapshot.CargoDeliveryCounters);
            Assert.That(statistics.LifetimeIncome, Is.EqualTo(30L));
            Assert.That(statistics.ContractsCompleted, Is.EqualTo(1L));
            Assert.That(statistics.RoadTilesTravelled, Is.EqualTo(5L));
            Assert.That(statistics.RailTilesTravelled, Is.EqualTo(6L));
        }

        [Test]
        public void IdenticalRuns_ProduceIdenticalAchievementState()
        {
            AchievementState first = RunScenario();
            AchievementState second = RunScenario();

            CollectionAssert.AreEqual(first.Completions, second.Completions);
            CollectionAssert.AreEqual(first.Counters, second.Counters);
            Assert.That(first.Completions, Is.Not.Empty);
        }

        [Test]
        public void StateHash_IncludesCompletionsAndCountersAndIsStableAcrossRuns()
        {
            string first = HashState(RunScenario());
            string second = HashState(RunScenario());
            GeneratedWorld changed = CreateWorld();
            changed.RecordCargoDelivered("bread", DeliveryDestinationKind.Town, 1);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(HashState(new AchievementState(changed.AchievementCompletions, changed.CargoDeliveryCounters)), Is.Not.EqualTo(first));
        }

        private static long Progress(GeneratedWorld world, ContentCatalog catalog, AchievementKind kind)
        {
            return Achievements.GetProgress(world, catalog, Track("t", kind, 1L, 2L, 3L));
        }

        private static string HashState(AchievementState state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)1);
                writer.Write(state.Completions.Count);
                for (int index = 0; index < state.Completions.Count; index++)
                {
                    writer.Write(state.Completions[index].AchievementId);
                    writer.Write(state.Completions[index].Date.Year);
                    writer.Write(state.Completions[index].Date.Month);
                    writer.Write(state.Completions[index].Date.Day);
                }

                writer.Write(state.Counters.Count);
                for (int index = 0; index < state.Counters.Count; index++)
                {
                    writer.Write(state.Counters[index].CargoId);
                    writer.Write((int)state.Counters[index].DestinationKind);
                    writer.Write(state.Counters[index].Amount);
                }

                writer.Flush();
                return DeterministicStateHasher.ToHexString(DeterministicStateHasher.ComputeSha256(stream.ToArray()));
            }
        }

        private static AchievementState RunScenario()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                Track("global", AchievementKind.AchievementsUnlocked, 20L, 50L, 100L),
                Track("town", AchievementKind.LargestTown, 1L, 2L, 3L),
                Track("income", AchievementKind.LifetimeIncome, 2L, 4L, 5L),
                CargoTrack("bread_sold", "bread", 2L, 4L, 6L));
            for (int step = 0; step < 6; step++)
            {
                world.CreditCompanyCash(1L, FinanceCategory.DeliveryIncome);
                world.RecordCargoDelivered("bread", step % 2 == 0 ? DeliveryDestinationKind.Town : DeliveryDestinationKind.Industry, 1);
                Achievements.AdvanceAll(world, catalog, new GameDate(1900, 1, step + 1));
            }

            return new AchievementState(world.AchievementCompletions, world.CargoDeliveryCounters);
        }

        private static int LargestPopulation(GeneratedWorld world)
        {
            int largest = 0;
            for (int index = 0; index < world.Towns.Count; index++)
            {
                largest = Math.Max(largest, world.Towns[index].Population);
            }

            return largest;
        }

        private static AchievementDefinition Track(
            string id,
            AchievementKind kind,
            long bronze,
            long silver,
            long gold,
            bool scales = false)
        {
            return new AchievementDefinition(id, kind, Milestones(id, bronze, silver, gold), "label." + id, scalesWithMapSize: scales);
        }

        private static AchievementDefinition CargoTrack(string id, string cargoId, long bronze, long silver, long gold)
        {
            return new AchievementDefinition(
                id,
                AchievementKind.CargoSold,
                Milestones(id, bronze, silver, gold),
                cargoId: cargoId);
        }

        private static AchievementMilestone[] Milestones(string id, long bronze, long silver, long gold)
        {
            return new[]
            {
                new AchievementMilestone(bronze, id + ".n0"),
                new AchievementMilestone(silver, id + ".n1"),
                new AchievementMilestone(gold, id + ".n2")
            };
        }

        private static ContentCatalog CreateCatalog(params AchievementDefinition[] achievements)
        {
            return CreateCatalog(null, achievements);
        }

        private static ContentCatalog CreateCatalog(AchievementGoalScale? scale, params AchievementDefinition[] achievements)
        {
            return new ContentCatalog(
                new[]
                {
                    new CargoDefinition("bread", "cargo.bread", CargoClass.Cargo, 24, 3),
                    new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1)
                },
                Array.Empty<IndustryDefinition>(),
                achievements: achievements,
                achievementGoalScale: scale);
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 46001UL);
        }

        private static TileCoordinate FindEmptyTile(GeneratedWorld world, TileCoordinate anchor)
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
                var candidate = new TileCoordinate(anchor.X + offsets[index].X, anchor.Y + offsets[index].Y);
                if (world.Map.Contains(candidate) &&
                    world.Map.GetObjectId(candidate) == TileMap.NoObjectId &&
                    world.Map.GetRoadTypeId(candidate) == TileMap.NoRoadTypeId &&
                    world.Map.GetRailTypeId(candidate) == TileMap.NoRailTypeId &&
                    world.Map.GetStationId(candidate) == TileMap.NoStationId &&
                    world.Map.GetRailStationId(candidate) == TileMap.NoRailStationId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }

        private sealed class AchievementState
        {
            public AchievementState(
                IReadOnlyList<AchievementCompletion> completions,
                IReadOnlyList<CargoDeliveryCounter> counters)
            {
                Completions = completions;
                Counters = counters;
            }

            public IReadOnlyList<AchievementCompletion> Completions { get; }

            public IReadOnlyList<CargoDeliveryCounter> Counters { get; }
        }
    }
}
