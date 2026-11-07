#nullable enable
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class MapCashPopupViewTests
    {
        [Test]
        public void FormatIncome_English_GroupsWithCommas()
        {
            Assert.That(
                MapCashPopupView.FormatIncome(1234567L, CultureInfo.GetCultureInfo("en")),
                Is.EqualTo("+1,234,567"));
        }

        [Test]
        public void FormatIncome_French_GroupsWithTheFrenchSpace()
        {
            // Issue #338: the popup follows the selected language instead of always grouping with English commas.
            string separator = CultureInfo.GetCultureInfo("fr").NumberFormat.NumberGroupSeparator;

            string text = MapCashPopupView.FormatIncome(1234567L, CultureInfo.GetCultureInfo("fr"));

            Assert.That(separator.Length, Is.EqualTo(1));
            Assert.That(char.IsWhiteSpace(separator[0]), Is.True, "U+" + ((int)separator[0]).ToString("X4"));
            Assert.That(text, Is.EqualTo("+1" + separator + "234" + separator + "567"));
        }

        [Test]
        public void Alpha_StartMiddleEnd_StaysOpaqueThenFadesToZero()
        {
            Assert.That(MapCashPopupView.Alpha(0f), Is.EqualTo(1f));
            Assert.That(MapCashPopupView.Alpha(0.5f), Is.EqualTo(1f));
            Assert.That(MapCashPopupView.Alpha(0.75f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(MapCashPopupView.Alpha(1f), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Show_ThenTickPastLifetime_RemovesPopup()
        {
            var view = new MapCashPopupView(null, null);
            try
            {
                view.Show(new TileCoordinate(3, 4), 120L);
                view.Show(new TileCoordinate(5, 6), 40L);
                Assert.That(view.ActiveCount, Is.EqualTo(2));

                view.Tick(MapCashPopupView.LifetimeSeconds * 0.5f);
                Assert.That(view.ActiveCount, Is.EqualTo(2));

                view.Tick(MapCashPopupView.LifetimeSeconds);
                Assert.That(view.ActiveCount, Is.Zero);
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void Show_NonPositiveIncomeOrTooManyPopups_AddsNothing()
        {
            var view = new MapCashPopupView(null, null);
            try
            {
                view.Show(new TileCoordinate(1, 1), 0L);
                Assert.That(view.ActiveCount, Is.Zero);

                for (int index = 0; index < MapCashPopupView.MaxPopups + 5; index++)
                {
                    view.Show(new TileCoordinate(1, 1), 10L);
                }

                Assert.That(view.ActiveCount, Is.EqualTo(MapCashPopupView.MaxPopups));
            }
            finally
            {
                view.Dispose();
            }
        }
    }
}
