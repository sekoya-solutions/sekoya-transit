#nullable enable
using System.Globalization;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    [TestFixture]
    public sealed class FinanceRecapViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildRows_ListsHeadingLinesTotalsAndNetForBothYears()
        {
            ShellTextCache text = CreateText();
            FinanceRecap recap = CreateRecap();

            var rows = FinanceRecapViewModel.BuildRows(recap, text, _culture);

            // Per year: heading, 1 income line, total income, 1 spending line, total spending, net.
            Assert.That(rows.Count, Is.EqualTo(12));
            Assert.That(rows[0].Kind, Is.EqualTo(FinanceRecapRowKind.Heading));
            Assert.That(rows[0].Label, Is.EqualTo("This year (1901)"));
            Assert.That(rows[1].Label, Is.EqualTo("Deliveries"));
            Assert.That(rows[1].Value, Is.EqualTo("12,500"));
            Assert.That(rows[2].Kind, Is.EqualTo(FinanceRecapRowKind.TotalIncome));
            Assert.That(rows[2].Value, Is.EqualTo("12,500"));
            Assert.That(rows[3].Label, Is.EqualTo("Running costs"));
            Assert.That(rows[3].Value, Is.EqualTo("2,000"));
            Assert.That(rows[4].Kind, Is.EqualTo(FinanceRecapRowKind.TotalSpending));
            Assert.That(rows[4].Value, Is.EqualTo("2,000"));
            Assert.That(rows[5].Kind, Is.EqualTo(FinanceRecapRowKind.NetResult));
            Assert.That(rows[5].Value, Is.EqualTo("10,500"));
            Assert.That(rows[6].Label, Is.EqualTo("Last year (1900)"));
            Assert.That(rows[11].Kind, Is.EqualTo(FinanceRecapRowKind.NetResult));
        }

        [Test]
        public void BuildRows_NetLoss_IsNegative()
        {
            ShellTextCache text = CreateText();
            var year = new FinanceYearRecap(
                1900,
                new[]
                {
                    new FinanceRecapLine("finance.category.delivery_income", 100L, true),
                    new FinanceRecapLine("finance.category.running_costs", 350L, false)
                });
            var recap = new FinanceRecap(year, new FinanceYearRecap(0, System.Array.Empty<FinanceRecapLine>()));

            var rows = FinanceRecapViewModel.BuildRows(recap, text, _culture);

            Assert.That(rows[5].Value, Is.EqualTo("-250"));
        }

        [Test]
        public void BuildRows_YearNotStarted_ShowsDashInHeading()
        {
            ShellTextCache text = CreateText();
            var empty = new FinanceYearRecap(0, System.Array.Empty<FinanceRecapLine>());

            var rows = FinanceRecapViewModel.BuildRows(new FinanceRecap(empty, empty), text, _culture);

            Assert.That(rows[0].Label, Is.EqualTo("This year (-)"));
            Assert.That(rows[1].Kind, Is.EqualTo(FinanceRecapRowKind.TotalIncome));
            Assert.That(rows[1].Value, Is.EqualTo("0"));
        }

        [Test]
        public void YearRecap_TotalsSumIncomeAndSpendingLines()
        {
            FinanceYearRecap year = CreateRecap().Current;

            Assert.That(year.TotalIncome, Is.EqualTo(12500L));
            Assert.That(year.TotalSpending, Is.EqualTo(2000L));
            Assert.That(year.Net, Is.EqualTo(10500L));
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.FinanceHeadingCurrent, "This year ({0})");
            text.Set(GoalsUiKeys.FinanceHeadingPrevious, "Last year ({0})");
            text.Set(GoalsUiKeys.FinanceTotalIncome, "Total income");
            text.Set(GoalsUiKeys.FinanceTotalSpending, "Total spending");
            text.Set(GoalsUiKeys.FinanceNetResult, "Net result");
            text.Set(GoalsUiKeys.FinanceDeliveryIncome, "Deliveries");
            text.Set(GoalsUiKeys.FinanceRunningCosts, "Running costs");
            return text;
        }

        private static FinanceRecap CreateRecap()
        {
            var lines = new[]
            {
                new FinanceRecapLine(GoalsUiKeys.FinanceDeliveryIncome, 12500L, true),
                new FinanceRecapLine(GoalsUiKeys.FinanceRunningCosts, 2000L, false)
            };
            return new FinanceRecap(new FinanceYearRecap(1901, lines), new FinanceYearRecap(1900, lines));
        }
    }
}
