#nullable enable
using NUnit.Framework;
using TransportTycoon.Presentation.Diagnostics;
using UnityEngine;

namespace TransportTycoon.Presentation.Tests.Diagnostics
{
    public sealed class FramePacingTests
    {
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;

        [SetUp]
        public void SetUp()
        {
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousVSyncCount = QualitySettings.vSyncCount;
        }

        [TearDown]
        public void TearDown()
        {
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }

        [Test]
        public void ApplyDesktop_CapsAtSixtyFpsWithVerticalSyncOff()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            FramePacing.ApplyDesktop();

            Assert.That(FramePacing.TargetFrameRate, Is.EqualTo(60));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplyWebGl_DrawsOnEveryNthAnimationFrameWithoutTargetFrameRate()
        {
            Application.targetFrameRate = 60;

            FramePacing.ApplyWebGl(2);

            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(2));
            Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
        }

        [TestCase(0, 1)]
        [TestCase(9, 4)]
        public void ApplyWebGl_IntervalOutsideUnityRange_IsClamped(int interval, int expected)
        {
            FramePacing.ApplyWebGl(interval);

            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(expected));
        }

        [TestCase(60d, 1)]
        [TestCase(75d, 1)]
        [TestCase(119.9d, 2)]
        [TestCase(120d, 2)]
        [TestCase(144d, 2)]
        [TestCase(165d, 2)]
        [TestCase(240d, 4)]
        [TestCase(360d, 4)]
        [TestCase(30d, 1)]
        [TestCase(0d, 1)]
        [TestCase(-5d, 1)]
        [TestCase(double.NaN, 1)]
        public void VSyncIntervalFor_KeepsAtLeastSixtyFps(double refreshRate, int expected)
        {
            Assert.That(FramePacing.VSyncIntervalFor(refreshRate), Is.EqualTo(expected));
        }
    }
}
