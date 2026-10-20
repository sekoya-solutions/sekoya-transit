#nullable enable
using System;
using System.Numerics;
using NUnit.Framework;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class FixedPointTests
    {
        [Test]
        public void FromInt64_CreatesExactIntegerRepresentation()
        {
            FixedPoint value = FixedPoint.FromInt64(-12);

            Assert.That(value.RawValue, Is.EqualTo(-12L * FixedPoint.OneRawValue));
            Assert.That(value.ToInt64(), Is.EqualTo(-12));
        }

        [Test]
        public void FromRaw_PreservesFractionalValue()
        {
            FixedPoint value = FixedPoint.FromRaw(FixedPoint.OneRawValue / 4);

            Assert.That(value.RawValue, Is.EqualTo(FixedPoint.OneRawValue / 4));
            Assert.That(value.ToInt64(), Is.EqualTo(0));
        }

        [Test]
        public void Arithmetic_AddsAndSubtractsRawValuesExactly()
        {
            FixedPoint left = FixedPoint.FromInt64(8);
            FixedPoint right = FixedPoint.FromInt64(3);

            Assert.That((left + right).ToInt64(), Is.EqualTo(11));
            Assert.That((left - right).ToInt64(), Is.EqualTo(5));
        }

        [Test]
        public void Arithmetic_MultipliesAndDividesExactValues()
        {
            FixedPoint oneAndHalf = FixedPoint.FromRaw(FixedPoint.OneRawValue + (FixedPoint.OneRawValue / 2));
            FixedPoint half = FixedPoint.FromRaw(FixedPoint.OneRawValue / 2);

            Assert.That((oneAndHalf * half).RawValue, Is.EqualTo(FixedPoint.OneRawValue * 3 / 4));
            Assert.That((FixedPoint.FromInt64(7) / FixedPoint.FromInt64(2)).RawValue, Is.EqualTo(FixedPoint.OneRawValue * 7 / 2));
        }

        [Test]
        public void Arithmetic_MultiplicationAndDivisionTruncateTowardZero()
        {
            FixedPoint positiveProduct = FixedPoint.FromRaw(1) * FixedPoint.FromRaw(FixedPoint.OneRawValue / 2);
            FixedPoint negativeProduct = FixedPoint.FromRaw(-1) * FixedPoint.FromRaw(FixedPoint.OneRawValue / 2);
            FixedPoint positiveQuotient = FixedPoint.FromRaw(1) / FixedPoint.FromInt64(2);
            FixedPoint negativeQuotient = FixedPoint.FromRaw(-1) / FixedPoint.FromInt64(2);

            Assert.That(positiveProduct.RawValue, Is.EqualTo(0));
            Assert.That(negativeProduct.RawValue, Is.EqualTo(0));
            Assert.That(positiveQuotient.RawValue, Is.EqualTo(0));
            Assert.That(negativeQuotient.RawValue, Is.EqualTo(0));
        }

        [Test]
        public void Arithmetic_HandlesMinimumRawValueMagnitude()
        {
            FixedPoint minimum = FixedPoint.FromRaw(long.MinValue);

            Assert.That((minimum / FixedPoint.One).RawValue, Is.EqualTo(long.MinValue));
            Assert.That((-FixedPoint.FromRaw(long.MinValue + 1)).RawValue, Is.EqualTo(long.MaxValue));
            Assert.That((minimum * FixedPoint.One).RawValue, Is.EqualTo(long.MinValue));
            Assert.That((FixedPoint.FromRaw(long.MaxValue) * FixedPoint.One).RawValue, Is.EqualTo(long.MaxValue));
            Assert.That(
                () => minimum / FixedPoint.FromInt64(-1),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Arithmetic_ThrowsWhenAdditionOrSubtractionOverflows()
        {
            Assert.That(
                () => FixedPoint.FromRaw(long.MaxValue) + FixedPoint.FromRaw(1),
                Throws.TypeOf<OverflowException>());
            Assert.That(
                () => FixedPoint.FromRaw(long.MinValue) - FixedPoint.FromRaw(1),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Arithmetic_ThrowsWhenUnaryNegationOverflows()
        {
            Assert.That(
                () => -FixedPoint.FromRaw(long.MinValue),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Arithmetic_ThrowsWhenMultiplicationOverflows()
        {
            Assert.That(
                () => FixedPoint.FromRaw(long.MaxValue) * FixedPoint.FromInt64(2),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Arithmetic_ThrowsWhenDivisionOverflows()
        {
            Assert.That(
                () => FixedPoint.FromRaw(long.MinValue) / FixedPoint.FromRaw(1),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Arithmetic_MatchesWideIntegerReferenceForBoundaryOperands()
        {
            FixedPoint[] operands =
            {
                FixedPoint.FromRaw(long.MinValue),
                FixedPoint.FromRaw(long.MinValue + 1),
                FixedPoint.FromRaw(-0x123456789abcdefL),
                FixedPoint.FromRaw(-1),
                FixedPoint.Zero,
                FixedPoint.FromRaw(1),
                FixedPoint.FromRaw(0x123456789abcdefL),
                FixedPoint.FromRaw(long.MaxValue - 1),
                FixedPoint.FromRaw(long.MaxValue),
            };

            foreach (FixedPoint left in operands)
            {
                foreach (FixedPoint right in operands)
                {
                    AssertMatchesWideIntegerMultiplication(left, right);
                    if (right.RawValue == 0)
                    {
                        Assert.That(
                            () => left / right,
                            Throws.TypeOf<DivideByZeroException>());
                    }
                    else
                    {
                        AssertMatchesWideIntegerDivision(left, right);
                    }
                }
            }
        }

        [Test]
        public void Arithmetic_ThrowsWhenDividingByZero()
        {
            Assert.That(
                () => FixedPoint.One / FixedPoint.Zero,
                Throws.TypeOf<DivideByZeroException>());
        }

        [Test]
        public void FromInt64_ThrowsWhenIntegerCannotBeRepresented()
        {
            Assert.That(
                () => FixedPoint.FromInt64(2147483648),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void FixedPoint_EqualityAndComparisonUseNumericOrder()
        {
            FixedPoint one = FixedPoint.One;
            FixedPoint two = FixedPoint.FromInt64(2);

            Assert.That(one, Is.EqualTo(FixedPoint.FromInt64(1)));
            Assert.That(one == FixedPoint.FromInt64(1), Is.True);
            Assert.That(one != two, Is.True);
            Assert.That(one < two, Is.True);
            Assert.That(one <= FixedPoint.FromInt64(1), Is.True);
            Assert.That(two > one, Is.True);
            Assert.That(two >= FixedPoint.FromInt64(2), Is.True);
            Assert.That(one.CompareTo(two), Is.LessThan(0));
        }

        private static void AssertMatchesWideIntegerMultiplication(FixedPoint left, FixedPoint right)
        {
            BigInteger expected = (new BigInteger(left.RawValue) * right.RawValue) / FixedPoint.OneRawValue;
            if (expected < long.MinValue || expected > long.MaxValue)
            {
                Assert.That(
                    () => left * right,
                    Throws.TypeOf<OverflowException>());
                return;
            }

            Assert.That((left * right).RawValue, Is.EqualTo((long)expected));
        }

        private static void AssertMatchesWideIntegerDivision(FixedPoint dividend, FixedPoint divisor)
        {
            BigInteger expected = (new BigInteger(dividend.RawValue) * FixedPoint.OneRawValue) / divisor.RawValue;
            if (expected < long.MinValue || expected > long.MaxValue)
            {
                Assert.That(
                    () => dividend / divisor,
                    Throws.TypeOf<OverflowException>());
                return;
            }

            Assert.That((dividend / divisor).RawValue, Is.EqualTo((long)expected));
        }
    }
}
