#nullable enable
using System;

namespace TransportTycoon.Core.Simulation
{
    /// <summary>
    /// A signed Q32.32 fixed-point value backed by a 64-bit raw integer.
    /// Multiplication and division truncate toward zero; arithmetic overflow throws.
    /// </summary>
    public readonly struct FixedPoint : IComparable<FixedPoint>, IEquatable<FixedPoint>
    {
        /// <summary>
        /// The number of fractional bits in the Q32.32 representation.
        /// </summary>
        public const int FractionalBits = 32;

        /// <summary>
        /// The raw integer value representing one.
        /// </summary>
        public const long OneRawValue = 1L << FractionalBits;

        /// <summary>
        /// The zero fixed-point value.
        /// </summary>
        public static readonly FixedPoint Zero = new FixedPoint(0);

        /// <summary>
        /// The one fixed-point value.
        /// </summary>
        public static readonly FixedPoint One = new FixedPoint(OneRawValue);

        private readonly long _rawValue;

        private FixedPoint(long rawValue)
        {
            _rawValue = rawValue;
        }

        /// <summary>
        /// Gets the signed Q32.32 raw representation.
        /// </summary>
        public long RawValue
        {
            get
            {
                return _rawValue;
            }
        }

        /// <summary>
        /// Creates a fixed-point value from its exact raw representation.
        /// </summary>
        /// <param name="rawValue">The signed Q32.32 raw value.</param>
        /// <returns>The corresponding fixed-point value.</returns>
        public static FixedPoint FromRaw(long rawValue)
        {
            return new FixedPoint(rawValue);
        }

        /// <summary>
        /// Creates an exact integer fixed-point value.
        /// </summary>
        /// <param name="value">The integer value.</param>
        /// <returns>The corresponding fixed-point value.</returns>
        public static FixedPoint FromInt64(long value)
        {
            return new FixedPoint(checked(value * OneRawValue));
        }

        /// <summary>
        /// Converts to an integer by truncating toward zero.
        /// </summary>
        /// <returns>The whole-number component.</returns>
        public long ToInt64()
        {
            return _rawValue / OneRawValue;
        }

        /// <inheritdoc />
        public int CompareTo(FixedPoint other)
        {
            return _rawValue.CompareTo(other._rawValue);
        }

        /// <inheritdoc />
        public bool Equals(FixedPoint other)
        {
            return _rawValue == other._rawValue;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is FixedPoint other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _rawValue.GetHashCode();
        }

        /// <summary>
        /// Adds two fixed-point values.
        /// </summary>
        public static FixedPoint operator +(FixedPoint left, FixedPoint right)
        {
            return new FixedPoint(checked(left._rawValue + right._rawValue));
        }

        /// <summary>
        /// Subtracts one fixed-point value from another.
        /// </summary>
        public static FixedPoint operator -(FixedPoint left, FixedPoint right)
        {
            return new FixedPoint(checked(left._rawValue - right._rawValue));
        }

        /// <summary>
        /// Negates a fixed-point value.
        /// </summary>
        public static FixedPoint operator -(FixedPoint value)
        {
            return new FixedPoint(checked(-value._rawValue));
        }

        /// <summary>
        /// Multiplies two values, truncating the scaled result toward zero.
        /// </summary>
        public static FixedPoint operator *(FixedPoint left, FixedPoint right)
        {
            ulong leftMagnitude = GetMagnitude(left._rawValue);
            ulong rightMagnitude = GetMagnitude(right._rawValue);
            MultiplyUnsigned(leftMagnitude, rightMagnitude, out ulong high, out ulong low);

            if ((high >> FractionalBits) != 0)
            {
                throw new OverflowException("The fixed-point multiplication result is outside the representable range.");
            }

            ulong magnitude = (high << FractionalBits) | (low >> FractionalBits);
            bool isNegative = (left._rawValue < 0) != (right._rawValue < 0);
            return FromSignedMagnitude(magnitude, isNegative);
        }

        /// <summary>
        /// Divides two values, truncating the scaled result toward zero.
        /// </summary>
        public static FixedPoint operator /(FixedPoint dividend, FixedPoint divisor)
        {
            if (divisor._rawValue == 0)
            {
                throw new DivideByZeroException();
            }

            ulong numerator = GetMagnitude(dividend._rawValue);
            ulong denominator = GetMagnitude(divisor._rawValue);
            ulong low = numerator << FractionalBits;
            ulong high = numerator >> (64 - FractionalBits);
            ulong magnitude = DivideShiftedNumerator(high, low, denominator);
            bool isNegative = (dividend._rawValue < 0) != (divisor._rawValue < 0);
            return FromSignedMagnitude(magnitude, isNegative);
        }

        /// <summary>
        /// Compares two values for numeric equality.
        /// </summary>
        public static bool operator ==(FixedPoint left, FixedPoint right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two values for numeric inequality.
        /// </summary>
        public static bool operator !=(FixedPoint left, FixedPoint right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether one value is less than another.
        /// </summary>
        public static bool operator <(FixedPoint left, FixedPoint right)
        {
            return left._rawValue < right._rawValue;
        }

        /// <summary>
        /// Determines whether one value is less than or equal to another.
        /// </summary>
        public static bool operator <=(FixedPoint left, FixedPoint right)
        {
            return left._rawValue <= right._rawValue;
        }

        /// <summary>
        /// Determines whether one value is greater than another.
        /// </summary>
        public static bool operator >(FixedPoint left, FixedPoint right)
        {
            return left._rawValue > right._rawValue;
        }

        /// <summary>
        /// Determines whether one value is greater than or equal to another.
        /// </summary>
        public static bool operator >=(FixedPoint left, FixedPoint right)
        {
            return left._rawValue >= right._rawValue;
        }

        private static ulong GetMagnitude(long value)
        {
            return value < 0
                ? unchecked((ulong)(-(value + 1))) + 1UL
                : (ulong)value;
        }

        private static FixedPoint FromSignedMagnitude(ulong magnitude, bool isNegative)
        {
            ulong maximumMagnitude = isNegative ? 1UL << 63 : long.MaxValue;
            if (magnitude > maximumMagnitude)
            {
                throw new OverflowException("The fixed-point arithmetic result is outside the representable range.");
            }

            if (!isNegative)
            {
                return new FixedPoint((long)magnitude);
            }

            return magnitude == (1UL << 63)
                ? new FixedPoint(long.MinValue)
                : new FixedPoint(-(long)magnitude);
        }

        private static void MultiplyUnsigned(ulong left, ulong right, out ulong high, out ulong low)
        {
            ulong leftLow = (uint)left;
            ulong leftHigh = left >> 32;
            ulong rightLow = (uint)right;
            ulong rightHigh = right >> 32;

            ulong lowProduct = leftLow * rightLow;
            ulong lowWord = lowProduct;
            ulong carry = 0;

            ulong crossProduct = leftLow * rightHigh;
            ulong addend = crossProduct << 32;
            lowWord = unchecked(lowWord + addend);
            if (lowWord < addend)
            {
                carry++;
            }

            ulong otherCrossProduct = leftHigh * rightLow;
            addend = otherCrossProduct << 32;
            lowWord = unchecked(lowWord + addend);
            if (lowWord < addend)
            {
                carry++;
            }

            high = unchecked(
                (leftHigh * rightHigh)
                + (crossProduct >> 32)
                + (otherCrossProduct >> 32)
                + carry);
            low = lowWord;
        }

        private static ulong DivideShiftedNumerator(ulong high, ulong low, ulong denominator)
        {
            ulong quotient = 0;
            ulong remainder = 0;

            for (int bitIndex = 95; bitIndex >= 0; bitIndex--)
            {
                ulong nextBit = bitIndex >= 64
                    ? (high >> (bitIndex - 64)) & 1UL
                    : (low >> bitIndex) & 1UL;
                remainder = (remainder << 1) | nextBit;

                if (remainder >= denominator)
                {
                    remainder -= denominator;
                    if (bitIndex >= 64)
                    {
                        throw new OverflowException(
                            "The fixed-point division result is outside the representable range.");
                    }

                    quotient |= 1UL << bitIndex;
                }
            }

            return quotient;
        }
    }
}
