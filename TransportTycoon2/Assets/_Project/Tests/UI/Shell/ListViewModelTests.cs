#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    [TestFixture]
    public sealed class ListViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void TownsFormat_UsesTheLocalizedTemplates()
        {
            ShellTextCache text = CreateText();
            TownListItem town = CreateTown(3, 120, new TownNeedListLine("wood_planks", "cargo.wood", 5, 10));

            Assert.That(TownsViewModel.FormatSummary(town, text, _culture), Is.EqualTo("Town #3 · Pop 120 · Growth 1/2"));
            Assert.That(TownsViewModel.FormatNeedLine(town.Needs[0], text, _culture), Is.EqualTo("wood planks 5/10"));
            Assert.That(TownsViewModel.FormatPassengerLine(town, text, _culture), Is.EqualTo("Passengers 7 / 4"));
            Assert.That(TownsViewModel.FormatMailLine(town, text, _culture), Is.EqualTo("Mail 3 / 2"));
        }

        [Test]
        public void TownsFingerprint_ChangesWithStructureNotWithNumbers()
        {
            TownListItem[] baseline = { CreateTown(1, 100, new TownNeedListLine("wood", "k", 1, 2)) };
            TownListItem[] populationOnly = { CreateTown(1, 250, new TownNeedListLine("wood", "k", 1, 2)) };
            TownListItem[] moreNeeds =
            {
                CreateTown(1, 100, new TownNeedListLine("wood", "k", 1, 2), new TownNeedListLine("iron", "k", 0, 4))
            };
            TownListItem[] otherTown = { CreateTown(2, 100, new TownNeedListLine("wood", "k", 1, 2)) };

            int fingerprint = TownsViewModel.ComputeFingerprint(baseline);

            Assert.That(TownsViewModel.ComputeFingerprint(populationOnly), Is.EqualTo(fingerprint));
            Assert.That(TownsViewModel.ComputeFingerprint(moreNeeds), Is.Not.EqualTo(fingerprint));
            Assert.That(TownsViewModel.ComputeFingerprint(otherTown), Is.Not.EqualTo(fingerprint));
        }

        [Test]
        public void IndustriesFormatRow_NoStacks_ShowsEmptyStock()
        {
            IndustryListItem item = CreateIndustry("sawmill", 1);

            Assert.That(
                IndustriesViewModel.FormatRow(item, CreateText(), _culture),
                Is.EqualTo("Sawmill #1 · L2 · Stock 0/100 · Produced 30 · Transported 20"));
        }

        [Test]
        public void IndustriesFormatStockDetail_OneStack_ShowsCargoAmountAndCapacity()
        {
            IndustryListItem item = CreateIndustry("sawmill", 1, 10, new CargoStackListLine("logs", "k", 10));

            Assert.That(IndustriesViewModel.FormatStockDetail(item, _culture), Is.EqualTo("logs 10/100"));
        }

        [Test]
        public void IndustriesFormatStockDetail_SeveralStacks_ShowsEachStackAndTheTotal()
        {
            IndustryListItem item = CreateIndustry(
                "sawmill",
                1,
                10,
                new CargoStackListLine("planks", "k", 4),
                new CargoStackListLine("logs", "k", 6));

            Assert.That(IndustriesViewModel.FormatStockDetail(item, _culture), Is.EqualTo("4 planks, 6 logs (10/100)"));
        }

        [Test]
        public void IndustriesFingerprint_ChangesWhenInstancesChange()
        {
            int one = IndustriesViewModel.ComputeFingerprint(new[] { CreateIndustry("sawmill", 0) });
            int sameWithNumbers = IndustriesViewModel.ComputeFingerprint(new[] { CreateIndustry("sawmill", 0, 50) });
            int other = IndustriesViewModel.ComputeFingerprint(new[] { CreateIndustry("sawmill", 1) });

            Assert.That(sameWithNumbers, Is.EqualTo(one));
            Assert.That(other, Is.Not.EqualTo(one));
        }

        [Test]
        public void UnlocksFormat_FollowsTheRowStatus()
        {
            ShellTextCache text = CreateText();
            var future = new VehicleUnlockListItem(
                "truck_mk2", "vehicle.truck_mk2", VehicleListKind.Road, 1950, 900L, VehicleUnlockRowStatus.Future, false);
            var unlockable = new VehicleUnlockListItem(
                "truck_mk2", "vehicle.truck_mk2", VehicleListKind.Road, 1950, 900L, VehicleUnlockRowStatus.Unlockable, true);
            var unlocked = new VehicleUnlockListItem(
                "truck_mk2", "vehicle.truck_mk2", VehicleListKind.Road, 1950, 900L, VehicleUnlockRowStatus.Unlocked, true);

            Assert.That(UnlocksViewModel.FormatSummary(future, text, _culture), Is.EqualTo("Truck Mk2 · from 1950 · unlock 900"));
            Assert.That(UnlocksViewModel.FormatSummary(unlockable, text, _culture), Is.EqualTo("Truck Mk2 · unlock 900"));
            Assert.That(UnlocksViewModel.FormatSummary(unlocked, text, _culture), Is.EqualTo("Truck Mk2 · Unlocked"));
            Assert.That(UnlocksViewModel.FormatAction(unlockable, text, _culture), Is.EqualTo("Unlock (900)"));
            Assert.That(UnlocksViewModel.FormatAction(unlocked, text, _culture), Is.EqualTo("Unlocked"));
            Assert.That(UnlocksViewModel.FormatAction(future, text, _culture), Is.EqualTo("Unlocked"));
        }

        [Test]
        public void UnlocksFingerprint_ChangesWithStatusAndAffordability()
        {
            VehicleUnlockListItem Item(VehicleUnlockRowStatus status, bool afford)
            {
                return new VehicleUnlockListItem(
                    "truck_mk2", "vehicle.truck_mk2", VehicleListKind.Road, 1950, 900L, status, afford);
            }

            int baseline = UnlocksViewModel.ComputeFingerprint(new[] { Item(VehicleUnlockRowStatus.Unlockable, true) });

            Assert.That(
                UnlocksViewModel.ComputeFingerprint(new[] { Item(VehicleUnlockRowStatus.Unlockable, true) }),
                Is.EqualTo(baseline));
            Assert.That(
                UnlocksViewModel.ComputeFingerprint(new[] { Item(VehicleUnlockRowStatus.Unlockable, false) }),
                Is.Not.EqualTo(baseline));
            Assert.That(
                UnlocksViewModel.ComputeFingerprint(new[] { Item(VehicleUnlockRowStatus.Unlocked, true) }),
                Is.Not.EqualTo(baseline));
        }

        [Test]
        public void VehiclesFormatRow_Road_UsesTheRoadTemplateAndCargoDetail()
        {
            var item = new VehicleListItem(
                VehicleListKind.Road,
                4,
                VehicleListStatusKind.Moving,
                6,
                20,
                300L,
                monthlyUpkeep: 40L,
                cargoStacks: new[] { new CargoStackListLine("wood", "k", 6) });

            Assert.That(
                VehiclesViewModel.FormatRow(item, false, CreateText(), _culture),
                Is.EqualTo("#4 · Truck · Moving · Cargo 6/20 · Income 300 · Upkeep 40 · 6 wood"));
        }

        [Test]
        public void VehiclesFormatRow_RailSelectedForOrders_UsesTheRailTemplateAndPrefix()
        {
            var item = new VehicleListItem(
                VehicleListKind.Rail,
                2,
                VehicleListStatusKind.Parked,
                0,
                30,
                0L,
                wagonCount: 1,
                maxWagons: 3,
                monthlyUpkeep: 90L);

            Assert.That(
                VehiclesViewModel.FormatRow(item, true, CreateText(), _culture),
                Is.EqualTo("► SELECTED · #2 · Train · Parked · Wagons 1/3 · Cargo 0/30 · Income 0 · Upkeep 90"));
        }

        [Test]
        public void VehiclesFormatBuyOptionAndAddWagon_ShowPrices()
        {
            ShellTextCache text = CreateText();
            var option = new VehiclePurchaseOption("truck_mk1", "vehicle.truck_mk1", 750L, true);

            Assert.That(VehiclesViewModel.FormatBuyOption(option, text, _culture), Is.EqualTo("Buy Truck Mk1 (750)"));
            Assert.That(VehiclesViewModel.FormatAddWagon(400L, text, _culture), Is.EqualTo("Add wagon (400)"));
        }

        [Test]
        public void VehiclesFingerprint_ChangesWithWagonsAndCargoButNotIncome()
        {
            VehicleListItem Item(int wagons, int cargo, long income)
            {
                return new VehicleListItem(
                    VehicleListKind.Rail, 1, VehicleListStatusKind.Moving, cargo, 40, income, wagons, 3);
            }

            int baseline = VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(1, 5, 0L) });

            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(1, 5, 900L) }), Is.EqualTo(baseline));
            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(2, 5, 0L) }), Is.Not.EqualTo(baseline));
            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(1, 6, 0L) }), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void PurchaseFingerprint_ChangesWithSelectionCashAndAffordability()
        {
            VehiclePurchaseUiState State(long cash, bool afford, VehiclePurchaseSelection selection)
            {
                return new VehiclePurchaseUiState(
                    cash,
                    selection,
                    new[] { new VehiclePurchaseOption("truck_mk1", "vehicle.truck_mk1", 750L, afford) },
                    400L);
            }

            int baseline = VehiclesViewModel.ComputePurchaseFingerprint(State(1_000L, true, VehiclePurchaseSelection.Road));

            Assert.That(
                VehiclesViewModel.ComputePurchaseFingerprint(State(1_000L, true, VehiclePurchaseSelection.Road)),
                Is.EqualTo(baseline));
            Assert.That(
                VehiclesViewModel.ComputePurchaseFingerprint(State(1_001L, true, VehiclePurchaseSelection.Road)),
                Is.Not.EqualTo(baseline));
            Assert.That(
                VehiclesViewModel.ComputePurchaseFingerprint(State(1_000L, false, VehiclePurchaseSelection.Road)),
                Is.Not.EqualTo(baseline));
            Assert.That(
                VehiclesViewModel.ComputePurchaseFingerprint(State(1_000L, true, VehiclePurchaseSelection.Rail)),
                Is.Not.EqualTo(baseline));
        }

        [Test]
        public void OrdersFormat_ShowsOneBasedStopsAndTheVehicle()
        {
            ShellTextCache text = CreateText();

            Assert.That(OrdersViewModel.FormatStop(0, 12, text, _culture), Is.EqualTo("#1 Station 12"));
            Assert.That(OrdersViewModel.FormatSelected(7, text, _culture), Is.EqualTo("Editing vehicle #7"));
        }

        [Test]
        public void CargoStackFormat_NoStacks_IsEmpty()
        {
            Assert.That(CargoStackFormat.FormatStacks(null, _culture), Is.Empty);
            Assert.That(CargoStackFormat.FormatStacks(Array.Empty<CargoStackListLine>(), _culture), Is.Empty);
        }

        [Test]
        public void ListSelection_SelectingOneKindClearsTheOther()
        {
            var selection = new ListSelection();

            selection.SelectTown(3);
            Assert.That(selection.IsTownSelected(3), Is.True);
            Assert.That(selection.HasIndustry, Is.False);

            selection.SelectIndustry("sawmill", 2);
            Assert.That(selection.TownId, Is.Null);
            Assert.That(selection.IsIndustrySelected("sawmill", 2), Is.True);
            Assert.That(selection.IsIndustrySelected("sawmill", 1), Is.False);
            Assert.That(selection.IsIndustrySelected("bakery", 2), Is.False);

            selection.SelectTown(5);
            Assert.That(selection.HasIndustry, Is.False);
            Assert.That(selection.IndustryInstance, Is.EqualTo(0));

            selection.Clear();
            Assert.That(selection.TownId, Is.Null);
            Assert.That(selection.IsTownSelected(5), Is.False);
        }

        [Test]
        public void ListSelection_NullIndustryId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ListSelection().SelectIndustry(null!, 0));
        }

        private static TownListItem CreateTown(int townId, int population, params TownNeedListLine[] needs)
        {
            return new TownListItem(townId, 0, 0, population, 1, 2, needs, 7L, 4L, 3L, 2L);
        }

        private static IndustryListItem CreateIndustry(
            string industryId,
            int instanceIndex,
            int cargoAmount = 0,
            params CargoStackListLine[] stacks)
        {
            return new IndustryListItem(
                industryId,
                "industry." + industryId,
                instanceIndex,
                2,
                cargoAmount,
                100,
                30L,
                20L,
                cargoStacks: stacks);
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GameUiKeys.TownsSummary, "Town #{0} · Pop {1} · Growth {2}/{3}");
            text.Set(GameUiKeys.TownsNeedLine, "{0} {1}/{2}");
            text.Set(GameUiKeys.TownsPassengerLine, "Passengers {0} / {1}");
            text.Set(GameUiKeys.TownsMailLine, "Mail {0} / {1}");
            text.Set(GameUiKeys.IndustriesRow, "{0} #{1} · L{2} · {3} · Produced {4} · Transported {5}");
            text.Set("industry.sawmill", "Sawmill");
            text.Set(GameUiKeys.UnlocksRowFuture, "{0} · from {1} · unlock {2}");
            text.Set(GameUiKeys.UnlocksRowUnlockable, "{0} · unlock {1}");
            text.Set(GameUiKeys.UnlocksRowUnlocked, "{0} · Unlocked");
            text.Set(GameUiKeys.UnlocksAction, "Unlock ({0})");
            text.Set(GameUiKeys.UnlocksActionUnlocked, "Unlocked");
            text.Set("vehicle.truck_mk1", "Truck Mk1");
            text.Set("vehicle.truck_mk2", "Truck Mk2");
            text.Set(GameUiKeys.VehiclesRow, "#{0} · {1} · {2} · Cargo {3}/{4} · Income {5} · Upkeep {6}");
            text.Set(
                GameUiKeys.VehiclesRowRail,
                "#{0} · {1} · {2} · Wagons {3}/{4} · Cargo {5}/{6} · Income {7} · Upkeep {8}");
            text.Set(GameUiKeys.VehiclesKindRoad, "Truck");
            text.Set(GameUiKeys.VehiclesKindRail, "Train");
            text.Set(GameUiKeys.VehiclesStatusMoving, "Moving");
            text.Set(GameUiKeys.VehiclesStatusParked, "Parked");
            text.Set(GameUiKeys.VehiclesBuyOption, "Buy {0} ({1})");
            text.Set(GameUiKeys.VehiclesAddWagon, "Add wagon ({0})");
            text.Set(GameUiKeys.OrdersStop, "#{0} Station {1}");
            text.Set(GameUiKeys.OrdersSelected, "Editing vehicle #{0}");
            return text;
        }
    }
}
