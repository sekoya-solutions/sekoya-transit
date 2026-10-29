#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// One stop in a rail vehicle's looping order list.
    /// </summary>
    public readonly struct RailVehicleOrder : IEquatable<RailVehicleOrder>
    {
        /// <summary>
        /// Initializes an order stop.
        /// </summary>
        /// <param name="stationId">Target rail station id.</param>
        /// <param name="waitForFull">When true, remain after unload until the vehicle is full.</param>
        public RailVehicleOrder(int stationId, bool waitForFull = false)
        {
            if (stationId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stationId), stationId, "Station id cannot be negative.");
            }

            StationId = stationId;
            WaitForFull = waitForFull;
        }

        /// <summary>
        /// Gets the destination rail station id.
        /// </summary>
        public int StationId { get; }

        /// <summary>
        /// Gets whether the vehicle should wait until full before leaving this stop.
        /// </summary>
        public bool WaitForFull { get; }

        /// <inheritdoc />
        public bool Equals(RailVehicleOrder other)
        {
            return StationId == other.StationId && WaitForFull == other.WaitForFull;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RailVehicleOrder other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (StationId * 397) ^ WaitForFull.GetHashCode();
            }
        }

        /// <summary>
        /// Returns whether two orders are equal.
        /// </summary>
        public static bool operator ==(RailVehicleOrder left, RailVehicleOrder right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether two orders are not equal.
        /// </summary>
        public static bool operator !=(RailVehicleOrder left, RailVehicleOrder right)
        {
            return !left.Equals(right);
        }
    }
}
