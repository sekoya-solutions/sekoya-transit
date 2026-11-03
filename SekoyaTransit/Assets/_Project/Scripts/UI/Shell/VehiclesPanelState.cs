#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// What the vehicles panel needs besides its rows (Issue #229): whether a station is selected on the map (the list
    /// then shows only the vehicles of the routes that stop there), and the wagon purchase price and sale value.
    /// </summary>
    public readonly struct VehiclesPanelState : IEquatable<VehiclesPanelState>
    {
        public VehiclesPanelState(bool hasSelectedStation, long cash, long wagonPurchasePrice, long wagonSellValue = 0L)
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

            if (wagonSellValue < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(wagonSellValue), wagonSellValue, "Wagon sell value cannot be negative.");
            }

            HasSelectedStation = hasSelectedStation;
            WagonSellValue = wagonSellValue;
            Cash = cash;
            WagonPurchasePrice = wagonPurchasePrice;
        }

        public bool HasSelectedStation { get; }

        public long Cash { get; }

        public long WagonPurchasePrice { get; }

        public long WagonSellValue { get; }

        public bool CanAffordWagon => Cash >= WagonPurchasePrice;

        public bool Equals(VehiclesPanelState other)
        {
            return HasSelectedStation == other.HasSelectedStation
                && Cash == other.Cash
                && WagonPurchasePrice == other.WagonPurchasePrice
                && WagonSellValue == other.WagonSellValue;
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
                hash = (hash * 397) ^ WagonSellValue.GetHashCode();
                return hash;
            }
        }
    }
}
