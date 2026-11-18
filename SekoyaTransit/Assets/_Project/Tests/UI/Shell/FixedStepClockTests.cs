#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class FixedStepClockTests
    {
        [Test]
        public void Now_WithoutFixedStep_FollowsRealTime()
        {
            var clock = new FixedStepClock();

            Assert.That(clock.Now(10, 3.5f), Is.EqualTo(3.5f));
            Assert.That(clock.DeltaTime(0.016f), Is.EqualTo(0.016f));
            Assert.That(clock.IsFixedStep, Is.False);
        }

        [Test]
        public void Now_DuringFixedStep_AdvancesOneStepPerFrameWhateverTheRealTime()
        {
            var clock = new FixedStepClock();
            clock.BeginFixedStep(0.5f, 100, 7f);

            Assert.That(clock.Now(100, 7f), Is.EqualTo(7f));
            Assert.That(clock.Now(104, 7.1f), Is.EqualTo(9f));
            Assert.That(clock.Now(104, 60f), Is.EqualTo(9f));
            Assert.That(clock.DeltaTime(0.9f), Is.EqualTo(0.5f));
        }

        [Test]
        public void EndFixedStep_ContinuesFromTheTimeReachedWithoutAJump()
        {
            var clock = new FixedStepClock();
            clock.BeginFixedStep(1f, 0, 2f);
            clock.EndFixedStep(10, 5f);

            Assert.That(clock.IsFixedStep, Is.False);
            Assert.That(clock.Now(10, 5f), Is.EqualTo(12f));
            Assert.That(clock.Now(11, 5.25f), Is.EqualTo(12.25f));
        }

        [Test]
        public void BeginFixedStep_AfterAnEarlierOne_StartsFromTheCurrentTime()
        {
            var clock = new FixedStepClock();
            clock.BeginFixedStep(1f, 0, 0f);
            clock.EndFixedStep(4, 1f);
            clock.BeginFixedStep(0.25f, 10, 2f);

            Assert.That(clock.Now(10, 2f), Is.EqualTo(5f));
            Assert.That(clock.Now(14, 2f), Is.EqualTo(6f));
        }

        [Test]
        public void BeginFixedStep_NotPositive_Throws()
        {
            var clock = new FixedStepClock();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginFixedStep(0f, 0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginFixedStep(float.NaN, 0, 0f));
        }
    }
}
