#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of company cash, selected station kind, and placeholder vehicle prices.
    /// </summary>
    public readonly struct VehiclePurchaseUiState : IEquatable<VehiclePurchaseUiState>
    {
        /// <summary>
        /// Initializes purchase-button state.
        /// </summary>
        public VehiclePurchaseUiState(
            long cash,
            VehiclePurchaseSelection selection,
            long roadPurchasePrice,
            long railPurchasePrice,
            long wagonPurchasePrice)
        {
            if (cash < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(cash), cash, "Cash cannot be negative.");
            }

            if (roadPurchasePrice < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roadPurchasePrice),
                    roadPurchasePrice,
                    "Road purchase price must be positive.");
            }

            if (railPurchasePrice < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(railPurchasePrice),
                    railPurchasePrice,
                    "Rail purchase price must be positive.");
            }

            if (wagonPurchasePrice < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonPurchasePrice),
                    wagonPurchasePrice,
                    "Wagon purchase price must be positive.");
            }

            Cash = cash;
            Selection = selection;
            RoadPurchasePrice = roadPurchasePrice;
            RailPurchasePrice = railPurchasePrice;
            WagonPurchasePrice = wagonPurchasePrice;
        }

        /// <summary>
        /// Gets current company cash.
        /// </summary>
        public long Cash { get; }

        /// <summary>
        /// Gets the selected station kind that Buy would use.
        /// </summary>
        public VehiclePurchaseSelection Selection { get; }

        /// <summary>
        /// Gets the placeholder truck purchase price.
        /// </summary>
        public long RoadPurchasePrice { get; }

        /// <summary>
        /// Gets the placeholder locomotive purchase price.
        /// </summary>
        public long RailPurchasePrice { get; }

        /// <summary>
        /// Gets the placeholder wagon purchase price.
        /// </summary>
        public long WagonPurchasePrice { get; }

        /// <summary>
        /// Gets whether Buy can afford the selected station's vehicle.
        /// </summary>
        public bool CanAffordSelected
        {
            get
            {
                if (Selection == VehiclePurchaseSelection.Road)
                {
                    return Cash >= RoadPurchasePrice;
                }

                if (Selection == VehiclePurchaseSelection.Rail)
                {
                    return Cash >= RailPurchasePrice;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets whether Add wagon can be paid from current cash.
        /// </summary>
        public bool CanAffordWagon
        {
            get
            {
                return Cash >= WagonPurchasePrice;
            }
        }

        /// <inheritdoc />
        public bool Equals(VehiclePurchaseUiState other)
        {
            return Cash == other.Cash
                && Selection == other.Selection
                && RoadPurchasePrice == other.RoadPurchasePrice
                && RailPurchasePrice == other.RailPurchasePrice
                && WagonPurchasePrice == other.WagonPurchasePrice;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is VehiclePurchaseUiState other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Cash.GetHashCode();
                hash = (hash * 397) ^ (int)Selection;
                hash = (hash * 397) ^ RoadPurchasePrice.GetHashCode();
                hash = (hash * 397) ^ RailPurchasePrice.GetHashCode();
                hash = (hash * 397) ^ WagonPurchasePrice.GetHashCode();
                return hash;
            }
        }
    }
}
