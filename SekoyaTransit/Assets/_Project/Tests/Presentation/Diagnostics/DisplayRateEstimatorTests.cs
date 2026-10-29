#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Presentation.Diagnostics;

namespace SekoyaTransit.Presentation.Tests.Diagnostics
{
    public sealed class DisplayRateEstimatorTests
    {
        [Test]
        public void AddFrame_WarmupThenSamples_ReturnsTheRateAfterTheLastSample()
        {
            var estimator = new DisplayRateEstimator(warmupFrames: 2, sampleFrames: 3);

            Assert.That(estimator.AddFrame(3d, out _), Is.False, "warm-up frame (loading hitch) is skipped");
            Assert.That(estimator.AddFrame(3d, out _), Is.False);
            Assert.That(estimator.AddFrame(1d / 165d, out _), Is.False);
            Assert.That(estimator.AddFrame(1d / 165d, out _), Is.False);
            Assert.That(estimator.AddFrame(1d / 165d, out double rate), Is.True);
            Assert.That(rate, Is.EqualTo(165d).Within(1e-6));
        }

        [Test]
        public void AddFrame_OccasionalSlowFrame_DoesNotMoveTheMedian()
        {
            var estimator = new DisplayRateEstimator(warmupFrames: 0, sampleFrames: 5);
            double[] frames = { 1d / 144d, 0.05d, 1d / 144d, 1d / 144d, 0.03d };
            double rate = 0d;
            bool done = false;
            for (int index = 0; index < frames.Length; index++)
            {
                done = estimator.AddFrame(frames[index], out rate);
            }

            Assert.That(done, Is.True);
            Assert.That(rate, Is.EqualTo(144d).Within(1e-6));
        }

        [Test]
        public void AddFrame_AfterCompletion_KeepsReturningTheSameRate()
        {
            var estimator = new DisplayRateEstimator(warmupFrames: 0, sampleFrames: 1);
            estimator.AddFrame(1d / 60d, out _);

            Assert.That(estimator.AddFrame(1d, out double rate), Is.True);
            Assert.That(rate, Is.EqualTo(60d).Within(1e-6));
        }

        [Test]
        public void AddFrame_ZeroFrameTime_ReturnsAnUnknownRate()
        {
            var estimator = new DisplayRateEstimator(warmupFrames: 0, sampleFrames: 1);

            Assert.That(estimator.AddFrame(0d, out double rate), Is.True);
            Assert.That(rate, Is.Zero);
        }

        [Test]
        public void Constructor_InvalidCounts_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DisplayRateEstimator(-1, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DisplayRateEstimator(0, 0));
        }
    }
}
