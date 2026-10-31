#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the vehicles list rows and buy options and decides when their structure changed (Issue #178).
    /// </summary>
    public static class VehiclesViewModel
    {
        /// <summary>
        /// Formats one vehicle row; the vehicle being edited in the orders section gets a "selected" prefix.
        /// </summary>
        public static TextLine FormatRow(
            VehicleListItem item,
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

            if (item.RouteName != null)
            {
                row += " · " + string.Format(culture, text.Get(RoutesUiKeys.VehiclesRoute), item.RouteName);
            }

            TextLine line = TextLine.Plain(row);
            TextLine cargoDetail = CargoStackFormat.FormatStacks(item.CargoStacks, culture);
            return cargoDetail.IsEmpty ? line : TextLine.Concat(line, TextLine.Plain(" · "), cargoDetail);
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
                    hash = (hash * 397) ^ (item.RouteName?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }
    }
}
