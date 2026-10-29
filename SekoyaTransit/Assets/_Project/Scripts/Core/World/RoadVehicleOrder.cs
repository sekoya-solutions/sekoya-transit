#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// One stop in a road vehicle's looping order list.
    /// </summary>
    public readonly struct RoadVehicleOrder : IEquatable<RoadVehicleOrder>
    {
        /// <summary>
        /// Initializes an order stop.
        /// </summary>
        /// <param name="stationId">Target road station id.</param>
        /// <param name="waitForFull">When true, remain after unload until the vehicle is full.</param>
        public RoadVehicleOrder(int stationId, bool waitForFull = false)
        {
            if (stationId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stationId), stationId, "Station id cannot be negative.");
            }

            StationId = stationId;
            WaitForFull = waitForFull;
        }

        /// <summary>
        /// Gets the destination road station id.
        /// </summary>
        public int StationId { get; }

        /// <summary>
        /// Gets whether the vehicle should wait until full before leaving this stop.
        /// </summary>
        public bool WaitForFull { get; }

        /// <inheritdoc />
        public bool Equals(RoadVehicleOrder other)
        {
            return StationId == other.StationId && WaitForFull == other.WaitForFull;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RoadVehicleOrder other && Equals(other);
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
        public static bool operator ==(RoadVehicleOrder left, RoadVehicleOrder right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether two orders are not equal.
        /// </summary>
        public static bool operator !=(RoadVehicleOrder left, RoadVehicleOrder right)
        {
            return !left.Equals(right);
        }
    }
}
