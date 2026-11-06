#nullable enable
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class FinanceTableViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildRows_ListsTheLinesInOrderWithSectionSeparators()
        {
            var rows = FinanceTableViewModel.BuildRows(CreateTable(), CreateText(), _culture);

            Assert.That(rows.Count, Is.EqualTo(FinanceTableViewModel.LineCount));
            string[] expected =
            {
                GoalsUiKeys.FinanceTruckCost,
                GoalsUiKeys.FinanceTruckBenefit,
                GoalsUiKeys.FinanceTruckProfit,
                GoalsUiKeys.FinanceTrainCost,
                GoalsUiKeys.FinanceTrainBenefit,
                GoalsUiKeys.FinanceTrainProfit,
                GoalsUiKeys.FinanceTotalCost,
                GoalsUiKeys.FinanceTotalBenefit,
                GoalsUiKeys.FinanceTotalVehiclesProfit,
                GoalsUiKeys.FinanceTruckBought,
                GoalsUiKeys.FinanceTrainBought,
                GoalsUiKeys.FinanceVehiclesSold,
                GoalsUiKeys.FinanceVehiclesUnlocked,
                GoalsUiKeys.FinanceContractProfit,
                GoalsUiKeys.FinanceCheats,
                GoalsUiKeys.FinanceTotalProfit
            };
            Assert.That(rows.Count, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(rows[index].Label, Is.EqualTo(expected[index]), "line " + index);
            }

            // Sections: truck, train, total vehicles, then purchases and the rest.
            Assert.That(rows[0].StartsSection, Is.False);
            Assert.That(rows[3].StartsSection, Is.True);
            Assert.That(rows[6].StartsSection, Is.True);
            Assert.That(rows[9].StartsSection, Is.True);
            Assert.That(rows[14].StartsSection, Is.False);
            Assert.That(rows[15].StartsSection, Is.False);
            Assert.That(rows[2].IsTotal, Is.True);
            Assert.That(rows[14].IsTotal, Is.False);
            Assert.That(rows[15].IsTotal, Is.True);
            Assert.That(rows[1].IsTotal, Is.False);
        }

        [Test]
        public void BuildRows_SignsAndColoursTheAmounts()
        {
            var rows = FinanceTableViewModel.BuildRows(CreateTable(), CreateText(), _culture);

            // Truck, current month: cost 250, benefit 350, profit 100.
            Assert.That(rows[0].Cells[1].Text, Is.EqualTo("-250"));
            Assert.That(rows[0].Cells[1].Tone, Is.EqualTo(FinanceCellTone.Negative));
            Assert.That(rows[1].Cells[1].Text, Is.EqualTo("+350"));
            Assert.That(rows[1].Cells[1].Tone, Is.EqualTo(FinanceCellTone.Positive));
            Assert.That(rows[2].Cells[1].Text, Is.EqualTo("+100"));
            // Train, current month: cost 250, benefit 150, profit -100.
            Assert.That(rows[5].Cells[1].Text, Is.EqualTo("-100"));
            Assert.That(rows[5].Cells[1].Tone, Is.EqualTo(FinanceCellTone.Negative));
            // Purchases are spending, sales, contracts and cheats are income, total profit is the signed cash change.
            Assert.That(rows[9].Cells[1].Text, Is.EqualTo("-2,000"));
            Assert.That(rows[10].Cells[1].Text, Is.EqualTo("-1,000"));
            Assert.That(rows[11].Cells[1].Text, Is.EqualTo("+500"));
            Assert.That(rows[12].Cells[1].Text, Is.EqualTo("-40"));
            Assert.That(rows[13].Cells[1].Text, Is.EqualTo("+60"));
            Assert.That(rows[14].Cells[1].Text, Is.EqualTo("+100"));
            Assert.That(rows[15].Cells[1].Text, Is.EqualTo("-2,800"));
            Assert.That(rows[15].Cells[1].Tone, Is.EqualTo(FinanceCellTone.Negative));
        }

        [Test]
        public void BuildRows_TotalVehiclesLinesUseTheTotalsNotTheTruckAndTrainSum()
        {
            var rows = FinanceTableViewModel.BuildRows(CreateTable(), CreateText(), _culture);

            // Total cost 500 and benefit 600 include 0 general amounts here; the profit is benefit minus cost.
            Assert.That(rows[6].Cells[1].Text, Is.EqualTo("-500"));
            Assert.That(rows[7].Cells[1].Text, Is.EqualTo("+600"));
            Assert.That(rows[8].Cells[1].Text, Is.EqualTo("+100"));
        }

        [Test]
        public void BuildRows_ZeroAmount_IsNeutralWithoutASign()
        {
            var rows = FinanceTableViewModel.BuildRows(FinanceTable.Empty, CreateText(), _culture);

            Assert.That(rows[0].Cells[1].Text, Is.EqualTo("0"));
            Assert.That(rows[0].Cells[1].Tone, Is.EqualTo(FinanceCellTone.Neutral));
            Assert.That(rows[0].Cells[1].ShowsCashIcon, Is.True);
        }

        [Test]
        public void BuildRows_UnavailableColumns_ShowNotAvailableYetOnEveryLine()
        {
            var rows = FinanceTableViewModel.BuildRows(FinanceTable.Empty, CreateText(), _culture);

            for (int index = 0; index < rows.Count; index++)
            {
                Assert.That(rows[index].Cells[0].Text, Is.EqualTo("Not available yet"));
                Assert.That(rows[index].Cells[0].Tone, Is.EqualTo(FinanceCellTone.Unavailable));
                Assert.That(rows[index].Cells[0].ShowsCashIcon, Is.False);
                Assert.That(rows[index].Cells[2].Tone, Is.EqualTo(FinanceCellTone.Unavailable));
                Assert.That(rows[index].Cells[1].Tone, Is.Not.EqualTo(FinanceCellTone.Unavailable));
                Assert.That(rows[index].Cells[3].Tone, Is.Not.EqualTo(FinanceCellTone.Unavailable));
            }
        }

        [Test]
        public void BuildHeaders_ReturnsTheFourColumnsInOrder()
        {
            string[] headers = FinanceTableViewModel.BuildHeaders(CreateText());

            CollectionAssert.AreEqual(
                new[] { "Last month", "Current month", "Last year", "Current year" },
                headers);
        }

        [Test]
        public void Rows_WithTheSameContent_AreEqual()
        {
            var first = FinanceTableViewModel.BuildRows(CreateTable(), CreateText(), _culture);
            var second = FinanceTableViewModel.BuildRows(CreateTable(), CreateText(), _culture);

            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(first[index], Is.EqualTo(second[index]));
            }
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.FinanceColumnLastMonth, "Last month");
            text.Set(GoalsUiKeys.FinanceColumnCurrentMonth, "Current month");
            text.Set(GoalsUiKeys.FinanceColumnLastYear, "Last year");
            text.Set(GoalsUiKeys.FinanceColumnCurrentYear, "Current year");
            text.Set(GoalsUiKeys.FinanceNotAvailable, "Not available yet");
            return text;
        }

        private static FinanceTable CreateTable()
        {
            var figures = new FinanceFigures(
                truckCost: 250L,
                truckBenefit: 350L,
                trainCost: 250L,
                trainBenefit: 150L,
                totalCost: 500L,
                totalBenefit: 600L,
                truckBought: 2000L,
                trainBought: 1000L,
                vehiclesSold: 500L,
                vehiclesUnlocked: 40L,
                contractProfit: 60L,
                cheats: 100L,
                totalProfit: -2800L);
            var column = new FinanceColumn(true, figures);
            return new FinanceTable(column, column, column, column);
        }
    }
}
