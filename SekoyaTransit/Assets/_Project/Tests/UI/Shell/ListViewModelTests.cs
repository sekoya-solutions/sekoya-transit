#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class ListViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void IndustriesFormatSummary_GrowingIndustry_ShowsNumbersFromOne()
        {
            IndustryListItem item = CreateGrowingForest(tier: 0, maxTier: 2, servedMonths: 2);

            Assert.That(
                IndustriesViewModel.FormatSummary(item, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Forest #1 - Growth level 1/3"));
        }

        [Test]
        public void IndustriesFormatSummary_TopTier_ShowsTheLastLevel()
        {
            IndustryListItem item = CreateGrowingForest(tier: 2, maxTier: 2, servedMonths: 0);

            Assert.That(
                IndustriesViewModel.FormatSummary(item, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Forest #1 - Growth level 3/3"));
        }

        [Test]
        public void IndustriesFormatSummary_ProcessingIndustry_HasNoGrowthLevel()
        {
            IndustryListItem item = CreateIndustry("sawmill", 1);

            Assert.That(
                IndustriesViewModel.FormatSummary(item, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Sawmill #2"));
        }

        [Test]
        public void IndustriesFormatLines_ShowStockLastMonthAndProgress()
        {
            IndustryListItem item = CreateGrowingForest(tier: 0, maxTier: 2, servedMonths: 2);
            ShellTextCache text = CreateText();

            Assert.That(IndustriesViewModel.FormatStockLine(item, text, _culture).ToPlainText(), Is.EqualTo("Current stock/max stock: 40/40 "));
            Assert.That(IndustriesViewModel.FormatProductionLine(item, text, _culture).ToPlainText(), Is.EqualTo("Last month production: 500 "));
            Assert.That(IndustriesViewModel.FormatDeliveredLine(item, text, _culture).ToPlainText(), Is.EqualTo("Last month delivered: 450 "));
            Assert.That(IndustriesViewModel.FormatProgressLine(item, text, _culture).ToPlainText(), Is.EqualTo("Next level progress: 2/12 months"));
        }

        [Test]
        public void IndustriesFormatStockLine_ShowsTheCargoIcon()
        {
            IndustryListItem item = CreateGrowingForest(tier: 0, maxTier: 2, servedMonths: 0);

            TextLine line = IndustriesViewModel.FormatStockLine(item, CreateText(), _culture);

            Assert.That(line.Segments[line.Segments.Count - 1].Kind, Is.EqualTo(TextLineSegmentKind.CargoIcon));
            Assert.That(line.Segments[line.Segments.Count - 1].Value, Is.EqualTo("wood"));
        }

        [Test]
        public void IndustriesFormatProgressLine_TopTierShowsANote_ProcessingIsEmpty()
        {
            ShellTextCache text = CreateText();

            Assert.That(
                IndustriesViewModel.FormatProgressLine(CreateGrowingForest(2, 2, 0), text, _culture).ToPlainText(),
                Is.EqualTo("Next level progress: max level reached"));
            Assert.That(
                IndustriesViewModel.FormatProgressLine(CreateIndustry("sawmill", 0), text, _culture).ToPlainText(),
                Is.Empty);
        }

        [Test]
        public void IndustriesFormatRules_UsesTheGrowthData()
        {
            var rules = new IndustryGrowthRulesInfo(80, 12, 2, 3);

            Assert.That(
                IndustriesViewModel.FormatRules(rules, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Grow at 80% for 12 months, x2, up to level 3"));
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
        public void VehiclesFormatTitle_Road_ShowsKindNumberAndStatus()
        {
            var item = new VehicleListItem(VehicleListKind.Road, 4, VehicleListStatusKind.Moving, 6, 20, 300L);

            Assert.That(VehiclesViewModel.FormatTitle(item, CreateText(), _culture), Is.EqualTo("Truck #4 - Moving"));
        }

        [Test]
        public void VehiclesFormatTitle_Rail_AddsTheWagons()
        {
            var item = new VehicleListItem(
                VehicleListKind.Rail, 2, VehicleListStatusKind.Parked, 0, 30, 0L, wagonCount: 1, maxWagons: 3);

            Assert.That(
                VehiclesViewModel.FormatTitle(item, CreateText(), _culture),
                Is.EqualTo("Train #2 - Parked · Wagons 1/3"));
        }

        [Test]
        public void VehiclesFormatCargo_WithStacks_ShowsLoadAndCargo()
        {
            var item = new VehicleListItem(
                VehicleListKind.Road,
                4,
                VehicleListStatusKind.Moving,
                6,
                20,
                0L,
                cargoStacks: new[] { new CargoStackListLine("wood", "k", 6) });

            Assert.That(
                VehiclesViewModel.FormatCargo(item, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Cargo (6/20): 6 "));
        }

        [Test]
        public void VehiclesFormatCargo_NoStacks_ShowsEmpty()
        {
            var item = new VehicleListItem(VehicleListKind.Road, 4, VehicleListStatusKind.Moving, 0, 20, 0L);

            Assert.That(
                VehiclesViewModel.FormatCargo(item, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Cargo (0/20): Empty"));
        }

        [Test]
        public void VehiclesFormatFinances_KnownMonth_ShowsUpkeepIncomeAndSignedBenefitOnTwoLines()
        {
            VehicleListItem Item(long income)
            {
                return new VehicleListItem(
                    VehicleListKind.Road,
                    1,
                    VehicleListStatusKind.Moving,
                    0,
                    20,
                    0L,
                    monthlyUpkeep: 150L,
                    routeName: "A",
                    routeId: 0,
                    hasLastMonth: true,
                    lastMonthIncome: income);
            }

            Assert.That(
                VehiclesViewModel.FormatFinances(Item(380L), CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Last month upkeep/income: 150/380 "));
            Assert.That(
                VehiclesViewModel.FormatBenefit(Item(380L), CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Last month benefit: +230 "));
            Assert.That(
                VehiclesViewModel.FormatFinances(Item(20L), CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Last month upkeep/income: 150/20 "));
            Assert.That(
                VehiclesViewModel.FormatBenefit(Item(20L), CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Last month benefit: -130 "));
        }

        [Test]
        public void VehiclesFormatFinances_NewOrUnassigned_ShowsNotAvailableYet()
        {
            var fresh = new VehicleListItem(
                VehicleListKind.Road, 1, VehicleListStatusKind.Moving, 0, 20, 0L, monthlyUpkeep: 150L, routeName: "A", routeId: 0);
            var unassigned = new VehicleListItem(
                VehicleListKind.Road, 2, VehicleListStatusKind.Unassigned, 0, 20, 0L, hasLastMonth: true, lastMonthIncome: 90L);

            Assert.That(
                VehiclesViewModel.FormatFinances(fresh, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Last month upkeep/income: Not available yet"));
            Assert.That(
                VehiclesViewModel.FormatFinances(unassigned, CreateText(), _culture).ToPlainText(),
                Is.EqualTo("Last month upkeep/income: Not available yet"));
            Assert.That(VehiclesViewModel.FormatBenefit(fresh, CreateText(), _culture).IsEmpty, Is.True);
        }

        [Test]
        public void VehiclesCardClass_ProfitGreenLossRedUnknownNeutral()
        {
            VehicleListItem Item(VehicleListStatusKind status, bool hasLastMonth, long income)
            {
                return new VehicleListItem(
                    VehicleListKind.Road,
                    1,
                    status,
                    0,
                    20,
                    0L,
                    monthlyUpkeep: 100L,
                    hasLastMonth: hasLastMonth,
                    lastMonthIncome: income);
            }

            Assert.That(VehiclesViewModel.CardClass(Item(VehicleListStatusKind.Moving, true, 101L)), Is.EqualTo("vehicles-row--profit"));
            Assert.That(VehiclesViewModel.CardClass(Item(VehicleListStatusKind.Moving, true, 100L)), Is.EqualTo("vehicles-row--loss"));
            Assert.That(VehiclesViewModel.CardClass(Item(VehicleListStatusKind.Moving, true, 0L)), Is.EqualTo("vehicles-row--loss"));
            Assert.That(VehiclesViewModel.CardClass(Item(VehicleListStatusKind.Moving, false, 0L)), Is.Null);
            Assert.That(VehiclesViewModel.CardClass(Item(VehicleListStatusKind.Unassigned, true, 500L)), Is.Null);
        }

        [Test]
        public void VehiclesFormatSell_ShowsTheSaleValue()
        {
            var item = new VehicleListItem(VehicleListKind.Road, 1, VehicleListStatusKind.Moving, 0, 20, 0L, sellValue: 2500L);

            Assert.That(VehiclesViewModel.FormatSell(item, CreateText(), _culture).ToPlainText(), Is.EqualTo("Sell for 2,500 "));
        }

        [Test]
        public void VehiclesFormatSell_Train_NamesTheTrainAndWagonSale()
        {
            var item = new VehicleListItem(
                VehicleListKind.Rail, 1, VehicleListStatusKind.Moving, 0, 20, 0L, wagonCount: 1, maxWagons: 3, sellValue: 4000L);

            Assert.That(VehiclesViewModel.FormatSell(item, CreateText(), _culture).ToPlainText(), Is.EqualTo("Sell train for 4,000 "));
            Assert.That(VehiclesViewModel.FormatSellWagon(150L, CreateText(), _culture).ToPlainText(), Is.EqualTo("Sell wagon for 150 "));
        }

        [Test]
        public void VehiclesFormatAddWagon_ShowsPrice()
        {
            ShellTextCache text = CreateText();

            Assert.That(VehiclesViewModel.FormatAddWagon(400L, text, _culture).ToPlainText(), Is.EqualTo("Buy wagon for 400 "));
        }

        [Test]
        public void VehiclesFingerprint_ChangesWithWagonsButNotCargoAmountOrIncome()
        {
            VehicleListItem Item(int wagons, int cargo, long income)
            {
                return new VehicleListItem(
                    VehicleListKind.Rail, 1, VehicleListStatusKind.Moving, cargo, 40, income, wagons, 3);
            }

            int baseline = VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(1, 5, 0L) });

            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(1, 5, 900L) }), Is.EqualTo(baseline));
            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(2, 5, 0L) }), Is.Not.EqualTo(baseline));
            Assert.That(
                VehiclesViewModel.ComputeVehicleFingerprint(new[] { Item(1, 6, 0L) }),
                Is.EqualTo(baseline),
                "A cargo change updates in place: a rebuild would swallow a click on a button.");
            var onRoute = new VehicleListItem(
                VehicleListKind.Rail, 1, VehicleListStatusKind.Moving, 5, 40, 0L, 1, 3, routeName: "Wheat", routeId: 2);
            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { onRoute }), Is.Not.EqualTo(baseline));
            var withMonth = new VehicleListItem(
                VehicleListKind.Rail, 1, VehicleListStatusKind.Moving, 5, 40, 0L, 1, 3, hasLastMonth: true, lastMonthIncome: 70L);
            Assert.That(VehiclesViewModel.ComputeVehicleFingerprint(new[] { withMonth }), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void CargoStackFormat_NoStacks_IsEmpty()
        {
            Assert.That(CargoStackFormat.FormatStacks(null, _culture).IsEmpty, Is.True);
            Assert.That(CargoStackFormat.FormatStacks(Array.Empty<CargoStackListLine>(), _culture).IsEmpty, Is.True);
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

        private static IndustryListItem CreateGrowingForest(int tier, int maxTier, int servedMonths)
        {
            return new IndustryListItem(
                "forest",
                "industry.forest",
                0,
                1,
                40,
                40,
                0L,
                0L,
                lastMonthProduced: 500L,
                lastMonthTransported: 450L,
                canGrow: true,
                growthTier: tier,
                maxGrowthTier: maxTier,
                servedMonths: servedMonths,
                monthsToGrow: 12,
                outputCargoIds: new[] { "wood" });
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GameUiKeys.IndustriesSummary, "{0} #{1}");
            text.Set(GameUiKeys.IndustriesSummaryGrowth, "{0} #{1} - Growth level {2}/{3}");
            text.Set(GameUiKeys.IndustriesStock, "Current stock/max stock: {0}/{1} {2}");
            text.Set(GameUiKeys.IndustriesProduction, "Last month production: {0} {1}");
            text.Set(GameUiKeys.IndustriesDelivered, "Last month delivered: {0} {1}");
            text.Set(GameUiKeys.IndustriesProgress, "Next level progress: {0}/{1} months");
            text.Set(GameUiKeys.IndustriesProgressMax, "Next level progress: max level reached");
            text.Set(GameUiKeys.IndustriesRulesIntro, "Grow at {0}% for {1} months, x{2}, up to level {3}");
            text.Set("industry.forest", "Forest");
            text.Set("industry.sawmill", "Sawmill");
            text.Set("vehicle.truck_mk1", "Truck Mk1");
            text.Set("vehicle.truck_mk2", "Truck Mk2");
            text.Set(GameUiKeys.VehiclesRow, "{1} #{0} - {2}");
            text.Set(GameUiKeys.VehiclesRowRail, "{1} #{0} - {2} · Wagons {3}/{4}");
            text.Set(GameUiKeys.VehiclesCargo, "Cargo ({0}/{1}): {2}");
            text.Set(GameUiKeys.VehiclesCargoEmpty, "Empty");
            text.Set(GameUiKeys.VehiclesFinance, "Last month upkeep/income: {0}/{1}");
            text.Set(GameUiKeys.VehiclesBenefit, "Last month benefit: {0}");
            text.Set(GameUiKeys.VehiclesFinanceUnknown, "Last month upkeep/income: Not available yet");
            text.Set(GameUiKeys.VehiclesSell, "Sell for {0}");
            text.Set(GameUiKeys.VehiclesKindRoad, "Truck");
            text.Set(GameUiKeys.VehiclesKindRail, "Train");
            text.Set(GameUiKeys.VehiclesStatusMoving, "Moving");
            text.Set(GameUiKeys.VehiclesStatusParked, "Parked");
            text.Set(GameUiKeys.VehiclesAddWagon, "Buy wagon for {0}");
            text.Set(GameUiKeys.VehiclesSellWagon, "Sell wagon for {0}");
            text.Set(GameUiKeys.VehiclesSellTrain, "Sell train for {0}");
            text.Set(RoutesUiKeys.VehiclesRoute, "Route:");
            text.Set(RoutesUiKeys.VehiclesStatusUnassigned, "Unassigned");
            return text;
        }
    }
}
