#nullable enable

namespace SekoyaTransit.Core.Simulation
{
    /// <summary>
    /// Samples deterministic two-dimensional gradient noise using Q32.32 arithmetic.
    /// </summary>
    public static class GradientNoise2D
    {
        private const long _diagonalComponentRaw = 3037000500L;

        private const ulong _xHashMultiplier = 0x9E3779B97F4A7C15UL;
        private const ulong _yHashMultiplier = 0xD1B54A32D192ED03UL;

        /// <summary>
        /// Samples the noise field at a Q32.32 coordinate.
        /// Scale the coordinates before sampling to control feature size.
        /// </summary>
        /// <param name="seed">The seed for deterministic lattice gradients.</param>
        /// <param name="x">The horizontal coordinate in lattice units.</param>
        /// <param name="y">The vertical coordinate in lattice units.</param>
        /// <returns>A value in the inclusive range [-1, 1].</returns>
        public static FixedPoint Sample(ulong seed, FixedPoint x, FixedPoint y)
        {
            SplitCoordinate(x, out long latticeX, out FixedPoint fractionX);
            SplitCoordinate(y, out long latticeY, out FixedPoint fractionY);

            FixedPoint lowerLeft = DotGradient(seed, latticeX, latticeY, fractionX, fractionY);
            FixedPoint lowerRight = DotGradient(seed, latticeX + 1, latticeY, fractionX - FixedPoint.One, fractionY);
            FixedPoint upperLeft = DotGradient(seed, latticeX, latticeY + 1, fractionX, fractionY - FixedPoint.One);
            FixedPoint upperRight = DotGradient(
                seed,
                latticeX + 1,
                latticeY + 1,
                fractionX - FixedPoint.One,
                fractionY - FixedPoint.One);

            FixedPoint fadeX = Fade(fractionX);
            FixedPoint fadeY = Fade(fractionY);
            FixedPoint lower = Lerp(lowerLeft, lowerRight, fadeX);
            FixedPoint upper = Lerp(upperLeft, upperRight, fadeX);
            FixedPoint result = Lerp(lower, upper, fadeY);

            if (result.RawValue > FixedPoint.OneRawValue)
            {
                return FixedPoint.One;
            }

            if (result.RawValue < -FixedPoint.OneRawValue)
            {
                return -FixedPoint.One;
            }

            return result;
        }

        private static FixedPoint DotGradient(
            ulong seed,
            long latticeX,
            long latticeY,
            FixedPoint offsetX,
            FixedPoint offsetY)
        {
            GetGradient(seed, latticeX, latticeY, out FixedPoint gradientX, out FixedPoint gradientY);
            return (gradientX * offsetX) + (gradientY * offsetY);
        }

        private static void GetGradient(
            ulong seed,
            long latticeX,
            long latticeY,
            out FixedPoint gradientX,
            out FixedPoint gradientY)
        {
            ulong hash = unchecked(seed ^ ((ulong)latticeX * _xHashMultiplier));
            hash = Mix(hash);
            hash = Mix(unchecked(hash ^ ((ulong)latticeY * _yHashMultiplier)));

            switch ((int)(hash & 7UL))
            {
                case 0:
                    gradientX = FixedPoint.One;
                    gradientY = FixedPoint.Zero;
                    break;
                case 1:
                    gradientX = -FixedPoint.One;
                    gradientY = FixedPoint.Zero;
                    break;
                case 2:
                    gradientX = FixedPoint.Zero;
                    gradientY = FixedPoint.One;
                    break;
                case 3:
                    gradientX = FixedPoint.Zero;
                    gradientY = -FixedPoint.One;
                    break;
                case 4:
                    gradientX = FixedPoint.FromRaw(_diagonalComponentRaw);
                    gradientY = FixedPoint.FromRaw(_diagonalComponentRaw);
                    break;
                case 5:
                    gradientX = FixedPoint.FromRaw(-_diagonalComponentRaw);
                    gradientY = FixedPoint.FromRaw(_diagonalComponentRaw);
                    break;
                case 6:
                    gradientX = FixedPoint.FromRaw(_diagonalComponentRaw);
                    gradientY = FixedPoint.FromRaw(-_diagonalComponentRaw);
                    break;
                default:
                    gradientX = FixedPoint.FromRaw(-_diagonalComponentRaw);
                    gradientY = FixedPoint.FromRaw(-_diagonalComponentRaw);
                    break;
            }
        }

        private static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value = unchecked(value * 0xBF58476D1CE4E5B9UL);
            value ^= value >> 27;
            value = unchecked(value * 0x94D049BB133111EBUL);
            return value ^ (value >> 31);
        }

        private static void SplitCoordinate(FixedPoint coordinate, out long lattice, out FixedPoint fraction)
        {
            lattice = coordinate.RawValue >> FixedPoint.FractionalBits;
            long fractionRaw = coordinate.RawValue & (FixedPoint.OneRawValue - 1);
            fraction = FixedPoint.FromRaw(fractionRaw);
        }

        private static FixedPoint Fade(FixedPoint value)
        {
            FixedPoint six = FixedPoint.FromInt64(6);
            FixedPoint fifteen = FixedPoint.FromInt64(15);
            FixedPoint ten = FixedPoint.FromInt64(10);
            FixedPoint squared = value * value;
            FixedPoint cubed = squared * value;
            FixedPoint curve = ((six * value - fifteen) * value + ten);
            FixedPoint result = cubed * curve;

            if (result.RawValue < 0)
            {
                return FixedPoint.Zero;
            }

            if (result.RawValue > FixedPoint.OneRawValue)
            {
                return FixedPoint.One;
            }

            return result;
        }

        private static FixedPoint Lerp(FixedPoint from, FixedPoint to, FixedPoint amount)
        {
            return from + ((to - from) * amount);
        }
    }
}
