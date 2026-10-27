#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats the vehicles list rows and buy options and decides when their structure changed (Issue #178).
    /// </summary>
    public static class VehiclesViewModel
    {
        /// <summary>
        /// Formats one vehicle row; the vehicle being edited in the orders section gets a "selected" prefix.
        /// </summary>
        public static string FormatRow(
            VehicleListItem item,
            bool isSelectedForOrders,
            ShellTextCache text,
            CultureInfo culture)
        {
            string status = text.Get(GameUiKeys.VehicleStatusKey(item.Status));
            string income = CashFormat.FormatAmount(item.LifetimeIncome, culture);
            string upkeep = CashFormat.FormatAmount(item.MonthlyUpkeep, culture);
            string row;
            if (item.Kind == VehicleListKind.Rail)
            {
                row = string.Format(
                    culture,
                    text.Get(GameUiKeys.VehiclesRowRail),
                    item.Id.ToString(culture),
                    text.Get(GameUiKeys.VehiclesKindRail),
                    status,
                    item.WagonCount.ToString(culture),
                    item.MaxWagons.ToString(culture),
                    item.CargoAmount.ToString(culture),
                    item.CargoCapacity.ToString(culture),
                    income,
                    upkeep);
            }
            else
            {
                row = string.Format(
                    culture,
                    text.Get(GameUiKeys.VehiclesRow),
                    item.Id.ToString(culture),
                    text.Get(GameUiKeys.VehiclesKindRoad),
                    status,
                    item.CargoAmount.ToString(culture),
                    item.CargoCapacity.ToString(culture),
                    income,
                    upkeep);
            }

            string cargoDetail = CargoStackFormat.FormatStacks(item.CargoStacks, culture);
            if (!string.IsNullOrEmpty(cargoDetail))
            {
                row = row + " · " + cargoDetail;
            }

            return isSelectedForOrders ? "► SELECTED · " + row : row;
        }

        /// <summary>
        /// Formats a buy button: the localized vehicle name and its price.
        /// </summary>
        public static string FormatBuyOption(VehiclePurchaseOption option, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.VehiclesBuyOption),
                text.Get(option.NameKey),
                CashFormat.FormatAmount(option.PurchasePrice, culture));
        }

        /// <summary>
        /// Formats the add-wagon button text with the wagon price.
        /// </summary>
        public static string FormatAddWagon(long wagonPurchasePrice, ShellTextCache text, CultureInfo culture)
        {
            return CashFormat.FormatLabeled(text.Get(GameUiKeys.VehiclesAddWagon), wagonPurchasePrice, culture);
        }

        /// <summary>
        /// Fingerprint of the row structure: it changes when vehicles, wagons, or cargo amounts and capacities change.
        /// </summary>
        public static int ComputeVehicleFingerprint(IReadOnlyList<VehicleListItem> vehicles)
        {
            if (vehicles is null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            unchecked
            {
                int hash = vehicles.Count;
                for (int index = 0; index < vehicles.Count; index++)
                {
                    VehicleListItem item = vehicles[index];
                    hash = (hash * 397) ^ (int)item.Kind;
                    hash = (hash * 397) ^ item.Id;
                    hash = (hash * 397) ^ item.WagonCount;
                    hash = (hash * 397) ^ item.CargoAmount;
                    hash = (hash * 397) ^ item.CargoCapacity;
                }

                return hash;
            }
        }

        /// <summary>
        /// Fingerprint of the buy options: it changes with the selected station kind, cash, and each option's
        /// price and affordability.
        /// </summary>
        public static int ComputePurchaseFingerprint(VehiclePurchaseUiState purchase)
        {
            unchecked
            {
                int hash = (int)purchase.Selection;
                hash = (hash * 397) ^ purchase.Cash.GetHashCode();
                IReadOnlyList<VehiclePurchaseOption> options = purchase.Options;
                hash = (hash * 397) ^ options.Count;
                for (int index = 0; index < options.Count; index++)
                {
                    VehiclePurchaseOption option = options[index];
                    hash = (hash * 397) ^ option.TypeId.GetHashCode();
                    hash = (hash * 397) ^ option.PurchasePrice.GetHashCode();
                    hash = (hash * 397) ^ option.CanAfford.GetHashCode();
                }

                return hash;
            }
        }
    }
}
