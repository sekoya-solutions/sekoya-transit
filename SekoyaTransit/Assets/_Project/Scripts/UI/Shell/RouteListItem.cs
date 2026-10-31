#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// One row of the routes window (Issue #229): the route, its last month money and its stop and vehicle counts.
    /// </summary>
    public readonly struct RouteListItem : IEquatable<RouteListItem>
    {
        /// <summary>
        /// Initializes a row. <paramref name="hasLastMonth"/> is false until the route lived through a month change.
        /// </summary>
        public RouteListItem(
            int routeId,
            string name,
            bool isValid,
            bool hasLastMonth,
            long lastMonthRunningCost,
            long lastMonthRevenue,
            int stopCount,
            int vehicleCount)
        {
            RouteId = routeId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsValid = isValid;
            HasLastMonth = hasLastMonth;
            LastMonthRunningCost = lastMonthRunningCost;
            LastMonthRevenue = lastMonthRevenue;
            StopCount = stopCount;
            VehicleCount = vehicleCount;
        }

        public int RouteId { get; }

        public string Name { get; }

        public bool IsValid { get; }

        public bool HasLastMonth { get; }

        public long LastMonthRunningCost { get; }

        public long LastMonthRevenue { get; }

        public long LastMonthProfit => LastMonthRevenue - LastMonthRunningCost;

        public int StopCount { get; }

        public int VehicleCount { get; }

        public bool Equals(RouteListItem other)
        {
            return RouteId == other.RouteId
                && Name == other.Name
                && IsValid == other.IsValid
                && HasLastMonth == other.HasLastMonth
                && LastMonthRunningCost == other.LastMonthRunningCost
                && LastMonthRevenue == other.LastMonthRevenue
                && StopCount == other.StopCount
                && VehicleCount == other.VehicleCount;
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteListItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RouteId;
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ LastMonthRunningCost.GetHashCode();
                hash = (hash * 397) ^ LastMonthRevenue.GetHashCode();
                hash = (hash * 397) ^ StopCount;
                hash = (hash * 397) ^ VehicleCount;
                return hash;
            }
        }
    }
}
