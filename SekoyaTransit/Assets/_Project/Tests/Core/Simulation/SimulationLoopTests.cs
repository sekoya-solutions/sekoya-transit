#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class SimulationLoopTests
    {
        private static readonly TimeSpan _tickDuration = TimeSpan.FromSeconds(1);

        [Test]
        public void Advance_ProcessesOneTickWhenDurationIsReached()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);
            var advancedTicks = new List<Tick>();

            loop.Advance(_tickDuration, advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1) }, advancedTicks);
            Assert.That(loop.CurrentTick, Is.EqualTo(new Tick(1)));
        }

        [Test]
        public void Advance_PreservesPartialElapsedTimeBetweenCalls()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);
            var advancedTicks = new List<Tick>();

            loop.Advance(TimeSpan.FromMilliseconds(400), advancedTicks.Add);
            loop.Advance(TimeSpan.FromMilliseconds(600), advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1) }, advancedTicks);
        }

        [Test]
        public void Advance_ProcessesEveryWholeTickAndRetainsRemainder()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);
            var advancedTicks = new List<Tick>();

            loop.Advance(TimeSpan.FromMilliseconds(2500), advancedTicks.Add);
            loop.Advance(TimeSpan.FromMilliseconds(500), advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1), new Tick(2), new Tick(3) }, advancedTicks);
            Assert.That(loop.CurrentTick, Is.EqualTo(new Tick(3)));
        }

        [Test]
        public void Advance_CallbackPausesLoop_StopsRemainingTicks()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero, speedMultiplier: 3);
            var advancedTicks = new List<Tick>();

            loop.Advance(_tickDuration, tick =>
            {
                advancedTicks.Add(tick);
                loop.IsPaused = true;
            });

            CollectionAssert.AreEqual(new[] { new Tick(1) }, advancedTicks);
            Assert.That(loop.CurrentTick, Is.EqualTo(new Tick(1)));
        }

        [Test]
        public void Advance_AppliesSpeedMultiplierWithoutChangingTickSequence()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero, speedMultiplier: 3);
            var advancedTicks = new List<Tick>();

            loop.Advance(_tickDuration, advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1), new Tick(2), new Tick(3) }, advancedTicks);
        }

        [Test]
        public void Advance_UsesUpdatedSpeedMultiplierForSubsequentElapsedTime()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);
            var advancedTicks = new List<Tick>();

            loop.Advance(TimeSpan.FromMilliseconds(500), advancedTicks.Add);
            loop.SpeedMultiplier = 2;
            loop.Advance(TimeSpan.FromMilliseconds(250), advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1) }, advancedTicks);
        }

        [Test]
        public void Advance_DiscardsElapsedTimeWhilePaused()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero) { IsPaused = true };
            var advancedTicks = new List<Tick>();

            loop.Advance(TimeSpan.FromSeconds(10), advancedTicks.Add);
            loop.IsPaused = false;
            loop.Advance(TimeSpan.FromMilliseconds(999), advancedTicks.Add);

            Assert.That(advancedTicks, Is.Empty);
        }

        [Test]
        public void Advance_PreservesPartialTickTimeAcrossPause()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);
            var advancedTicks = new List<Tick>();

            loop.Advance(TimeSpan.FromMilliseconds(400), advancedTicks.Add);
            loop.IsPaused = true;
            loop.Advance(TimeSpan.FromSeconds(10), advancedTicks.Add);
            loop.IsPaused = false;
            loop.Advance(TimeSpan.FromMilliseconds(600), advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1) }, advancedTicks);
        }

        [Test]
        public void Constructor_RejectsNonPositiveTickDuration()
        {
            Assert.That(
                () => new SimulationLoop(TimeSpan.Zero, Tick.Zero),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new SimulationLoop(TimeSpan.FromTicks(-1), Tick.Zero),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsNonPositiveSpeedMultiplier()
        {
            Assert.That(
                () => new SimulationLoop(_tickDuration, Tick.Zero, speedMultiplier: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SpeedMultiplier_RejectsNonPositiveValuesWithoutChangingCurrentSpeed()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);

            Assert.That(() => loop.SpeedMultiplier = 0, Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(loop.SpeedMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void Advance_RejectsNegativeElapsedTime()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);

            Assert.That(
                () => loop.Advance(TimeSpan.FromTicks(-1), _ => { }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Advance_RejectsNullCallback()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);

            Assert.That(
                () => loop.Advance(_tickDuration, null!),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Advance_PropagatesCallbackFailureWithoutConsumingPendingTick()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero);

            Assert.That(
                () => loop.Advance(_tickDuration, _ => throw new InvalidOperationException()),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(loop.CurrentTick, Is.EqualTo(Tick.Zero));

            var advancedTicks = new List<Tick>();
            loop.Advance(TimeSpan.Zero, advancedTicks.Add);

            CollectionAssert.AreEqual(new[] { new Tick(1) }, advancedTicks);
        }

        [Test]
        public void Advance_ThrowsOnTickOverflowWithoutWrapping()
        {
            var loop = new SimulationLoop(_tickDuration, new Tick(long.MaxValue));

            Assert.That(
                () => loop.Advance(_tickDuration, _ => { }),
                Throws.TypeOf<OverflowException>());

            Assert.That(loop.CurrentTick, Is.EqualTo(new Tick(long.MaxValue)));
        }

        [Test]
        public void Advance_ThrowsWhenElapsedTimeScalingOverflows()
        {
            var loop = new SimulationLoop(_tickDuration, Tick.Zero, int.MaxValue);

            Assert.That(
                () => loop.Advance(TimeSpan.MaxValue, _ => { }),
                Throws.TypeOf<OverflowException>());
        }
    }
}
