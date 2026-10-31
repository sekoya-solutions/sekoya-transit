#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class FinanceLedgerTests
    {
        private const string _truckMk2Id = "truck_mk2";
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public void Credit_RecordsIncomeUnderItsCategory()
        {
            GeneratedWorld world = CreateWorld();

            world.CreditCompanyCash(300L, FinanceCategory.DeliveryIncome);
            world.CreditCompanyCash(50L, FinanceCategory.ContractBonus);
            world.CreditCompanyCash(25L, FinanceCategory.DeliveryIncome);

            Assert.That(world.Finance.Current.Get(FinanceCategory.DeliveryIncome), Is.EqualTo(325L));
            Assert.That(world.Finance.Current.Get(FinanceCategory.ContractBonus), Is.EqualTo(50L));
            Assert.That(world.Finance.Current.TotalIncome, Is.EqualTo(375L));
            Assert.That(world.Finance.Current.TotalSpending, Is.Zero);
        }

        [Test]
        public void Debit_RecordsSpendingUnderItsCategory()
        {
            GeneratedWorld world = CreateWorld();

            Assert.That(world.TryDebitCompanyCash(400L, FinanceCategory.VehiclePurchases), Is.True);
            Assert.That(world.TryDebitCompanyCash(100L, FinanceCategory.VehicleUnlocks), Is.True);

            Assert.That(world.Finance.Current.Get(FinanceCategory.VehiclePurchases), Is.EqualTo(400L));
            Assert.That(world.Finance.Current.Get(FinanceCategory.VehicleUnlocks), Is.EqualTo(100L));
            Assert.That(world.Finance.Current.TotalSpending, Is.EqualTo(500L));
            Assert.That(world.Finance.Current.Net, Is.EqualTo(-500L));
        }

        [Test]
        public void Debit_RejectedPurchase_RecordsNothing()
        {
            GeneratedWorld world = CreateWorld();

            bool debited = world.TryDebitCompanyCash(world.CompanyCash + 1L, FinanceCategory.VehiclePurchases);

            Assert.That(debited, Is.False);
            Assert.That(world.Finance.Current.IsEmpty, Is.True);
        }

        [Test]
        public void ClampedDebit_RecordsTheAmountActuallyCharged()
        {
            GeneratedWorld world = CreateWorld();
            world.DebitCompanyCashClamped(world.CompanyCash - 50L, FinanceCategory.VehiclePurchases);

            long charged = world.DebitCompanyCashClamped(500L, FinanceCategory.RunningCosts);

            Assert.That(charged, Is.EqualTo(50L));
            Assert.That(world.Finance.Current.Get(FinanceCategory.RunningCosts), Is.EqualTo(50L));
        }

        [Test]
        public void CashChanges_WithTheWrongDirectionOfCategory_Throw()
        {
            GeneratedWorld world = CreateWorld();

            Assert.Throws<ArgumentException>(() => world.CreditCompanyCash(1L, FinanceCategory.RunningCosts));
            Assert.Throws<ArgumentException>(() => world.TryDebitCompanyCash(1L, FinanceCategory.DeliveryIncome));
            Assert.Throws<ArgumentException>(
                () => world.DebitCompanyCashClamped(1L, FinanceCategory.VehicleSales));
        }

        [Test]
        public void AdvanceTo_FirstObservation_OnlyRecordsTheYear()
        {
            var ledger = new FinanceLedger();

            bool rolled = ledger.AdvanceTo(1900);

            Assert.That(rolled, Is.False);
            Assert.That(ledger.Year, Is.EqualTo(1900));
        }

        [Test]
        public void AdvanceTo_NextYear_MovesCurrentToPreviousAndResetsCurrent()
        {
            GeneratedWorld world = CreateWorld();
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(700L, FinanceCategory.DeliveryIncome);
            world.TryDebitCompanyCash(200L, FinanceCategory.VehiclePurchases);

            bool rolled = world.Finance.AdvanceTo(1901);

            Assert.That(rolled, Is.True);
            Assert.That(world.Finance.Year, Is.EqualTo(1901));
            Assert.That(world.Finance.Previous.Get(FinanceCategory.DeliveryIncome), Is.EqualTo(700L));
            Assert.That(world.Finance.Previous.Get(FinanceCategory.VehiclePurchases), Is.EqualTo(200L));
            Assert.That(world.Finance.Current.IsEmpty, Is.True);
        }

        [Test]
        public void AdvanceTo_SameYear_KeepsTotals()
        {
            GeneratedWorld world = CreateWorld();
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(700L, FinanceCategory.DeliveryIncome);

            bool rolled = world.Finance.AdvanceTo(1900);

            Assert.That(rolled, Is.False);
            Assert.That(world.Finance.Current.Get(FinanceCategory.DeliveryIncome), Is.EqualTo(700L));
        }

        [Test]
        public void AdvanceTo_SkippedYear_ClearsPrevious()
        {
            GeneratedWorld world = CreateWorld();
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(700L, FinanceCategory.DeliveryIncome);

            world.Finance.AdvanceTo(1903);

            Assert.That(world.Finance.Previous.IsEmpty, Is.True);
            Assert.That(world.Finance.Current.IsEmpty, Is.True);
        }

        [Test]
        public void AdvanceTo_EarlierYear_Throws()
        {
            var ledger = new FinanceLedger();
            ledger.AdvanceTo(1900);

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.AdvanceTo(1899));
        }

        [Test]
        public void VehicleCommands_RecordPurchasesSalesAndUnlocks()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorldWithStations(out RoadStation roadStation, out RailStation railStation);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            RoadStation roadStation2 = AddRoadStation(world);
            RailStation railStation2 = AddRailStation(world);
            int truckId = RouteTestSupport.PlaceRoadVehicle(processor, world, catalog, roadStation.Id, roadStation2.Id);
            int trainId = RouteTestSupport.PlaceRailVehicle(processor, world, catalog, railStation.Id, railStation2.Id);
            Assert.That(processor.Process(new Tick(3), new AddRailWagonCommand(trainId)).IsAccepted, Is.True);
            // A train bought for a route comes with one wagon.
            long purchases = catalog.StartingRoadVehicle.Purchase
                + catalog.StartingRailLocomotive.Purchase
                + (2L * catalog.RailWagon.Purchase);
            Assert.That(world.Finance.Current.Get(FinanceCategory.VehiclePurchases), Is.EqualTo(purchases));

            long cashBeforeSales = world.CompanyCash;
            Assert.That(processor.Process(new Tick(4), new SellRoadVehicleCommand(truckId)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(5), new SellRailWagonCommand(trainId)).IsAccepted, Is.True);
            Assert.That(processor.Process(new Tick(6), new SellRailVehicleCommand(trainId)).IsAccepted, Is.True);
            Assert.That(
                world.Finance.Current.Get(FinanceCategory.VehicleSales),
                Is.EqualTo(world.CompanyCash - cashBeforeSales));
            Assert.That(world.Finance.Current.Get(FinanceCategory.VehicleSales), Is.GreaterThan(0L));
        }

        [Test]
        public void UnlockCommand_RecordsVehicleUnlocks()
        {
            ContentCatalog catalog = CreateCatalogWithMk2();
            GeneratedWorld world = CreateWorldWithStations(out _, out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            CommandProcessingResult result = processor.Process(
                new Tick(1),
                new UnlockVehicleTypeCommand(_truckMk2Id, calendarYear: 1910, catalog));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(world.Finance.Current.Get(FinanceCategory.VehicleUnlocks), Is.EqualTo(50000L));
        }

        [Test]
        public void Upkeep_RecordsRunningCostsActuallyCharged()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = CreateWorldWithStations(out RoadStation roadStation, out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            RoadStation secondStation = AddRoadStation(world);
            RouteTestSupport.PlaceRoadVehicle(processor, world, catalog, roadStation.Id, secondStation.Id);

            // Upkeep is charged day by day and a full month adds up to the monthly upkeep.
            long charged = 0L;
            for (int day = 1; day <= 31; day++)
            {
                charged += VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, day));
            }

            Assert.That(charged, Is.EqualTo(catalog.StartingRoadVehicle.UpkeepPerMonth));
            Assert.That(world.Finance.Current.Get(FinanceCategory.RunningCosts), Is.EqualTo(charged));

            world.DebitCompanyCashClamped(world.CompanyCash - 3L, FinanceCategory.VehiclePurchases);
            long clamped = VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 2, 2));

            Assert.That(clamped, Is.EqualTo(3L));
            Assert.That(world.Finance.Current.Get(FinanceCategory.RunningCosts), Is.EqualTo(charged + 3L));
        }

        [Test]
        public void CashChange_EqualsIncomeMinusSpending_OverARun()
        {
            ContentCatalog catalog = CreateCatalog();
            long startingCash = CreateWorld().CompanyCash;
            GeneratedWorld world = RunScenario(catalog);

            Assert.That(world.Finance.Current.TotalIncome, Is.GreaterThan(0L));
            Assert.That(world.Finance.Current.TotalSpending, Is.GreaterThan(0L));
            Assert.That(
                world.CompanyCash - startingCash,
                Is.EqualTo(world.Finance.Current.Net + world.Finance.Previous.Net));
        }

        [Test]
        public void IdenticalRuns_ProduceIdenticalLedgers()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld first = RunScenario(catalog);
            GeneratedWorld second = RunScenario(catalog);

            Assert.That(second.Finance.Year, Is.EqualTo(first.Finance.Year));
            CollectionAssert.AreEqual(first.Finance.Current.ToArray(), second.Finance.Current.ToArray());
            CollectionAssert.AreEqual(first.Finance.Previous.ToArray(), second.Finance.Previous.ToArray());
        }

        [Test]
        public void Snapshot_CapturesAndCopiesTheLedger()
        {
            GeneratedWorld world = CreateWorld();
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(80L, FinanceCategory.ContractBonus);
            world.Finance.AdvanceTo(1901);
            world.CreditCompanyCash(30L, FinanceCategory.DeliveryIncome);

            SaveSnapshot snapshot = SaveSnapshot.Capture(61002UL, Tick.Zero, world);
            world.CreditCompanyCash(5L, FinanceCategory.DeliveryIncome);

            Assert.That(snapshot.HasFinanceLedger, Is.True);
            Assert.That(snapshot.FinanceLedger.Year, Is.EqualTo(1901));
            Assert.That(snapshot.FinanceLedger.Current.Get(FinanceCategory.DeliveryIncome), Is.EqualTo(30L));
            Assert.That(snapshot.FinanceLedger.Previous.Get(FinanceCategory.ContractBonus), Is.EqualTo(80L));
        }

        [Test]
        public void YearTotals_RejectBadAmountArrays()
        {
            Assert.Throws<ArgumentException>(() => new FinanceYearTotals(new long[2]));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FinanceYearTotals(new long[] { 1, 2, 3, 4, 5, -1 }));
        }

        private static GeneratedWorld RunScenario(ContentCatalog catalog)
        {
            GeneratedWorld world = CreateWorldWithStations(out RoadStation roadStation, out _);
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            world.Finance.AdvanceTo(1900);
            VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 1, 1));
            RoadStation secondStation = AddRoadStation(world);
            int truckId = RouteTestSupport.PlaceRoadVehicle(processor, world, roadStation.Id, secondStation.Id);
            world.CreditCompanyCash(900L, FinanceCategory.DeliveryIncome);
            VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1900, 2, 1));
            world.Finance.AdvanceTo(1901);
            world.CreditCompanyCash(400L, FinanceCategory.ContractBonus);
            VehicleUpkeep.AdvanceAll(world, catalog, new GameDate(1901, 1, 1));
            Assert.That(processor.Process(new Tick(2), new SellRoadVehicleCommand(truckId)).IsAccepted, Is.True);
            return world;
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 2, 16),
                4,
                new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
            return WorldGenerator.Generate(settings, 61002UL);
        }

        private static GeneratedWorld CreateWorldWithStations(out RoadStation roadStation, out RailStation railStation)
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate roadTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(roadTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(roadTile, out RoadStation? road), Is.True);
            roadStation = road!;
            TileCoordinate railTile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[1]);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(railTile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(railTile, out RailStation? rail), Is.True);
            railStation = rail!;
            return world;
        }

        private static RoadStation AddRoadStation(GeneratedWorld world)
        {
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate tile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[0]);
            Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? station), Is.True);
            return station!;
        }

        private static RailStation AddRailStation(GeneratedWorld world)
        {
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            TileCoordinate tile = FindOrthogonalEmptyNeighbor(world, world.TownAnchors[1]);
            Assert.That(processor.Process(new Tick(1), new BuildRailStationCommand(tile)).IsAccepted, Is.True);
            Assert.That(world.TryGetRailStationAt(tile, out RailStation? station), Is.True);
            return station!;
        }

        private static TileCoordinate FindOrthogonalEmptyNeighbor(GeneratedWorld world, TileCoordinate anchor)
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
                if (!world.Map.Contains(candidate))
                {
                    continue;
                }

                if (world.Map.GetObjectId(candidate) == TileMap.NoObjectId
                    && world.Map.GetRoadTypeId(candidate) == TileMap.NoRoadTypeId
                    && world.Map.GetRailTypeId(candidate) == TileMap.NoRailTypeId
                    && world.Map.GetStationId(candidate) == TileMap.NoStationId
                    && world.Map.GetRailStationId(candidate) == TileMap.NoRailStationId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("No empty orthogonal neighbor for station.");
        }

        private static ContentCatalog CreateCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }

        private static ContentCatalog CreateCatalogWithMk2()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: new[]
                {
                    new VehicleDefinition(
                        "truck_mk1",
                        "vehicle.truck_mk1",
                        VehicleMode.Road,
                        8000L,
                        200L,
                        1900,
                        0L,
                        true,
                        cargoCapacity: 10,
                        speedThousandths: 250),
                    new VehicleDefinition(
                        _truckMk2Id,
                        "vehicle.truck_mk2",
                        VehicleMode.Road,
                        12000L,
                        250L,
                        1910,
                        50000L,
                        false,
                        cargoCapacity: 12,
                        speedThousandths: 300),
                    new VehicleDefinition(
                        "loco_mk1",
                        "vehicle.loco_mk1",
                        VehicleMode.Rail,
                        20000L,
                        400L,
                        1900,
                        0L,
                        true,
                        maxWagons: 3,
                        capacityPerWagon: 10,
                        speedThousandths: 250),
                    new VehicleDefinition(
                        "rail_wagon",
                        "vehicle.rail_wagon",
                        VehicleMode.RailWagon,
                        4000L,
                        100L,
                        1900,
                        0L,
                        true)
                });
        }
    }
}
