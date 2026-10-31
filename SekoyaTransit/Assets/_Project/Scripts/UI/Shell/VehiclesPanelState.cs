#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// What the vehicles panel needs besides its rows (Issue #229): whether a station is selected on the map (the list
    /// then shows the vehicles of the routes that stop there, with a "Manage routes" button), and the wagon price.
    /// </summary>
    public readonly struct VehiclesPanelState : IEquatable<VehiclesPanelState>
    {
        public VehiclesPanelState(bool hasSelectedStation, long cash, long wagonPurchasePrice)
        {
            if (cash < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(cash), cash, "Cash cannot be negative.");
            }

            if (wagonPurchasePrice < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonPurchasePrice),
                    wagonPurchasePrice,
                    "Wagon purchase price must be positive.");
            }

            HasSelectedStation = hasSelectedStation;
            Cash = cash;
            WagonPurchasePrice = wagonPurchasePrice;
        }

        public bool HasSelectedStation { get; }

        public long Cash { get; }

        public long WagonPurchasePrice { get; }

        public bool CanAffordWagon => Cash >= WagonPurchasePrice;

        public bool Equals(VehiclesPanelState other)
        {
            return HasSelectedStation == other.HasSelectedStation
                && Cash == other.Cash
                && WagonPurchasePrice == other.WagonPurchasePrice;
        }

        public override bool Equals(object? obj)
        {
            return obj is VehiclesPanelState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HasSelectedStation ? 1 : 0;
                hash = (hash * 397) ^ Cash.GetHashCode();
                hash = (hash * 397) ^ WagonPurchasePrice.GetHashCode();
                return hash;
            }
        }
    }
}
