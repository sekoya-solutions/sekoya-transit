#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Data-driven company cash, placeholder vehicle purchase prices, and monthly upkeep.
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>
        /// Initializes economy settings.
        /// </summary>
        public EconomySettings(
            long startingCash,
            long roadVehiclePurchase,
            long railLocomotivePurchase,
            long railWagonPurchase,
            long roadVehicleUpkeepPerMonth,
            long railLocomotiveUpkeepPerMonth,
            long railWagonUpkeepPerMonth)
        {
            if (startingCash < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingCash),
                    startingCash,
                    "Starting cash cannot be negative.");
            }

            if (roadVehiclePurchase < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roadVehiclePurchase),
                    roadVehiclePurchase,
                    "Road vehicle purchase price must be positive.");
            }

            if (railLocomotivePurchase < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(railLocomotivePurchase),
                    railLocomotivePurchase,
                    "Rail locomotive purchase price must be positive.");
            }

            if (railWagonPurchase < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(railWagonPurchase),
                    railWagonPurchase,
                    "Rail wagon purchase price must be positive.");
            }

            if (roadVehicleUpkeepPerMonth < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roadVehicleUpkeepPerMonth),
                    roadVehicleUpkeepPerMonth,
                    "Road vehicle upkeep cannot be negative.");
            }

            if (railLocomotiveUpkeepPerMonth < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(railLocomotiveUpkeepPerMonth),
                    railLocomotiveUpkeepPerMonth,
                    "Rail locomotive upkeep cannot be negative.");
            }

            if (railWagonUpkeepPerMonth < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(railWagonUpkeepPerMonth),
                    railWagonUpkeepPerMonth,
                    "Rail wagon upkeep cannot be negative.");
            }

            StartingCash = startingCash;
            RoadVehiclePurchase = roadVehiclePurchase;
            RailLocomotivePurchase = railLocomotivePurchase;
            RailWagonPurchase = railWagonPurchase;
            RoadVehicleUpkeepPerMonth = roadVehicleUpkeepPerMonth;
            RailLocomotiveUpkeepPerMonth = railLocomotiveUpkeepPerMonth;
            RailWagonUpkeepPerMonth = railWagonUpkeepPerMonth;
        }

        /// <summary>
        /// Creates the Issue #61 placeholder prices used when content is unavailable.
        /// </summary>
        public static EconomySettings CreatePlaceholder()
        {
            return new EconomySettings(
                100000L,
                8000L,
                20000L,
                4000L,
                200L,
                400L,
                100L);
        }

        /// <summary>
        /// Gets company cash granted to a new world.
        /// </summary>
        public long StartingCash { get; }

        /// <summary>
        /// Gets the placeholder truck purchase price.
        /// </summary>
        public long RoadVehiclePurchase { get; }

        /// <summary>
        /// Gets the placeholder locomotive purchase price.
        /// </summary>
        public long RailLocomotivePurchase { get; }

        /// <summary>
        /// Gets the placeholder price to attach one wagon.
        /// </summary>
        public long RailWagonPurchase { get; }

        /// <summary>
        /// Gets fixed monthly upkeep per owned truck.
        /// </summary>
        public long RoadVehicleUpkeepPerMonth { get; }

        /// <summary>
        /// Gets fixed monthly upkeep per owned locomotive.
        /// </summary>
        public long RailLocomotiveUpkeepPerMonth { get; }

        /// <summary>
        /// Gets fixed monthly upkeep per attached wagon.
        /// </summary>
        public long RailWagonUpkeepPerMonth { get; }

        /// <summary>
        /// Returns half of <paramref name="purchasePrice"/> using integer division.
        /// </summary>
        public static long HalfRefund(long purchasePrice)
        {
            if (purchasePrice < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(purchasePrice),
                    purchasePrice,
                    "Purchase price cannot be negative.");
            }

            return purchasePrice / 2L;
        }

        /// <summary>
        /// Returns monthly upkeep for a locomotive plus <paramref name="wagonCount"/> wagons.
        /// </summary>
        public long MonthlyUpkeepForRail(int wagonCount)
        {
            if (wagonCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count cannot be negative.");
            }

            return checked(RailLocomotiveUpkeepPerMonth + (RailWagonUpkeepPerMonth * wagonCount));
        }

        /// <summary>
        /// Returns the sell refund for a locomotive plus <paramref name="wagonCount"/> wagons.
        /// </summary>
        public long SellRefundForRail(int wagonCount)
        {
            if (wagonCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count cannot be negative.");
            }

            return checked(
                HalfRefund(RailLocomotivePurchase) + (HalfRefund(RailWagonPurchase) * wagonCount));
        }
    }
}
