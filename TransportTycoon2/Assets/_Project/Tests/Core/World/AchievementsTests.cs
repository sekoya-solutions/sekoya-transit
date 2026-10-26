#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.Simulation.Commands;
using TransportTycoon.Core.Tests.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    [TestFixture]
    public sealed class AchievementsTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };
        private static readonly GameDate _firstDate = new GameDate(1901, 2, 3);
        private static readonly GameDate _laterDate = new GameDate(1905, 7, 9);

        [Test]
        public void TownPopulation_CompletesAtGoalAndNotBefore()
        {
            GeneratedWorld world = CreateWorld();
            int largest = LargestPopulation(world);
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("at_goal", "a.at_goal", AchievementKind.TownPopulation, largest),
                new AchievementDefinition("above_goal", "a.above_goal", AchievementKind.TownPopulation, largest + 1L));

            int completed = Achievements.AdvanceAll(world, catalog, _firstDate);

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("at_goal"), Is.True);
            Assert.That(world.IsAchievementCompleted("above_goal"), Is.False);
        }

        [Test]
        public void CompanyCash_CompletesAtGoalAndNotBefore()
        {
            GeneratedWorld world = CreateWorld();
            long goal = world.CompanyCash + 100L;
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, goal));

            world.CreditCompanyCash(99L);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.Zero);

            world.CreditCompanyCash(1L);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("cash"), Is.True);
        }

        [Test]
        public void VehicleCount_CountsRoadVehiclesAndTrainsButNotWagons()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("three", "a.three", AchievementKind.VehicleCount, 3L),
                new AchievementDefinition("four", "a.four", AchievementKind.VehicleCount, 4L));
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate roadTile = FindEmptyTile(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(roadTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(roadTile, out RoadStation? roadStation), Is.True);
            TileCoordinate railTile = FindEmptyTile(world, world.TownAnchors[1]);
            Assert.That(processor.Process(new Tick(2), new BuildRailStationCommand(railTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(railTile, out RailStation? railStation), Is.True);

            Assert.That(processor.Process(new Tick(3), new BuyRoadVehicleCommand(roadStation!.Id, catalog)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(4), new BuyRoadVehicleCommand(roadStation.Id, catalog)).IsAccepted, Is.True);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.Zero);

            Assert.That(processor.Process(new Tick(5), new BuyRailVehicleCommand(railStation!.Id, catalog)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(6), new AddRailWagonCommand(0, catalog)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(7), new AddRailWagonCommand(0, catalog)).IsAccepted, Is.True);

            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("three"), Is.True);
            Assert.That(world.IsAchievementCompleted("four"), Is.False);
        }

        [Test]
        public void CargoDelivered_FiltersByDestinationKind()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("town", "a.town", AchievementKind.CargoDelivered, 5L, "bread", DeliveryDestinationKind.Town),
                new AchievementDefinition("industry", "a.industry", AchievementKind.CargoDelivered, 5L, "bread", DeliveryDestinationKind.Industry),
                new AchievementDefinition("any", "a.any", AchievementKind.CargoDelivered, 8L, "bread", DeliveryDestinationKind.Any),
                new AchievementDefinition("other", "a.other", AchievementKind.CargoDelivered, 1L, "wood", DeliveryDestinationKind.Any));

            world.RecordCargoDelivered("bread", DeliveryDestinationKind.Town, 4);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.Zero);

            world.RecordCargoDelivered("bread", DeliveryDestinationKind.Town, 1);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("town"), Is.True);

            world.RecordCargoDelivered("bread", DeliveryDestinationKind.Industry, 3);
            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("any"), Is.True);
            Assert.That(world.IsAchievementCompleted("industry"), Is.False);
            Assert.That(world.IsAchievementCompleted("other"), Is.False);
        }

        [Test]
        public void Completion_IsPermanentAndKeepsFirstDate()
        {
            GeneratedWorld world = CreateWorld();
            long goal = world.CompanyCash;
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, goal));
            Achievements.AdvanceAll(world, catalog, _firstDate);

            world.DebitCompanyCashClamped(world.CompanyCash - 1L);
            int completed = Achievements.AdvanceAll(world, catalog, _laterDate);

            Assert.That(completed, Is.Zero);
            Assert.That(world.IsAchievementCompleted("cash"), Is.True);
            Assert.That(world.AchievementCompletions, Has.Count.EqualTo(1));
            Assert.That(world.AchievementCompletions[0].Date, Is.EqualTo(_firstDate));
        }

        [Test]
        public void Completion_PaysNothingAndDoesNotEndGame()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, 1L));
            long cashBefore = world.CompanyCash;

            Achievements.AdvanceAll(world, catalog, _firstDate);

            Assert.That(world.CompanyCash, Is.EqualTo(cashBefore));
            Assert.That(world.IsBankrupt, Is.False);
        }

        [Test]
        public void AdvanceAll_PublishesOneEventPerCompletion()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, 1L));
            var events = new EventBus();
            var received = new List<AchievementCompletedEvent>();
            events.Subscribe<AchievementCompletedEvent>(received.Add);

            Achievements.AdvanceAll(world, catalog, _firstDate, events);
            Achievements.AdvanceAll(world, catalog, _laterDate, events);
            EventDispatchResult result = events.DispatchPending();

            Assert.That(result.EventsProcessed, Is.EqualTo(1));
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].AchievementId, Is.EqualTo("cash"));
            Assert.That(received[0].NameKey, Is.EqualTo("a.cash"));
            Assert.That(received[0].Date, Is.EqualTo(_firstDate));
        }

        [Test]
        public void AdvanceAll_CompletedIdMissingFromContent_IsKeptAndIgnored()
        {
            GeneratedWorld world = CreateWorld();
            Achievements.AdvanceAll(
                world,
                CreateCatalog(new AchievementDefinition("old", "a.old", AchievementKind.CompanyCash, 1L)),
                _firstDate);
            ContentCatalog newer = CreateCatalog(
                new AchievementDefinition("new", "a.new", AchievementKind.CompanyCash, 1L));

            int completed = Achievements.AdvanceAll(world, newer, _laterDate);

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(world.IsAchievementCompleted("old"), Is.True);
            Assert.That(world.AchievementCompletions, Has.Count.EqualTo(2));
        }

        [Test]
        public void AdvanceAll_AlreadyMetConditionCompletesOnFirstCheck()
        {
            GeneratedWorld world = CreateWorld();
            ContentCatalog catalog = CreateCatalog(
                new AchievementDefinition("town", "a.town", AchievementKind.TownPopulation, 1L));
            Assert.That(world.AchievementCompletions, Is.Empty);

            Assert.That(Achievements.AdvanceAll(world, catalog, _firstDate), Is.EqualTo(1));
        }

        [Test]
        public void Capture_RoundTripsCompletionsAndCountersThroughSnapshot()
        {
            GeneratedWorld world = CreateWorld();
            Achievements.AdvanceAll(
                world,
                CreateCatalog(new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, 1L)),
                _firstDate);
            world.RecordCargoDelivered("wood", DeliveryDestinationKind.Industry, 7);
            world.RecordCargoDelivered("bread", DeliveryDestinationKind.Town, 3);

            SaveSnapshot snapshot = SaveSnapshot.Capture(46001UL, new Tick(5), world);
            var rebuilt = new SaveSnapshot(
                snapshot.FormatVersion,
                snapshot.MapSeed,
                snapshot.CurrentTick,
                snapshot.MapSize,
                snapshot.TerrainTypeIds.ToArray(),
                snapshot.ObjectIds.ToArray(),
                snapshot.OwnerIds.ToArray(),
                snapshot.RoadTypeIds.ToArray(),
                snapshot.RailTypeIds.ToArray(),
                snapshot.TownAnchors,
                snapshot.Industries,
                snapshot.RoadStations,
                snapshot.RoadVehicles,
                snapshot.CompanyCash,
                snapshot.RailStations,
                snapshot.RailVehicles,
                snapshot.Towns,
                snapshot.LastUpkeepYear,
                snapshot.LastUpkeepMonth,
                snapshot.UnlockedVehicleTypeIds,
                snapshot.AchievementCompletions,
                snapshot.CargoDeliveryCounters);

            Assert.That(snapshot.HasAchievementProgress, Is.True);
            CollectionAssert.AreEqual(world.AchievementCompletions, rebuilt.AchievementCompletions);
            CollectionAssert.AreEqual(world.CargoDeliveryCounters, rebuilt.CargoDeliveryCounters);
            Assert.That(rebuilt.CargoDeliveryCounters[0].CargoId, Is.EqualTo("bread"));
            Assert.That(rebuilt.CargoDeliveryCounters[1].Amount, Is.EqualTo(7L));
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
                new AchievementDefinition("town", "a.town", AchievementKind.TownPopulation, 1L),
                new AchievementDefinition("cash", "a.cash", AchievementKind.CompanyCash, world.CompanyCash + 5L),
                new AchievementDefinition("bread", "a.bread", AchievementKind.CargoDelivered, 4L, "bread", DeliveryDestinationKind.Any));
            for (int step = 0; step < 6; step++)
            {
                world.CreditCompanyCash(1L);
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

        private static ContentCatalog CreateCatalog(params AchievementDefinition[] achievements)
        {
            return new ContentCatalog(
                new[]
                {
                    new CargoDefinition("bread", "cargo.bread", CargoClass.Cargo, 24, 3),
                    new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1)
                },
                Array.Empty<IndustryDefinition>(),
                achievements: achievements);
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
