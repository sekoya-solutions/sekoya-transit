#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class SimulationCalendarTests
    {
        private static readonly TimeSpan _oneSecondPerTick = TimeSpan.FromSeconds(1);
        private static readonly GameDate _startDate = new GameDate(1900, 1, 1);

        [Test]
        public void GetDate_UsesJanuaryFirst1900AtTickZero()
        {
            var calendar = new SimulationCalendar(_oneSecondPerTick);

            Assert.That(calendar.GetDate(Tick.Zero), Is.EqualTo(_startDate));
        }

        [TestCase(5, 1900, 1, 2)]
        [TestCase(153, 1900, 2, 1)]
        [TestCase(1800, 1901, 1, 1)]
        public void GetDate_AdvancesAtDayMonthAndYearBoundaries(
            long tick,
            int expectedYear,
            int expectedMonth,
            int expectedDay)
        {
            var calendar = new SimulationCalendar(_oneSecondPerTick);

            GameDate date = calendar.GetDate(new Tick(tick));

            Assert.That(date, Is.EqualTo(new GameDate(expectedYear, expectedMonth, expectedDay)));
        }

        [Test]
        public void GetDate_UsesFixed365DayYearsWithoutLeapDays()
        {
            var calendar = new SimulationCalendar(TimeSpan.FromTicks(1));
            long ticksInYear = 1800L * TimeSpan.TicksPerSecond;
            long tickBeforeNewYear = (ticksInYear * 101) - 1;

            Assert.That(
                calendar.GetDate(new Tick(ticksInYear * 100)),
                Is.EqualTo(new GameDate(2000, 1, 1)));
            Assert.That(
                calendar.GetDate(new Tick(tickBeforeNewYear)),
                Is.EqualTo(new GameDate(2000, 12, 31)));
            Assert.That(
                calendar.GetDate(new Tick(ticksInYear * 101)),
                Is.EqualTo(new GameDate(2001, 1, 1)));
            Assert.That(
                () => new GameDate(2000, 2, 29),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(1899, 1, 1)]
        [TestCase(1900, 0, 1)]
        [TestCase(1900, 13, 1)]
        [TestCase(1900, 2, 29)]
        [TestCase(1900, 4, 31)]
        public void GameDate_RejectsInvalidCalendarComponents(int year, int month, int day)
        {
            Assert.That(
                () => new GameDate(year, month, day),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void GetDate_PreservesSubDayProgressAcrossTickBoundaries()
        {
            var calendar = new SimulationCalendar(TimeSpan.FromSeconds(1));

            Assert.That(calendar.GetDate(new Tick(4)), Is.EqualTo(_startDate));
            Assert.That(calendar.GetDate(new Tick(5)), Is.EqualTo(new GameDate(1900, 1, 2)));
        }

        [Test]
        public void SimulationCalendar_UsesSimulationLoopTickDuration()
        {
            var loop = new SimulationLoop(_oneSecondPerTick, Tick.Zero);
            var calendar = new SimulationCalendar(loop);

            loop.Advance(TimeSpan.FromSeconds(1800), _ => { });

            Assert.That(calendar.GetDate(loop.CurrentTick), Is.EqualTo(new GameDate(1901, 1, 1)));
        }

        [Test]
        public void GetDate_IsIndependentOfElapsedTimeBatching()
        {
            var singleBatchLoop = new SimulationLoop(_oneSecondPerTick, Tick.Zero);
            var multipleBatchLoop = new SimulationLoop(_oneSecondPerTick, Tick.Zero);
            var calendar = new SimulationCalendar(_oneSecondPerTick);
            singleBatchLoop.Advance(TimeSpan.FromSeconds(1800), _ => { });
            for (int batch = 0; batch < 18; batch++)
            {
                multipleBatchLoop.Advance(TimeSpan.FromSeconds(100), _ => { });
            }

            Assert.That(
                calendar.GetDate(singleBatchLoop.CurrentTick),
                Is.EqualTo(calendar.GetDate(multipleBatchLoop.CurrentTick)));
        }

        [Test]
        public void GetDate_DoesNotAdvanceWhenLoopIsPausedAndSpeedScalesTickProgression()
        {
            var loop = new SimulationLoop(_oneSecondPerTick, Tick.Zero);
            var calendar = new SimulationCalendar(loop);
            loop.IsPaused = true;
            loop.Advance(TimeSpan.FromSeconds(1800), _ => { });
            Assert.That(calendar.GetDate(loop.CurrentTick), Is.EqualTo(_startDate));

            loop.IsPaused = false;
            loop.SpeedMultiplier = 2;
            loop.Advance(TimeSpan.FromSeconds(900), _ => { });

            Assert.That(calendar.GetDate(loop.CurrentTick), Is.EqualTo(new GameDate(1901, 1, 1)));
        }

        [Test]
        public void GetDate_ThrowsWhenDateExceedsSupportedYearRange()
        {
            var calendar = new SimulationCalendar(TimeSpan.MaxValue);

            Assert.That(
                () => calendar.GetDate(new Tick(5)),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void SimulationCalendar_RejectsNonPositiveTickDuration()
        {
            Assert.That(
                () => new SimulationCalendar(TimeSpan.Zero),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new SimulationCalendar(TimeSpan.FromTicks(-1)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
