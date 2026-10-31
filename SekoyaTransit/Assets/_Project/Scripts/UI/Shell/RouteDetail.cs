#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The transport mode of a route as the UI sees it (Issue #229).
    /// </summary>
    public enum RouteUiMode
    {
        None = 0,
        Road = 1,
        Rail = 2
    }

    /// <summary>
    /// One stop card of the station window (Issue #229).
    /// </summary>
    public readonly struct RouteStopItem : IEquatable<RouteStopItem>
    {
        public RouteStopItem(int stationId, bool waitForFull, bool canMoveUp, bool canMoveDown, bool removeNeedsConfirmation)
        {
            StationId = stationId;
            WaitForFull = waitForFull;
            CanMoveUp = canMoveUp;
            CanMoveDown = canMoveDown;
            RemoveNeedsConfirmation = removeNeedsConfirmation;
        }

        public int StationId { get; }

        public bool WaitForFull { get; }

        public bool CanMoveUp { get; }

        public bool CanMoveDown { get; }

        /// <summary>
        /// Gets whether removing this stop would leave the route invalid while vehicles run on it.
        /// </summary>
        public bool RemoveNeedsConfirmation { get; }

        public bool Equals(RouteStopItem other)
        {
            return StationId == other.StationId
                && WaitForFull == other.WaitForFull
                && CanMoveUp == other.CanMoveUp
                && CanMoveDown == other.CanMoveDown
                && RemoveNeedsConfirmation == other.RemoveNeedsConfirmation;
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteStopItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StationId * 397) ^ (WaitForFull ? 1 : 0) ^ (CanMoveUp ? 2 : 0) ^ (CanMoveDown ? 4 : 0);
            }
        }
    }

    /// <summary>
    /// A vehicle shown in the vehicle window: on the route, or unassigned and ready to add (Issue #229).
    /// </summary>
    public readonly struct RouteVehicleItem : IEquatable<RouteVehicleItem>
    {
        public RouteVehicleItem(int vehicleId, string typeNameKey)
        {
            VehicleId = vehicleId;
            TypeNameKey = typeNameKey ?? throw new ArgumentNullException(nameof(typeNameKey));
        }

        public int VehicleId { get; }

        public string TypeNameKey { get; }

        public bool Equals(RouteVehicleItem other)
        {
            return VehicleId == other.VehicleId && TypeNameKey == other.TypeNameKey;
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteVehicleItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (VehicleId * 397) ^ TypeNameKey.GetHashCode();
        }
    }

    /// <summary>
    /// Everything the station and vehicle windows show about one route (Issue #229).
    /// </summary>
    public sealed class RouteDetail
    {
        public RouteDetail(
            int routeId,
            string name,
            RouteUiMode mode,
            bool isValid,
            IReadOnlyList<RouteStopItem> stops,
            IReadOnlyList<RouteVehicleItem> vehicles,
            IReadOnlyList<RouteVehicleItem> unassignedVehicles,
            IReadOnlyList<VehiclePurchaseOption> buyOptions)
        {
            RouteId = routeId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Mode = mode;
            IsValid = isValid;
            Stops = stops ?? throw new ArgumentNullException(nameof(stops));
            Vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            UnassignedVehicles = unassignedVehicles ?? throw new ArgumentNullException(nameof(unassignedVehicles));
            BuyOptions = buyOptions ?? throw new ArgumentNullException(nameof(buyOptions));
        }

        public int RouteId { get; }

        public string Name { get; }

        public RouteUiMode Mode { get; }

        public bool IsValid { get; }

        public IReadOnlyList<RouteStopItem> Stops { get; }

        public IReadOnlyList<RouteVehicleItem> Vehicles { get; }

        /// <summary>
        /// Gets the unassigned vehicles of the route's mode (empty while the route has no mode).
        /// </summary>
        public IReadOnlyList<RouteVehicleItem> UnassignedVehicles { get; }

        /// <summary>
        /// Gets the unlocked vehicle types of the route's mode, with the price of buying one for the route.
        /// </summary>
        public IReadOnlyList<VehiclePurchaseOption> BuyOptions { get; }

        /// <summary>
        /// Returns a value that changes whenever anything shown changes.
        /// </summary>
        public int ComputeFingerprint()
        {
            unchecked
            {
                int hash = RouteId;
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ (int)Mode;
                hash = (hash * 397) ^ (IsValid ? 1 : 0);
                hash = (hash * 397) ^ Stops.Count;
                for (int index = 0; index < Stops.Count; index++)
                {
                    hash = (hash * 397) ^ Stops[index].GetHashCode();
                    hash = (hash * 397) ^ (Stops[index].RemoveNeedsConfirmation ? 8 : 0);
                }

                hash = (hash * 397) ^ Vehicles.Count;
                for (int index = 0; index < Vehicles.Count; index++)
                {
                    hash = (hash * 397) ^ Vehicles[index].GetHashCode();
                }

                hash = (hash * 397) ^ UnassignedVehicles.Count;
                for (int index = 0; index < UnassignedVehicles.Count; index++)
                {
                    hash = (hash * 397) ^ UnassignedVehicles[index].GetHashCode();
                }

                hash = (hash * 397) ^ BuyOptions.Count;
                for (int index = 0; index < BuyOptions.Count; index++)
                {
                    hash = (hash * 397) ^ BuyOptions[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
