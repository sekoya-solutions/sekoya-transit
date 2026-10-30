#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveFormatV20ToV21ContractsMigrationTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Migrate_BumpsToV21WithNoContractsFirstIdAndSeededGenerator()
        {
            SaveSnapshot v20 = CreateV20Snapshot(63001UL);

            SaveSnapshot migrated = new SaveFormatV20ToV21ContractsMigration().Migrate(v20);

            Assert.That(migrated.FormatVersion, Is.EqualTo(21));
            Assert.That(migrated.HasContracts, Is.True);
            Assert.That(migrated.ContractList, Is.Empty);
            Assert.That(migrated.NextContractId, Is.EqualTo(GeneratedWorld.FirstContractId));
            Pcg32State expected = GeneratedWorld.CreateContractRandom(63001UL).CapturedState;
            Assert.That(migrated.ContractRandomState.State, Is.EqualTo(expected.State));
            Assert.That(migrated.ContractRandomState.Increment, Is.EqualTo(expected.Increment));
        }

        [Test]
        public void Pipeline_MigratesV20ToCurrentVersion()
        {
            SaveSnapshot current = CurrentSaveMigrations.CreatePipeline().Migrate(CreateV20Snapshot(63002UL));

            Assert.That(current.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentFormatVersion));
            Assert.That(current.ContractList, Is.Empty);
        }

        [Test]
        public void Migrate_KeepsAchievementProgress()
        {
            SaveSnapshot v20 = CreateV20Snapshot(63003UL);

            SaveSnapshot migrated = new SaveFormatV20ToV21ContractsMigration().Migrate(v20);

            CollectionAssert.AreEqual(v20.AchievementCompletions, migrated.AchievementCompletions);
            CollectionAssert.AreEqual(v20.CargoDeliveryCounters, migrated.CargoDeliveryCounters);
        }

        [Test]
        public void Migrate_WrongVersion_Throws()
        {
            SaveSnapshot current = SaveSnapshot.Capture(63004UL, Tick.Zero, CreateWorld(63004UL));

            Assert.Throws<SaveFormatException>(() => new SaveFormatV20ToV21ContractsMigration().Migrate(current));
        }

        [Test]
        public void Constructor_BelowV21WithContractState_Throws()
        {
            SaveSnapshot v20 = CreateV20Snapshot(63005UL);
            Contract contract = MakeContract(1, "wood", 0);

            Assert.Throws<ArgumentException>(() => Rebuild(v20, 20, new[] { contract }, 2, null));
            Assert.Throws<ArgumentException>(() => Rebuild(v20, 20, null, 5, null));
            Assert.Throws<ArgumentException>(
                () => Rebuild(v20, 20, null, 0, GeneratedWorld.CreateContractRandom(1UL).CapturedState));
        }

        [Test]
        public void Constructor_BadContractLists_Throw()
        {
            SaveSnapshot v21 = CreateV20Snapshot(63006UL).WithFormatVersion(21);
            Contract first = MakeContract(2, "wood", 0);
            Contract second = MakeContract(3, "wood", 1);

            // Ids must ascend, stay below the next id, and not repeat a (cargo, target).
            Assert.Throws<ArgumentException>(() => Rebuild(v21, 21, new[] { second, first }, 10, null));
            Assert.Throws<ArgumentException>(() => Rebuild(v21, 21, new[] { first, second }, 3, null));
            Assert.Throws<ArgumentException>(
                () => Rebuild(v21, 21, new[] { first, MakeContract(3, "wood", 0) }, 10, null));
            Assert.DoesNotThrow(() => Rebuild(v21, 21, new[] { first, second }, 10, null));
        }

        [Test]
        public void Snapshot_CopiesContractsSoLiveProgressDoesNotLeak()
        {
            GeneratedWorld world = CreateWorld(63007UL);
            Contract live = world.OfferContract("wood", 0, 50, new GameDate(1900, 1, 1));
            Contracts.Accept(
                world,
                new ContentCatalog(Array.Empty<CargoDefinition>(), Array.Empty<IndustryDefinition>()),
                live.Id,
                new GameDate(1900, 1, 1));

            SaveSnapshot snapshot = SaveSnapshot.Capture(63007UL, Tick.Zero, world);
            world.RecordContractDelivery(FindTownNeighbor(world), "wood", 5);

            Assert.That(live.Delivered, Is.EqualTo(5L));
            Assert.That(snapshot.ContractList[0].Delivered, Is.Zero);
            Assert.That(snapshot.NextContractId, Is.EqualTo(2));
        }

        private static Contract MakeContract(int id, string cargoId, int townIndex)
        {
            return new Contract(
                id,
                cargoId,
                townIndex,
                50,
                0L,
                ContractStatus.Proposal,
                new GameDate(1900, 1, 1),
                new GameDate(1900, 1, 1),
                new GameDate(1900, 1, 1));
        }

        private static TileCoordinate FindTownNeighbor(GeneratedWorld world)
        {
            TileCoordinate anchor = world.TownAnchors[0];
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
                if (world.Map.Contains(candidate) && world.Map.GetObjectId(candidate) == TileMap.NoObjectId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty neighbor next to the town anchor.");
        }

        private static SaveSnapshot Rebuild(
            SaveSnapshot source,
            int formatVersion,
            System.Collections.Generic.IReadOnlyList<Contract>? contracts,
            int nextContractId,
            Pcg32State? randomState)
        {
            return new SaveSnapshot(
                formatVersion,
                source.MapSeed,
                source.CurrentTick,
                source.MapSize,
                source.TerrainTypeIds.ToArray(),
                source.ObjectIds.ToArray(),
                source.OwnerIds.ToArray(),
                source.RoadTypeIds.ToArray(),
                source.RailTypeIds.ToArray(),
                source.TownAnchors,
                source.Industries,
                source.RoadStations,
                source.RoadVehicles,
                source.CompanyCash,
                source.RailStations,
                source.RailVehicles,
                source.Towns,
                unlockedVehicleTypeIds: source.UnlockedVehicleTypeIds,
                contractList: contracts,
                nextContractId: nextContractId,
                contractRandomState: randomState);
        }

        private static GeneratedWorld CreateWorld(ulong seed)
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, seed);
        }

        private static SaveSnapshot CreateV20Snapshot(ulong seed)
        {
            GeneratedWorld world = CreateWorld(seed);
            return new SaveSnapshot(
                20,
                seed,
                Tick.Zero,
                MapSize.Small,
                world.Map.TerrainTypeIds.ToArray(),
                world.Map.ObjectIds.ToArray(),
                world.Map.OwnerIds.ToArray(),
                world.Map.RoadTypeIds.ToArray(),
                world.Map.RailTypeIds.ToArray(),
                world.TownAnchors,
                world.Industries,
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                GeneratedWorld.PlaceholderStartingCash,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>(),
                world.Towns,
                unlockedVehicleTypeIds: Array.Empty<string>(),
                achievementCompletions: new[]
                {
                    new AchievementCompletion("first", new GameDate(1900, 3, 4))
                },
                cargoDeliveryCounters: new[]
                {
                    new CargoDeliveryCounter("wood", SekoyaTransit.Core.Content.DeliveryDestinationKind.Industry, 9L)
                });
        }
    }
}
