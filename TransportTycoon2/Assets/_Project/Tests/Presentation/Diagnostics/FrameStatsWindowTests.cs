#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Presentation.Diagnostics;

namespace TransportTycoon.Presentation.Tests.Diagnostics
{
    public sealed class FrameStatsWindowTests
    {
        [Test]
        public void AddFrame_BeforeWindowElapses_ReturnsNoSummary()
        {
            var window = new FrameStatsWindow(1d);

            Assert.That(window.AddFrame(0.5d, 1d, 0.1d, 100L, out _), Is.False);
        }

        [Test]
        public void AddFrame_WindowElapses_ReturnsAveragedSummary()
        {
            var window = new FrameStatsWindow(1d);
            window.AddFrame(0.25d, 2d, 0.2d, 100L, out _);
            window.AddFrame(0.25d, 4d, 0.4d, 300L, out _);
            window.AddFrame(0.25d, 2d, 0.2d, 100L, out _);

            bool completed = window.AddFrame(0.25d, 4d, 0.4d, 300L, out FrameStatsSummary summary);

            Assert.That(completed, Is.True);
            Assert.That(summary.Frames, Is.EqualTo(4));
            Assert.That(summary.DurationSeconds, Is.EqualTo(1d).Within(1e-9));
            Assert.That(summary.FramesPerSecond, Is.EqualTo(4d).Within(1e-9));
            Assert.That(summary.AverageFrameMs, Is.EqualTo(250d).Within(1e-9));
            Assert.That(summary.MaxFrameMs, Is.EqualTo(250d).Within(1e-9));
            Assert.That(summary.AverageMainThreadMs, Is.EqualTo(3d).Within(1e-9));
            Assert.That(summary.AverageScriptsMs, Is.EqualTo(0.3d).Within(1e-9));
            Assert.That(summary.AverageGcAllocatedBytes, Is.EqualTo(200d).Within(1e-9));
        }

        [Test]
        public void AddFrame_AfterSummary_StartsANewWindow()
        {
            var window = new FrameStatsWindow(1d);
            window.AddFrame(2d, 10d, 1d, 1000L, out _);

            window.AddFrame(0.5d, 1d, 0.1d, 10L, out _);
            bool completed = window.AddFrame(0.5d, 3d, 0.3d, 30L, out FrameStatsSummary summary);

            Assert.That(completed, Is.True);
            Assert.That(summary.Frames, Is.EqualTo(2));
            Assert.That(summary.MaxFrameMs, Is.EqualTo(500d).Within(1e-9));
            Assert.That(summary.AverageMainThreadMs, Is.EqualTo(2d).Within(1e-9));
        }

        [Test]
        public void AddFrame_UnavailableMetrics_AreReportedAsNullAndLoggedAsNotAvailable()
        {
            var window = new FrameStatsWindow(1d);

            window.AddFrame(1d, null, null, null, out FrameStatsSummary summary);

            Assert.That(summary.AverageMainThreadMs, Is.Null);
            Assert.That(summary.AverageScriptsMs, Is.Null);
            Assert.That(summary.AverageGcAllocatedBytes, Is.Null);
            Assert.That(
                summary.ToLogLine(),
                Is.EqualTo(
                    "[FrameStats] 1.0 s: 1 frames, 1.0 fps, frame avg 1000.00 ms max 1000.00 ms, " +
                    "main thread avg n/a ms, scripts avg n/a ms, GC alloc avg n/a B/frame"));
        }

        [Test]
        public void AddFrame_InvalidFrameTime_Throws()
        {
            var window = new FrameStatsWindow(1d);

            Assert.Throws<ArgumentOutOfRangeException>(() => window.AddFrame(-0.1d, null, null, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => window.AddFrame(double.NaN, null, null, null, out _));
        }

        [Test]
        public void Constructor_NonPositiveOrNaNWindow_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStatsWindow(0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStatsWindow(double.NaN));
        }
    }
}
