#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Distance-based delivery pay (Issue #73). A vehicle remembers the station where its current load started; a
    /// delivery pays price × units × distance ÷ reference distance, where the distance is the Manhattan distance in
    /// tiles between that station and the delivery station (at least the minimum distance). The fraction below one
    /// money unit is carried on the vehicle to its next delivery, so pay never depends on how unloading is split
    /// across ticks.
    /// </summary>
    public static class DeliveryPay
    {
        /// <summary>
        /// Returns the paid distance in tiles: the Manhattan distance from <paramref name="origin"/> to
        /// <paramref name="destination"/>, or the minimum distance when the origin is unknown or closer.
        /// </summary>
        public static int ResolveDistance(
            TileCoordinate? origin,
            TileCoordinate destination,
            DeliveryPaySettings settings)
        {
#pragma warning disable CA1510
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
#pragma warning restore CA1510

            if (!origin.HasValue)
            {
                return settings.MinimumDistance;
            }

            int distance = Math.Abs(origin.Value.X - destination.X) + Math.Abs(origin.Value.Y - destination.Y);
            return distance < settings.MinimumDistance ? settings.MinimumDistance : distance;
        }

        /// <summary>
        /// Returns the money paid for <paramref name="amount"/> units carried <paramref name="distance"/> tiles and
        /// updates <paramref name="remainder"/>, the unpaid fraction in units of one reference distance. Flat pay
        /// returns price × amount and leaves the remainder unchanged.
        /// </summary>
        public static long Compute(
            int pricePerUnit,
            int amount,
            int distance,
            DeliveryPaySettings settings,
            ref int remainder)
        {
#pragma warning disable CA1510
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
#pragma warning restore CA1510

            if (pricePerUnit < 0 || amount < 0 || distance < 0 || remainder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Pay inputs cannot be negative.");
            }

            if (settings.IsFlat)
            {
                return (long)pricePerUnit * amount;
            }

            long numerator = checked(((long)pricePerUnit * amount * distance) + remainder);
            remainder = (int)(numerator % settings.ReferenceDistance);
            return numerator / settings.ReferenceDistance;
        }

        /// <summary>
        /// Records the station a road vehicle is servicing as the start of its load when it is still empty.
        /// </summary>
        internal static void NoteLoadStart(GeneratedWorld world, RoadVehicle vehicle)
        {
            if (!vehicle.Cargo.IsEmpty || !vehicle.TryGetCurrentOrder(out RoadVehicleOrder order))
            {
                return;
            }

            vehicle.SetCargoOriginTile(
                world.TryGetRoadStation(order.StationId, out RoadStation? station) && station != null
                    ? station.Coordinate
                    : (TileCoordinate?)null);
        }

        /// <summary>
        /// Records the station a train is servicing as the start of its load when it is still empty.
        /// </summary>
        internal static void NoteLoadStart(GeneratedWorld world, RailVehicle vehicle)
        {
            if (!vehicle.Cargo.IsEmpty || !vehicle.TryGetCurrentOrder(out RailVehicleOrder order))
            {
                return;
            }

            vehicle.SetCargoOriginTile(
                world.TryGetRailStation(order.StationId, out RailStation? station) && station != null
                    ? station.Coordinate
                    : (TileCoordinate?)null);
        }
    }
}
