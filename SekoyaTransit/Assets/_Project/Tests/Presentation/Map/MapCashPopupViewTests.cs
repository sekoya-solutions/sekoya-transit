#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class MapCashPopupViewTests
    {
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
