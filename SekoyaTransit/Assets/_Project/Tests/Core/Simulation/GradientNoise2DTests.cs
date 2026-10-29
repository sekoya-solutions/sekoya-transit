#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class GradientNoise2DTests
    {
        [Test]
        public void Sample_ReturnsZeroAtIntegerLatticePoints()
        {
            long[] coordinates = { -2147483648L, -10, -1, 0, 1, 10, 2147483647L };

            foreach (long x in coordinates)
            {
                foreach (long y in coordinates)
                {
                    FixedPoint value = GradientNoise2D.Sample(
                        123456789UL,
                        FixedPoint.FromInt64(x),
                        FixedPoint.FromInt64(y));

                    Assert.That(value, Is.EqualTo(FixedPoint.Zero));
                }
            }
        }

        [Test]
        public void Sample_ProducesStableKnownAnswers()
        {
            Assert.That(
                GradientNoise2D.Sample(
                    0UL,
                    FixedPoint.FromRaw(FixedPoint.OneRawValue / 4),
                    FixedPoint.FromRaw(FixedPoint.OneRawValue / 2)).RawValue,
                Is.EqualTo(890740838L));
            Assert.That(
                GradientNoise2D.Sample(
                    123456789UL,
                    FixedPoint.FromRaw(-FixedPoint.OneRawValue / 3),
                    FixedPoint.FromRaw(5 * FixedPoint.OneRawValue / 4)).RawValue,
                Is.EqualTo(-584351416L));
            Assert.That(
                GradientNoise2D.Sample(
                    ulong.MaxValue,
                    FixedPoint.FromRaw(7 * FixedPoint.OneRawValue / 3),
                    FixedPoint.FromRaw(-11 * FixedPoint.OneRawValue / 5)).RawValue,
                Is.EqualTo(1397585609L));
        }

        [Test]
        public void Sample_IsRepeatableAndUsesSeedAndCoordinates()
        {
            FixedPoint x = FixedPoint.FromRaw(FixedPoint.OneRawValue / 3);
            FixedPoint y = FixedPoint.FromRaw(-7 * FixedPoint.OneRawValue / 4);

            FixedPoint original = GradientNoise2D.Sample(42UL, x, y);

            Assert.That(GradientNoise2D.Sample(42UL, x, y), Is.EqualTo(original));
            Assert.That(GradientNoise2D.Sample(43UL, x, y), Is.Not.EqualTo(original));
            Assert.That(GradientNoise2D.Sample(42UL, x + FixedPoint.One, y), Is.Not.EqualTo(original));
        }

        [Test]
        public void Sample_StaysWithinRangeForPositiveAndNegativeCoordinates()
        {
            ulong[] seeds = { 0UL, 1UL, 987654321UL, ulong.MaxValue };

            foreach (ulong seed in seeds)
            {
                for (int x = -16; x <= 16; x++)
                {
                    for (int y = -16; y <= 16; y++)
                    {
                        for (int fraction = 0; fraction < 4; fraction++)
                        {
                            FixedPoint coordinateX = FixedPoint.FromRaw(
                                ((long)x * FixedPoint.OneRawValue) + ((long)fraction * FixedPoint.OneRawValue / 4));
                            FixedPoint coordinateY = FixedPoint.FromRaw(
                                ((long)y * FixedPoint.OneRawValue) + ((long)(3 - fraction) * FixedPoint.OneRawValue / 4));
                            FixedPoint value = GradientNoise2D.Sample(seed, coordinateX, coordinateY);

                            Assert.That(value, Is.GreaterThanOrEqualTo(-FixedPoint.One));
                            Assert.That(value, Is.LessThanOrEqualTo(FixedPoint.One));
                        }
                    }
                }
            }
        }
    }
}
