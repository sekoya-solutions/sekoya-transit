#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class Pcg32RandomTests
    {
        private static readonly uint[] _canonicalVector =
        {
            0xa15c02b7U,
            0x7b47f409U,
            0xba1d3330U,
            0x83d2f293U,
            0xbfa4784bU,
            0xcbed606eU,
        };

        [Test]
        public void NextUInt32_ProducesCanonicalPcg32KnownAnswerVector()
        {
            var random = new Pcg32Random(42, 54);

            foreach (uint expected in _canonicalVector)
            {
                Assert.That(random.NextUInt32(), Is.EqualTo(expected));
            }
        }

        [Test]
        public void NextUInt32_ProducesRepeatableValuesForEqualSeedAndSequence()
        {
            var first = new Pcg32Random(123456789, 987654321);
            var second = new Pcg32Random(123456789, 987654321);

            for (int index = 0; index < 100; index++)
            {
                Assert.That(first.NextUInt32(), Is.EqualTo(second.NextUInt32()));
            }
        }

        [Test]
        public void CapturedState_RestoresSubsequentValues()
        {
            var original = new Pcg32Random(123456789, 987654321);
            for (int index = 0; index < 10; index++)
            {
                original.NextUInt32();
            }

            var restored = new Pcg32Random(original.CapturedState);

            for (int index = 0; index < 100; index++)
            {
                Assert.That(restored.NextUInt32(), Is.EqualTo(original.NextUInt32()));
            }
        }

        [Test]
        public void NextInt32_ReturnsValuesWithinInclusiveExclusiveBounds()
        {
            var random = new Pcg32Random(42, 54);

            for (int index = 0; index < 1000; index++)
            {
                int value = random.NextInt32(-17, 23);

                Assert.That(value, Is.GreaterThanOrEqualTo(-17));
                Assert.That(value, Is.LessThan(23));
            }
        }

        [Test]
        public void NextInt32_SupportsTheFullInt32Range()
        {
            var random = new Pcg32Random(42, 54);

            for (int index = 0; index < 100; index++)
            {
                int value = random.NextInt32(int.MinValue, int.MaxValue);

                Assert.That(value, Is.GreaterThanOrEqualTo(int.MinValue));
                Assert.That(value, Is.LessThan(int.MaxValue));
            }
        }

        [TestCase(5, 5)]
        [TestCase(6, 5)]
        public void NextInt32_RejectsEmptyOrReversedBounds(int minInclusive, int maxExclusive)
        {
            var random = new Pcg32Random(42, 54);

            Assert.That(
                () => random.NextInt32(minInclusive, maxExclusive),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Pcg32Random_RejectsCapturedStateWithEvenIncrement()
        {
            Assert.That(
                () => new Pcg32Random(default),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Pcg32State_RejectsEvenIncrement()
        {
            Assert.That(
                () => new Pcg32State(1, 2),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
