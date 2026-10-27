#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// The data providers and command callbacks Presentation supplies to the vehicles, unlocks, industries, and
    /// towns panels (Issue #178).
    /// </summary>
    public sealed class ListPanelBindings
    {
        /// <summary>
        /// Initializes the binding set; no argument may be null.
        /// </summary>
        public ListPanelBindings(
            Func<IReadOnlyList<VehicleListItem>> vehicles,
            Func<IReadOnlyList<IndustryListItem>> industries,
            Func<IReadOnlyList<TownListItem>> towns,
            Func<IReadOnlyList<VehicleUnlockListItem>> unlocks,
            Func<VehiclePurchaseUiState> purchase,
            Action<string> buyVehicleType,
            Action<string> unlockVehicleType,
            Action<VehicleListKind, int> sellVehicle,
            Action<int> addRailWagon,
            Action<int> sellRailWagon,
            Func<VehicleListKind, int, IReadOnlyList<int>> getVehicleOrderStationIds,
            Func<VehicleListKind, int, IReadOnlyList<int>, bool> tryApplyVehicleOrders,
            Action<int> townRowSelected,
            Action<string, int> industryRowSelected)
        {
            Vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            Industries = industries ?? throw new ArgumentNullException(nameof(industries));
            Towns = towns ?? throw new ArgumentNullException(nameof(towns));
            Unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            Purchase = purchase ?? throw new ArgumentNullException(nameof(purchase));
            BuyVehicleType = buyVehicleType ?? throw new ArgumentNullException(nameof(buyVehicleType));
            UnlockVehicleType = unlockVehicleType ?? throw new ArgumentNullException(nameof(unlockVehicleType));
            SellVehicle = sellVehicle ?? throw new ArgumentNullException(nameof(sellVehicle));
            AddRailWagon = addRailWagon ?? throw new ArgumentNullException(nameof(addRailWagon));
            SellRailWagon = sellRailWagon ?? throw new ArgumentNullException(nameof(sellRailWagon));
            GetVehicleOrderStationIds = getVehicleOrderStationIds
                ?? throw new ArgumentNullException(nameof(getVehicleOrderStationIds));
            TryApplyVehicleOrders = tryApplyVehicleOrders
                ?? throw new ArgumentNullException(nameof(tryApplyVehicleOrders));
            TownRowSelected = townRowSelected ?? throw new ArgumentNullException(nameof(townRowSelected));
            IndustryRowSelected = industryRowSelected ?? throw new ArgumentNullException(nameof(industryRowSelected));
        }

        /// <summary>
        /// Gets the vehicle list provider.
        /// </summary>
        public Func<IReadOnlyList<VehicleListItem>> Vehicles { get; }

        /// <summary>
        /// Gets the industry list provider.
        /// </summary>
        public Func<IReadOnlyList<IndustryListItem>> Industries { get; }

        /// <summary>
        /// Gets the town list provider.
        /// </summary>
        public Func<IReadOnlyList<TownListItem>> Towns { get; }

        /// <summary>
        /// Gets the vehicle unlock list provider.
        /// </summary>
        public Func<IReadOnlyList<VehicleUnlockListItem>> Unlocks { get; }

        /// <summary>
        /// Gets the buy options and cash provider.
        /// </summary>
        public Func<VehiclePurchaseUiState> Purchase { get; }

        /// <summary>
        /// Gets the buy callback (vehicle type id).
        /// </summary>
        public Action<string> BuyVehicleType { get; }

        /// <summary>
        /// Gets the unlock callback (vehicle type id).
        /// </summary>
        public Action<string> UnlockVehicleType { get; }

        /// <summary>
        /// Gets the sell callback (kind, vehicle id).
        /// </summary>
        public Action<VehicleListKind, int> SellVehicle { get; }

        /// <summary>
        /// Gets the add-wagon callback (rail vehicle id).
        /// </summary>
        public Action<int> AddRailWagon { get; }

        /// <summary>
        /// Gets the sell-wagon callback (rail vehicle id).
        /// </summary>
        public Action<int> SellRailWagon { get; }

        /// <summary>
        /// Gets the provider of a vehicle's current route (station ids).
        /// </summary>
        public Func<VehicleListKind, int, IReadOnlyList<int>> GetVehicleOrderStationIds { get; }

        /// <summary>
        /// Gets the callback that applies a route to a vehicle; returns whether it was accepted.
        /// </summary>
        public Func<VehicleListKind, int, IReadOnlyList<int>, bool> TryApplyVehicleOrders { get; }

        /// <summary>
        /// Gets the callback for a click on a town row (town id).
        /// </summary>
        public Action<int> TownRowSelected { get; }

        /// <summary>
        /// Gets the callback for a click on an industry row (industry id, instance index).
        /// </summary>
        public Action<string, int> IndustryRowSelected { get; }
    }
}
