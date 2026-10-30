#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class WorldSaveRestoreTests
    {
        private const int _ticksBeforeSave = 7000;
        private const int _ticksAfterSave = 4000;
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly TimeSpan _tickDuration = TimeSpan.FromMilliseconds(50);

        [Test]
        public void Restore_RunningWorld_ReproducesCapturedState()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld world = CreateRunningWorld(catalog, calendar, out Tick tick);

            SaveSnapshot snapshot = SaveSnapshot.Capture(world.MapSeed, tick, world);
            GeneratedWorld restored = WorldSave.Restore(snapshot, catalog, calendar);

            Assert.That(calendar.GetDate(tick).Month, Is.GreaterThan(1));
            Assert.That(world.GetCargoDelivered("wood", DeliveryDestinationKind.Industry), Is.GreaterThan(0L));
            Assert.That(world.RoadVehicles.Count, Is.EqualTo(1));
            Assert.That(world.CompanyCash, Is.Not.EqualTo(GeneratedWorld.PlaceholderStartingCash));
            Assert.That(world.RoadVehicles[0].CargoOriginTile.HasValue, Is.True);
            Assert.That(world.Statistics.RoadTilesTravelled, Is.GreaterThan(0L));
            Assert.That(world.Statistics.LifetimeIncome, Is.GreaterThan(0L));
            Assert.That(FindIndustry(world, "forest").ProductionLevel, Is.GreaterThan(0));
            int forestIndex = IndexOfIndustry(world, FindIndustry(world, "forest"));
            Assert.That(snapshot.WithFormatVersion(23).Industries[forestIndex].ProductionLevel, Is.Zero);
            Assert.That(Dump(restored, calendar, tick), Is.EqualTo(Dump(world, calendar, tick)));
        }

        [Test]
        public void Restore_ThenSimulate_MatchesTheUninterruptedRun()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld uninterrupted = CreateRunningWorld(catalog, calendar, out Tick saveTick);
            GeneratedWorld restored = WorldSave.Restore(
                SaveSnapshot.Capture(uninterrupted.MapSeed, saveTick, uninterrupted),
                catalog,
                calendar);

            var eventsA = new EventBus();
            var eventsB = new EventBus();
            Tick tick = saveTick;
            for (int step = 0; step < _ticksAfterSave; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(uninterrupted, catalog, calendar, tick, eventsA);
                WorldSimulationStep.Advance(restored, catalog, calendar, tick, eventsB);
                if (step % 500 == 0)
                {
                    Assert.That(
                        Dump(restored, calendar, tick),
                        Is.EqualTo(Dump(uninterrupted, calendar, tick)),
                        "diverged at step " + step);
                }
            }

            Assert.That(Dump(restored, calendar, tick), Is.EqualTo(Dump(uninterrupted, calendar, tick)));
        }

        [Test]
        public void Restore_DoesNotShareStateWithTheSnapshot()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld world = CreateRunningWorld(catalog, calendar, out Tick tick);
            SaveSnapshot snapshot = SaveSnapshot.Capture(world.MapSeed, tick, world);
            string before = Dump(WorldSave.Restore(snapshot, catalog, calendar), calendar, tick);

            GeneratedWorld second = WorldSave.Restore(snapshot, catalog, calendar);
            var events = new EventBus();
            Tick later = tick;
            for (int step = 0; step < 500; step++)
            {
                later = later.Next();
                WorldSimulationStep.Advance(second, catalog, calendar, later, events);
            }

            Assert.That(Dump(WorldSave.Restore(snapshot, catalog, calendar), calendar, tick), Is.EqualTo(before));
        }

        [Test]
        public void Restore_AtTickZero_LeavesTheContractMonthUnset()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld world = GenerateForestAndSawmillWorld(48014UL, catalog);

            GeneratedWorld restored = WorldSave.Restore(
                SaveSnapshot.Capture(world.MapSeed, Tick.Zero, world),
                catalog,
                calendar);

            Assert.That(restored.LastContractYear, Is.Zero);
            Assert.That(restored.LastContractMonth, Is.Zero);
        }

        [Test]
        public void Restore_InputsStockedByAnOlderSave_AreDeliveredIntoOutput()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld world = GenerateForestAndSawmillWorld(48015UL, catalog);
            // Before format 23 delivered wood waited in the sawmill stock (Issue #193).
            FindIndustry(world, "sawmill").Cargo.Add("wood", 5);

            GeneratedWorld restored = WorldSave.Restore(
                SaveSnapshot.Capture(world.MapSeed, Tick.Zero, world),
                catalog,
                calendar);

            PlacedIndustry sawmill = FindIndustry(restored, "sawmill");
            Assert.That(sawmill.Cargo.GetAmount("wood"), Is.Zero);
            Assert.That(sawmill.Cargo.GetAmount("planks"), Is.EqualTo(2));
            Assert.That(sawmill.InputLeftover.GetAmount("wood"), Is.EqualTo(1));
        }

        [Test]
        public void Restore_OlderFormatVersion_Throws()
        {
            ContentCatalog catalog = CreateWoodChainCatalog();
            var calendar = new SimulationCalendar(_tickDuration);
            GeneratedWorld world = GenerateForestAndSawmillWorld(48014UL, catalog);
            SaveSnapshot older = SaveSnapshot.Capture(world.MapSeed, Tick.Zero, world).WithFormatVersion(21);

            Assert.Throws<ArgumentException>(() => WorldSave.Restore(older, catalog, calendar));
        }

        private static GeneratedWorld CreateRunningWorld(
            ContentCatalog catalog,
            SimulationCalendar calendar,
            out Tick tick)
        {
            GeneratedWorld world = GenerateForestAndSawmillWorld(48014UL, catalog);
            RoadStation pickup = BuildAdjacentStation(world, FindIndustry(world, "forest").Coordinate);
            RoadStation consumer = BuildAdjacentStation(world, FindIndustry(world, "sawmill").Coordinate);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ConnectStationsWithRoads(world, processor, pickup.Coordinate, consumer.Coordinate, 10);
            Assert.That(processor.Process(new Tick(1), new BuyRoadVehicleCommand(pickup.Id)).IsAccepted, Is.True);
            Assert.That(
                processor.Process(
                    new Tick(2),
                    new SetRoadVehicleOrdersCommand(
                        0,
                        new[]
                        {
                            new RoadVehicleOrder(consumer.Id, waitForFull: false),
                            new RoadVehicleOrder(pickup.Id, waitForFull: false)
                        },
                        catalog)).IsAccepted,
                Is.True);
            world.OfferContract(
                "wood",
                DeliveryDestinationKind.Industry,
                IndexOfIndustry(world, FindIndustry(world, "sawmill")),
                50,
                new GameDate(1900, 1, 1),
                new GameDate(1902, 1, 1));

            var events = new EventBus();
            tick = Tick.Zero;
            for (int step = 0; step < _ticksBeforeSave; step++)
            {
                tick = tick.Next();
                WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
            }

            return world;
        }

        private static string Dump(GeneratedWorld world, SimulationCalendar calendar, Tick tick)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var text = new StringBuilder();
            text.Append("date=").Append(calendar.GetDate(tick)).Append('\n');
            text.Append("cash=").Append(world.CompanyCash.ToString(c)).Append('\n');
            text.Append("upkeep=").Append(world.LastUpkeepYear).Append('/').Append(world.LastUpkeepMonth).Append('\n');
            text.Append("contractMonth=").Append(world.LastContractYear).Append('/')
                .Append(world.LastContractMonth).Append('\n');
            text.Append("finance=").Append(world.Finance.Year);
#pragma warning disable CA2263 // Unity's Core API cannot rely on the generic GetValues.
            foreach (FinanceCategory category in Enum.GetValues(typeof(FinanceCategory)))
#pragma warning restore CA2263
            {
                text.Append(' ').Append(world.Finance.Current.Get(category).ToString(c));
                text.Append('/').Append(world.Finance.Previous.Get(category).ToString(c));
            }

            text.Append('\n');
            text.Append("tiles=");
            AppendChannel(text, world.Map.TerrainTypeIds);
            AppendChannel(text, world.Map.ObjectIds);
            AppendChannel(text, world.Map.OwnerIds);
            AppendChannel(text, world.Map.RoadTypeIds);
            AppendChannel(text, world.Map.RailTypeIds);
            AppendChannel(text, world.Map.StationIds);
            AppendChannel(text, world.Map.RailStationIds);
            text.Append('\n');
            for (int index = 0; index < world.RoadStations.Count; index++)
            {
                text.Append("rs ").Append(world.RoadStations[index].Id).Append(' ')
                    .Append(world.RoadStations[index].Coordinate).Append('\n');
            }

            for (int index = 0; index < world.RoadVehicles.Count; index++)
            {
                RoadVehicle v = world.RoadVehicles[index];
                text.Append("rv ").Append(v.Id).Append(' ').Append(v.TypeId).Append(' ').Append(v.OrderIndex)
                    .Append(' ').Append(v.IsStuck).Append(' ').Append(v.IsOnEdge).Append(' ').Append(v.EdgeFromNodeId)
                    .Append(' ').Append(v.EdgeToNodeId).Append(' ').Append(v.ProgressAlongEdge)
                    .Append(' ').Append(v.IsServicingStop).Append(' ').Append(v.IsUnloadComplete)
                    .Append(' ').Append(v.LifetimeDeliveryIncome.ToString(c)).Append(' ').Append(v.CargoCapacity)
                    .Append(' ').Append(v.PassengerCargoOriginTownId)
                    .Append(" origin ").Append(v.CargoOriginTile.HasValue ? v.CargoOriginTile.Value.X.ToString(c) + "," + v.CargoOriginTile.Value.Y.ToString(c) : "none")
                    .Append(' ').Append(v.DeliveryPayRemainder);
                for (int o = 0; o < v.Orders.Count; o++)
                {
                    text.Append(" o").Append(v.Orders[o].StationId).Append(v.Orders[o].WaitForFull);
                }

                AppendCargo(text, v.Cargo.Stacks);
                text.Append('\n');
            }

            for (int index = 0; index < world.Towns.Count; index++)
            {
                Town t = world.Towns[index];
                text.Append("t ").Append(t.Id).Append(' ').Append(t.Population).Append(' ').Append(t.StatsYear)
                    .Append('/').Append(t.StatsMonth).Append(' ').Append(t.LoadRoundRobinIndex).Append(' ')
                    .Append(t.MonthPassengersGenerated.ToString(c)).Append(' ')
                    .Append(t.MonthPassengersTransported.ToString(c)).Append(' ')
                    .Append(t.MonthMailGenerated.ToString(c)).Append(' ')
                    .Append(t.MonthMailTransported.ToString(c)).Append(" '").Append(t.Name).Append("' plan");
                for (int level = 1; level <= TownNeedPlan.MaxLevel; level++)
                {
                    text.Append(' ').Append(string.Join(",", t.NeedPlan.GetLevel(level)));
                }

                for (int n = 0; n < t.Needs.Count; n++)
                {
                    text.Append(' ').Append(t.Needs[n].CargoId).Append(':').Append(t.Needs[n].MonthSupply);
                }

                AppendCargo(text, t.WaitingCargo.Stacks);
                text.Append('\n');
            }

            for (int index = 0; index < world.Industries.Count; index++)
            {
                PlacedIndustry i = world.Industries[index];
                text.Append("i ").Append(i.IndustryId).Append(' ').Append(i.InstanceIndex).Append(' ')
                    .Append(i.MonthProduced.ToString(c)).Append(' ').Append(i.MonthTransported.ToString(c))
                    .Append(' ').Append(i.StatsYear).Append('/').Append(i.StatsMonth).Append(' ')
                    .Append(i.LoadRoundRobinIndex).Append(" level ").Append(i.ProductionLevel).Append('/')
                    .Append(i.ServedMonths);
                AppendCargo(text, i.Cargo.Stacks);
                text.Append(" leftover");
                AppendCargo(text, i.InputLeftover.Stacks);
                text.Append('\n');
            }

            text.Append("unlocks=").Append(string.Join(",", world.UnlockedVehicleTypeIds)).Append('\n');
            for (int index = 0; index < world.AchievementCompletions.Count; index++)
            {
                text.Append("a ").Append(world.AchievementCompletions[index].AchievementId).Append(' ')
                    .Append(world.AchievementCompletions[index].Date).Append('\n');
            }

            for (int index = 0; index < world.CargoDeliveryCounters.Count; index++)
            {
                CargoDeliveryCounter k = world.CargoDeliveryCounters[index];
                text.Append("d ").Append(k.CargoId).Append(' ').Append(k.DestinationKind).Append(' ')
                    .Append(k.Amount.ToString(c)).Append('\n');
            }

            text.Append("contracts next=").Append(world.NextContractId).Append(" rng=")
                .Append(world.ContractRandomState.State.ToString(c)).Append('/')
                .Append(world.ContractRandomState.Increment.ToString(c)).Append('\n');
            for (int index = 0; index < world.OpenContracts.Count; index++)
            {
                Contract k = world.OpenContracts[index];
                text.Append("c ").Append(k.Id).Append(' ').Append(k.CargoId).Append(' ').Append(k.TargetKind)
                    .Append(' ').Append(k.TargetIndex).Append(' ').Append(k.Amount.ToString(c)).Append(' ')
                    .Append(k.Delivered.ToString(c)).Append(' ').Append(k.StartDate).Append(' ')
                    .Append(k.Deadline).Append('\n');
            }

            return text.ToString();
        }

        private static void AppendChannel(StringBuilder text, ReadOnlySpan<int> channel)
        {
            long hash = 17;
            for (int index = 0; index < channel.Length; index++)
            {
                hash = unchecked((hash * 31) + channel[index]);
            }

            text.Append(hash.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        private static void AppendCargo(StringBuilder text, IReadOnlyList<CargoAmount> stacks)
        {
            for (int index = 0; index < stacks.Count; index++)
            {
                text.Append(" [").Append(stacks[index].CargoId).Append('x').Append(stacks[index].Amount).Append(']');
            }
        }

        private static ContentCatalog CreateWoodChainCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 14, 2)
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
            // Distance pay and fast primary growth, so the running world carries load origins, pay fractions and levels.
            var economy = new EconomySettings(
                GeneratedWorld.PlaceholderStartingCash,
                new DeliveryPaySettings(30, 4),
                new IndustryGrowthSettings(1, 1, 3, 50));
            return new ContentCatalog(cargo, industries, townNeeds, economy);
        }

        private static GeneratedWorld GenerateForestAndSawmillWorld(ulong seed, ContentCatalog catalog)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }

        private static int IndexOfIndustry(GeneratedWorld world, PlacedIndustry industry)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (ReferenceEquals(world.Industries[index], industry))
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Industry is not part of the world.");
        }

        private static PlacedIndustry FindIndustry(GeneratedWorld world, string industryId)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (string.Equals(world.Industries[index].IndustryId, industryId, StringComparison.Ordinal))
                {
                    return world.Industries[index];
                }
            }

            throw new InvalidOperationException($"Industry '{industryId}' was not placed.");
        }

        private static int _stationBuildTick = 1;

        private static RoadStation BuildAdjacentStation(GeneratedWorld world, TileCoordinate anchor)
        {
            TileCoordinate stationCoordinate = FindEmptyOrthogonalNeighbor(world, anchor);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            Assert.That(
                processor.Process(new Tick(_stationBuildTick++), new BuildRoadStationCommand(stationCoordinate))
                    .IsAccepted,
                Is.True);
            Assert.That(world.TryGetRoadStationAt(stationCoordinate, out RoadStation? built), Is.True);
            return built!;
        }

        private static TileCoordinate FindEmptyOrthogonalNeighbor(GeneratedWorld world, TileCoordinate coordinate)
        {
            var offsets = new[]
            {
                new TileCoordinate(0, 1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, -1),
                new TileCoordinate(-1, 0)
            };
            for (int index = 0; index < offsets.Length; index++)
            {
                var neighbor = new TileCoordinate(
                    coordinate.X + offsets[index].X,
                    coordinate.Y + offsets[index].Y);
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                if (world.Map.GetObjectId(neighbor) == TileMap.NoObjectId &&
                    world.Map.GetRoadTypeId(neighbor) == TileMap.NoRoadTypeId &&
                    world.Map.GetStationId(neighbor) == TileMap.NoStationId)
                {
                    return neighbor;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }

        private static void ConnectStationsWithRoads(
            GeneratedWorld world,
            SimulationCommandProcessor<GeneratedWorld> processor,
            TileCoordinate fromStation,
            TileCoordinate toStation,
            int startingTick)
        {
            var cameFrom = new System.Collections.Generic.Dictionary<int, int>();
            var queue = new System.Collections.Generic.Queue<TileCoordinate>();
            queue.Enqueue(fromStation);
            cameFrom[world.Map.GetTileId(fromStation)] = -1;
            bool found = false;
            var offsets = new[]
            {
                new TileCoordinate(0, -1),
                new TileCoordinate(1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(-1, 0)
            };
            while (queue.Count > 0)
            {
                TileCoordinate current = queue.Dequeue();
                if (current.Equals(toStation))
                {
                    found = true;
                    break;
                }

                for (int index = 0; index < offsets.Length; index++)
                {
                    var next = new TileCoordinate(current.X + offsets[index].X, current.Y + offsets[index].Y);
                    if (!world.Map.Contains(next))
                    {
                        continue;
                    }

                    int nextId = world.Map.GetTileId(next);
                    if (cameFrom.ContainsKey(nextId))
                    {
                        continue;
                    }

                    if (world.Map.GetObjectId(next) != TileMap.NoObjectId && !next.Equals(toStation))
                    {
                        continue;
                    }

                    if (world.Map.GetStationId(next) != TileMap.NoStationId && !next.Equals(toStation))
                    {
                        continue;
                    }

                    cameFrom[nextId] = world.Map.GetTileId(current);
                    queue.Enqueue(next);
                }
            }

            Assert.That(found, Is.True, "Could not path between stations to pave roads.");
            var path = new System.Collections.Generic.List<TileCoordinate>();
            int cursor = world.Map.GetTileId(toStation);
            while (cursor >= 0)
            {
                path.Add(world.Map.GetCoordinate(cursor));
                if (!cameFrom.TryGetValue(cursor, out int previous))
                {
                    break;
                }

                cursor = previous;
            }

            path.Reverse();
            int tick = startingTick;
            for (int index = 0; index < path.Count; index++)
            {
                TileCoordinate step = path[index];
                if (step.Equals(fromStation) || step.Equals(toStation))
                {
                    continue;
                }

                if (world.Map.GetRoadTypeId(step) != TileMap.NoRoadTypeId)
                {
                    continue;
                }

                Assert.That(processor.Process(new Tick(tick++), new BuildRoadCommand(step)).IsAccepted, Is.True);
            }
        }
    }
}
