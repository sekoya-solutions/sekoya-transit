#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A named looping list of stops shared by every vehicle assigned to it, with its running cost and delivery
    /// revenue for the current and the last calendar month (Issue #229). Vehicles point to their route with
    /// <see cref="RoadVehicle.RouteId"/> and <see cref="RailVehicle.RouteId"/>.
    /// </summary>
    public sealed class Route
    {
        /// <summary>
        /// The longest route name, in UTF-16 characters, after trimming.
        /// </summary>
        public const int MaxNameLength = 32;

        /// <summary>
        /// The fewest stops a route needs before vehicles can run on it.
        /// </summary>
        public const int MinValidStops = 2;

        private readonly List<RouteStop> _stops;
        private readonly ReadOnlyCollection<RouteStop> _stopsView;

        /// <summary>
        /// Initializes an empty route.
        /// </summary>
        public Route(int id, string name)
            : this(id, name, RouteMode.None, Array.Empty<RouteStop>(), 0L, 0L, false, 0L, 0L)
        {
        }

        /// <summary>
        /// Restores a route from persisted fields (save load and tests).
        /// </summary>
        public Route(
            int id,
            string name,
            RouteMode mode,
            IReadOnlyList<RouteStop> stops,
            long currentMonthRunningCost,
            long currentMonthRevenue,
            bool hasLastMonth,
            long lastMonthRunningCost,
            long lastMonthRevenue)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Route id cannot be negative.");
            }

            if (!IsValidName(name))
            {
                throw new ArgumentException("Route name must be trimmed, non-empty and at most 32 characters.", nameof(name));
            }

#pragma warning disable CA1510
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }
#pragma warning restore CA1510

            if (mode != RouteMode.None && mode != RouteMode.Road && mode != RouteMode.Rail)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown route mode.");
            }

            if ((mode == RouteMode.None) != (stops.Count == 0))
            {
                throw new ArgumentException("A route has a mode exactly when it has stops.", nameof(mode));
            }

            if (currentMonthRunningCost < 0L || currentMonthRevenue < 0L || lastMonthRunningCost < 0L || lastMonthRevenue < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(currentMonthRunningCost), "Route totals cannot be negative.");
            }

            if (!hasLastMonth && (lastMonthRunningCost != 0L || lastMonthRevenue != 0L))
            {
                throw new ArgumentException("Last month totals need a known last month.", nameof(hasLastMonth));
            }

            Id = id;
            Name = name;
            Mode = mode;
            _stops = new List<RouteStop>(stops);
            _stopsView = _stops.AsReadOnly();
            CurrentMonthRunningCost = currentMonthRunningCost;
            CurrentMonthRevenue = currentMonthRevenue;
            HasLastMonth = hasLastMonth;
            LastMonthRunningCost = lastMonthRunningCost;
            LastMonthRevenue = lastMonthRevenue;
        }

        /// <summary>
        /// Gets the stable route id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the player-chosen route name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the route's transport mode, or <see cref="RouteMode.None"/> while it has no stop.
        /// </summary>
        public RouteMode Mode { get; private set; }

        /// <summary>
        /// Gets the looping stop list.
        /// </summary>
        public IReadOnlyList<RouteStop> Stops
        {
            get
            {
                return _stopsView;
            }
        }

        /// <summary>
        /// Gets whether the route has enough stops for vehicles to run on it.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return _stops.Count >= MinValidStops;
            }
        }

        /// <summary>
        /// Gets the upkeep charged to this route so far this month.
        /// </summary>
        public long CurrentMonthRunningCost { get; private set; }

        /// <summary>
        /// Gets the delivery income earned by this route so far this month.
        /// </summary>
        public long CurrentMonthRevenue { get; private set; }

        /// <summary>
        /// Gets whether the route existed at a month change, so the last month totals are known.
        /// </summary>
        public bool HasLastMonth { get; private set; }

        /// <summary>
        /// Gets the upkeep charged to this route last month (zero while <see cref="HasLastMonth"/> is false).
        /// </summary>
        public long LastMonthRunningCost { get; private set; }

        /// <summary>
        /// Gets the delivery income earned by this route last month (zero while <see cref="HasLastMonth"/> is false).
        /// </summary>
        public long LastMonthRevenue { get; private set; }

        /// <summary>
        /// Gets last month's revenue minus running cost (zero while <see cref="HasLastMonth"/> is false).
        /// </summary>
        public long LastMonthProfit
        {
            get
            {
                return LastMonthRevenue - LastMonthRunningCost;
            }
        }

        /// <summary>
        /// Returns whether <paramref name="name"/> is an acceptable stored name: trimmed, non-empty and at most
        /// <see cref="MaxNameLength"/> characters.
        /// </summary>
        public static bool IsValidName(string? name)
        {
            return name != null &&
                name.Length > 0 &&
                name.Length <= MaxNameLength &&
                name.Trim().Length == name.Length;
        }

        /// <summary>
        /// Returns an independent copy that keeps every persisted field.
        /// </summary>
        public Route Copy()
        {
            return new Route(
                Id,
                Name,
                Mode,
                _stops,
                CurrentMonthRunningCost,
                CurrentMonthRevenue,
                HasLastMonth,
                LastMonthRunningCost,
                LastMonthRevenue);
        }

        /// <summary>
        /// Returns the stops as road vehicle orders.
        /// </summary>
        internal RoadVehicleOrder[] ToRoadOrders()
        {
            var orders = new RoadVehicleOrder[_stops.Count];
            for (int index = 0; index < _stops.Count; index++)
            {
                orders[index] = new RoadVehicleOrder(_stops[index].StationId, _stops[index].WaitForFull);
            }

            return orders;
        }

        /// <summary>
        /// Returns the stops as rail vehicle orders.
        /// </summary>
        internal RailVehicleOrder[] ToRailOrders()
        {
            var orders = new RailVehicleOrder[_stops.Count];
            for (int index = 0; index < _stops.Count; index++)
            {
                orders[index] = new RailVehicleOrder(_stops[index].StationId, _stops[index].WaitForFull);
            }

            return orders;
        }

        internal void Rename(string name)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException("Route name must be trimmed, non-empty and at most 32 characters.", nameof(name));
            }

            Name = name;
        }

        internal void AppendStop(RouteMode mode, RouteStop stop)
        {
            if (mode == RouteMode.None || (Mode != RouteMode.None && mode != Mode))
            {
                throw new ArgumentException("A stop must match the route's mode.", nameof(mode));
            }

            Mode = mode;
            _stops.Add(stop);
        }

        internal void SetStop(int index, RouteStop stop)
        {
            _stops[index] = stop;
        }

        internal void SwapStops(int first, int second)
        {
            RouteStop stop = _stops[first];
            _stops[first] = _stops[second];
            _stops[second] = stop;
        }

        /// <summary>
        /// Removes the stop at <paramref name="index"/>, then merges stops that became neighbours at the same station.
        /// </summary>
        internal void RemoveStopAt(int index)
        {
            _stops.RemoveAt(index);
            MergeRepeatedNeighbours();
        }

        /// <summary>
        /// Removes every stop at <paramref name="stationId"/>, then merges stops that became neighbours at the same
        /// station.
        /// </summary>
        /// <returns>Whether any stop was removed.</returns>
        internal bool RemoveStation(int stationId)
        {
            int removed = _stops.RemoveAll(stop => stop.StationId == stationId);
            if (removed == 0)
            {
                return false;
            }

            MergeRepeatedNeighbours();
            return true;
        }

        internal void AddRunningCost(long amount)
        {
            CurrentMonthRunningCost = checked(CurrentMonthRunningCost + amount);
        }

        internal void AddRevenue(long amount)
        {
            CurrentMonthRevenue = checked(CurrentMonthRevenue + amount);
        }

        /// <summary>
        /// Moves this month's totals to last month and starts a new month at zero.
        /// </summary>
        internal void CloseMonth()
        {
            LastMonthRunningCost = CurrentMonthRunningCost;
            LastMonthRevenue = CurrentMonthRevenue;
            HasLastMonth = true;
            CurrentMonthRunningCost = 0L;
            CurrentMonthRevenue = 0L;
        }

        // Two neighbouring stops at the same station would make vehicles service it twice in a row; the first one is
        // kept, waiting until full when either did. An emptied route forgets its mode so it accepts any station again.
        private void MergeRepeatedNeighbours()
        {
            for (int index = _stops.Count - 1; index > 0; index--)
            {
                if (_stops[index].StationId == _stops[index - 1].StationId)
                {
                    bool waitForFull = _stops[index].WaitForFull || _stops[index - 1].WaitForFull;
                    _stops[index - 1] = new RouteStop(_stops[index].StationId, waitForFull);
                    _stops.RemoveAt(index);
                }
            }

            if (_stops.Count == 0)
            {
                Mode = RouteMode.None;
            }
        }
    }
}
