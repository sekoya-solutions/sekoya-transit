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
        public void Apply_CapsAtSixtyFpsWithVerticalSyncOff()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            FramePacing.Apply();

            Assert.That(FramePacing.TargetFrameRate, Is.EqualTo(60));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
        }
    }
}
