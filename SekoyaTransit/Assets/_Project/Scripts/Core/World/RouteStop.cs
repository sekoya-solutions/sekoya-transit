#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// One stop of a route: a station of the route's mode and whether vehicles wait there until full (Issue #229).
    /// </summary>
    public readonly struct RouteStop : IEquatable<RouteStop>
    {
        /// <summary>
        /// Initializes a stop at a station.
        /// </summary>
        /// <param name="stationId">The road or rail station id, depending on the route's mode.</param>
        /// <param name="waitForFull">Whether vehicles wait at this stop until they are full.</param>
        public RouteStop(int stationId, bool waitForFull = false)
        {
            if (stationId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stationId), stationId, "Station id cannot be negative.");
            }

            StationId = stationId;
            WaitForFull = waitForFull;
        }

        /// <summary>
        /// Gets the station id.
        /// </summary>
        public int StationId { get; }

        /// <summary>
        /// Gets whether vehicles wait at this stop until they are full.
        /// </summary>
        public bool WaitForFull { get; }

        /// <inheritdoc />
        public bool Equals(RouteStop other)
        {
            return StationId == other.StationId && WaitForFull == other.WaitForFull;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RouteStop other && Equals(other);
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
        /// Compares two stops for equality.
        /// </summary>
        public static bool operator ==(RouteStop left, RouteStop right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two stops for inequality.
        /// </summary>
        public static bool operator !=(RouteStop left, RouteStop right)
        {
            return !left.Equals(right);
        }
    }
}
