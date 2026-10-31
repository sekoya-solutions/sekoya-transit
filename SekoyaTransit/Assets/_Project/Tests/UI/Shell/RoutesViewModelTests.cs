#nullable enable
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class RoutesViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void FormatMoney_LastMonthKnown_ShowsSignedAmountsWithTheCashIcon()
        {
            ShellTextCache text = CreateText();
            var item = new RouteListItem(1, "Wheat to Rome", true, true, 150L, 300L, 2, 1);

            TextLine cost = RoutesViewModel.FormatRunningCost(item, text, _culture);
            TextLine revenue = RoutesViewModel.FormatRevenue(item, text, _culture);
            TextLine profit = RoutesViewModel.FormatProfit(item, text, _culture);

            Assert.That(cost.ToPlainText(), Is.EqualTo("-150 "));
            Assert.That(revenue.ToPlainText(), Is.EqualTo("300 "));
            Assert.That(profit.ToPlainText(), Is.EqualTo("+150 "));
            CollectionAssert.Contains(profit.Segments, TextLineSegment.Cash);
            Assert.That(RoutesViewModel.ProfitClass(item), Is.EqualTo("routes-profit--positive"));
        }

        [Test]
        public void FormatProfit_Loss_IsNegativeAndRed()
        {
            ShellTextCache text = CreateText();
            var item = new RouteListItem(2, "Iron to Paris", true, true, 400L, 300L, 2, 3);

            Assert.That(RoutesViewModel.FormatProfit(item, text, _culture).ToPlainText(), Is.EqualTo("-100 "));
            Assert.That(RoutesViewModel.ProfitClass(item), Is.EqualTo("routes-profit--negative"));
        }

        [Test]
        public void FormatMoney_NoLastMonth_ShowsTheNoValueMarkWithoutColor()
        {
            ShellTextCache text = CreateText();
            var item = new RouteListItem(3, "New", false, false, 0L, 0L, 0, 0);

            Assert.That(RoutesViewModel.FormatRunningCost(item, text, _culture).ToPlainText(), Is.EqualTo("—"));
            Assert.That(RoutesViewModel.FormatRevenue(item, text, _culture).ToPlainText(), Is.EqualTo("—"));
            Assert.That(RoutesViewModel.FormatProfit(item, text, _culture).ToPlainText(), Is.EqualTo("—"));
            Assert.That(RoutesViewModel.ProfitClass(item), Is.Null);
        }

        [Test]
        public void SuggestName_SkipsNamesTakenInAnyCase()
        {
            ShellTextCache text = CreateText();
            RouteListItem[] routes =
            {
                new RouteListItem(1, "route 1", true, false, 0L, 0L, 2, 0),
                new RouteListItem(2, "Route 2", true, false, 0L, 0L, 2, 0)
            };

            Assert.That(RoutesViewModel.SuggestName(routes, text, _culture), Is.EqualTo("Route 3"));
        }

        [Test]
        public void ErrorKeys_MapEachRefusalToItsMessage()
        {
            Assert.That(RoutesViewModel.NameErrorKey(RouteNameError.None), Is.Null);
            Assert.That(RoutesViewModel.NameErrorKey(RouteNameError.Empty), Is.EqualTo(RoutesUiKeys.NameErrorEmpty));
            Assert.That(RoutesViewModel.NameErrorKey(RouteNameError.TooLong), Is.EqualTo(RoutesUiKeys.NameErrorTooLong));
            Assert.That(RoutesViewModel.NameErrorKey(RouteNameError.Taken), Is.EqualTo(RoutesUiKeys.NameErrorTaken));
            Assert.That(RoutesViewModel.StopErrorKey(RouteStopAddResult.Added), Is.Null);
            Assert.That(
                RoutesViewModel.StopErrorKey(RouteStopAddResult.ModeMismatch),
                Is.EqualTo(RoutesUiKeys.StationsModeMismatch));
            Assert.That(RoutesViewModel.StopErrorKey(RouteStopAddResult.Repeated), Is.EqualTo(RoutesUiKeys.StationsRepeated));
        }

        [Test]
        public void Fingerprint_ChangesWithMoneyNameAndValidity()
        {
            RouteListItem Item(string name, long revenue, bool valid)
            {
                return new RouteListItem(1, name, valid, true, 10L, revenue, 2, 1);
            }

            int baseline = RoutesViewModel.ComputeFingerprint(new[] { Item("A", 5L, true) });

            Assert.That(RoutesViewModel.ComputeFingerprint(new[] { Item("A", 5L, true) }), Is.EqualTo(baseline));
            Assert.That(RoutesViewModel.ComputeFingerprint(new[] { Item("B", 5L, true) }), Is.Not.EqualTo(baseline));
            Assert.That(RoutesViewModel.ComputeFingerprint(new[] { Item("A", 6L, true) }), Is.Not.EqualTo(baseline));
            Assert.That(RoutesViewModel.ComputeFingerprint(new[] { Item("A", 5L, false) }), Is.Not.EqualTo(baseline));
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(RoutesUiKeys.NoValue, "—");
            text.Set(RoutesUiKeys.NameDefault, "Route {0}");
            return text;
        }
    }
}
