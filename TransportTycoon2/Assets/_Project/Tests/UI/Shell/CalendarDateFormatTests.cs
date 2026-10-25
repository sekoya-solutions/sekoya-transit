#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class CalendarDateFormatTests
    {
        [Test]
        public void FormatLabeled_SimulationDateAdvances_UpdatesDisplayedDate()
        {
            var loop = new SimulationLoop(TimeSpan.FromMilliseconds(50), Tick.Zero);
            var calendar = new SimulationCalendar(loop);
            CultureInfo culture = CultureInfo.GetCultureInfo("en");

            string initial = CalendarDateFormat.FormatLabeled(
                "Date: {0}",
                calendar.GetDate(loop.CurrentTick).ToString(),
                culture);

            loop.SpeedMultiplier = 3;
            loop.Advance(TimeSpan.FromMinutes(10), _ => { });

            string advanced = CalendarDateFormat.FormatLabeled(
                "Date: {0}",
                calendar.GetDate(loop.CurrentTick).ToString(),
                culture);

            Assert.That(initial, Is.EqualTo("Date: 1900-01-01"));
            Assert.That(advanced, Is.EqualTo("Date: 1901-01-01"));
        }
    }
}
