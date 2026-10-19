#nullable enable
using System;

namespace TransportTycoon.Core.Simulation
{
    /// <summary>
    /// A discrete point in simulation time. The simulation advances by whole ticks,
    /// independently of the rendering frame rate (see ADR 0003).
    /// </summary>
    public readonly struct Tick : IEquatable<Tick>
    {
        public static readonly Tick Zero = new Tick(0);

        public Tick(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A tick cannot be negative.");
            }

            Value = value;
        }

        public long Value { get; }

        public Tick Next()
        {
            return new Tick(checked(Value + 1));
        }

        public bool Equals(Tick other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is Tick other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool operator ==(Tick left, Tick right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Tick left, Tick right)
        {
            return !left.Equals(right);
        }
    }
}
