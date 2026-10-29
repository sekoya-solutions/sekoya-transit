#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of company cash, selected station kind, buy options, and wagon price.
    /// </summary>
    public readonly struct VehiclePurchaseUiState : IEquatable<VehiclePurchaseUiState>
    {
        private readonly VehiclePurchaseOption[] _options;

        /// <summary>
        /// Initializes purchase UI state.
        /// </summary>
        public VehiclePurchaseUiState(
            long cash,
            VehiclePurchaseSelection selection,
            IReadOnlyList<VehiclePurchaseOption> options,
            long wagonPurchasePrice,
            int? selectedStationId = null)
        {
#pragma warning disable CA1510
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
#pragma warning restore CA1510

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

            _options = new VehiclePurchaseOption[options.Count];
            for (int index = 0; index < options.Count; index++)
            {
                _options[index] = options[index];
            }

            Cash = cash;
            Selection = selection;
            WagonPurchasePrice = wagonPurchasePrice;
            SelectedStationId = selectedStationId;
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
        /// Gets the id of the selected station (of kind <see cref="Selection"/>), or null when none is selected.
        /// </summary>
        public int? SelectedStationId { get; }

        /// <summary>
        /// Gets purchasable type options for the selected station (empty when none selected).
        /// </summary>
        public IReadOnlyList<VehiclePurchaseOption> Options => _options;

        /// <summary>
        /// Gets the per-wagon attach price.
        /// </summary>
        public long WagonPurchasePrice { get; }

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
            if (Cash != other.Cash
                || Selection != other.Selection
                || SelectedStationId != other.SelectedStationId
                || WagonPurchasePrice != other.WagonPurchasePrice
                || _options.Length != other._options.Length)
            {
                return false;
            }

            for (int index = 0; index < _options.Length; index++)
            {
                if (!_options[index].Equals(other._options[index]))
                {
                    return false;
                }
            }

            return true;
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
                hash = (hash * 397) ^ (SelectedStationId ?? -1);
                hash = (hash * 397) ^ WagonPurchasePrice.GetHashCode();
                for (int index = 0; index < _options.Length; index++)
                {
                    hash = (hash * 397) ^ _options[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
