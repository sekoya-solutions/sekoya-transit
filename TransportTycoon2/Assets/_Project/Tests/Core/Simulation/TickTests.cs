#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class TickTests
    {
        [Test]
        public void Zero_HasValueZero()
        {
            Assert.That(Tick.Zero.Value, Is.EqualTo(0));
        }

        [Test]
        public void Next_IncrementsByOne()
        {
            Assert.That(new Tick(41).Next(), Is.EqualTo(new Tick(42)));
        }

        [Test]
        public void Constructor_RejectsNegativeValues()
        {
            Assert.That(() => new Tick(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Next_ThrowsOnOverflowInsteadOfWrapping()
        {
            Assert.That(() => new Tick(long.MaxValue).Next(), Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Equality_IsBasedOnValue()
        {
            Assert.That(new Tick(7) == new Tick(7), Is.True);
            Assert.That(new Tick(7) != new Tick(8), Is.True);
            Assert.That(new Tick(7).GetHashCode(), Is.EqualTo(new Tick(7).GetHashCode()));
        }
    }
}
