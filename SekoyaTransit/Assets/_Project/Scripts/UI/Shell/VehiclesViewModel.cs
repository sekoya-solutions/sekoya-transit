#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the vehicle cards and decides when their structure changed and decides when their structure changed (Issue #178).
    /// </summary>
    public static class VehiclesViewModel
    {
        /// <summary>
        /// Formats the card title: kind, number and status (a train adds its wagons), like "Truck #1 - Moving".
        /// </summary>
        public static string FormatTitle(VehicleListItem item, ShellTextCache text, CultureInfo culture)
        {
            string status = text.Get(GameUiKeys.VehicleStatusKey(item.Status));
            if (item.Kind == VehicleListKind.Rail)
            {
                return string.Format(
                    culture,
                    text.Get(GameUiKeys.VehiclesRowRail),
                    item.Id.ToString(culture),
                    text.Get(GameUiKeys.VehiclesKindRail),
                    status,
                    item.WagonCount.ToString(culture),
                    item.MaxWagons.ToString(culture));
            }

            return string.Format(
                culture,
                text.Get(GameUiKeys.VehiclesRow),
                item.Id.ToString(culture),
                text.Get(GameUiKeys.VehiclesKindRoad),
                status);
        }

        /// <summary>
        /// Formats the cargo line: the load and capacity, then the carried cargo with icons or "Empty".
        /// </summary>
        public static TextLine FormatCargo(VehicleListItem item, ShellTextCache text, CultureInfo culture)
        {
            TextLine stacks = CargoStackFormat.FormatStacks(item.CargoStacks, culture);
            if (stacks.IsEmpty)
            {
                stacks = TextLine.Plain(text.Get(GameUiKeys.VehiclesCargoEmpty));
            }

            return TextLine.Format(
                text.Get(GameUiKeys.VehiclesCargo),
                TextLine.Plain(item.CargoAmount.ToString(culture)),
                TextLine.Plain(item.CargoCapacity.ToString(culture)),
                stacks);
        }

        /// <summary>
        /// Formats last month's upkeep and income with the cash icon, or "not available yet".
        /// </summary>
        public static TextLine FormatFinances(VehicleListItem item, ShellTextCache text, CultureInfo culture)
        {
            if (!item.HasLastMonthFigures)
            {
                return TextLine.Plain(text.Get(GameUiKeys.VehiclesFinanceUnknown));
            }

            string line = string.Format(
                culture,
                text.Get(GameUiKeys.VehiclesFinance),
                CashFormat.FormatAmount(item.MonthlyUpkeep, culture),
                CashFormat.FormatAmount(item.LastMonthIncome, culture));
            return TextLine.Concat(TextLine.Plain(line + " "), TextLine.CashIcon());
        }

        /// <summary>
        /// Formats last month's benefit (income minus upkeep) with an explicit plus sign and the cash icon, or an empty
        /// line while the figures are unknown.
        /// </summary>
        public static TextLine FormatBenefit(VehicleListItem item, ShellTextCache text, CultureInfo culture)
        {
            if (!item.HasLastMonthFigures)
            {
                return TextLine.Empty;
            }

            long profit = item.LastMonthProfit;
            string profitText = (profit > 0L ? culture.NumberFormat.PositiveSign : string.Empty)
                + CashFormat.FormatAmount(profit, culture);
            string line = string.Format(culture, text.Get(GameUiKeys.VehiclesBenefit), profitText);
            return TextLine.Concat(TextLine.Plain(line + " "), TextLine.CashIcon());
        }

        /// <summary>
        /// Formats the sell button text with the cash received and the cash icon.
        /// </summary>
        public static TextLine FormatSell(VehicleListItem item, ShellTextCache text, CultureInfo culture)
        {
            string key = item.Kind == VehicleListKind.Rail ? GameUiKeys.VehiclesSellTrain : GameUiKeys.VehiclesSell;
            return ButtonLine(CashFormat.FormatLabeled(text.Get(key), item.SellValue, culture));
        }

        /// <summary>
        /// Formats the sell-wagon button text with the wagon sale value and the cash icon.
        /// </summary>
        public static TextLine FormatSellWagon(long wagonSellValue, ShellTextCache text, CultureInfo culture)
        {
            return ButtonLine(CashFormat.FormatLabeled(text.Get(GameUiKeys.VehiclesSellWagon), wagonSellValue, culture));
        }

        /// <summary>
        /// Returns the USS modifier of the card: green when last month's profit is positive, red otherwise, none while the
        /// figures are unknown (new or unassigned vehicles).
        /// </summary>
        public static string? CardClass(VehicleListItem item)
        {
            if (!item.HasLastMonthFigures)
            {
                return null;
            }

            return item.LastMonthProfit > 0L ? "vehicles-row--profit" : "vehicles-row--loss";
        }

        /// <summary>
        /// Formats the buy-wagon button text with the wagon price and the cash icon.
        /// </summary>
        public static TextLine FormatAddWagon(long wagonPurchasePrice, ShellTextCache text, CultureInfo culture)
        {
            return ButtonLine(CashFormat.FormatLabeled(text.Get(GameUiKeys.VehiclesAddWagon), wagonPurchasePrice, culture));
        }

        // A button label that ends with the cash icon, like the mockup.
        private static TextLine ButtonLine(string label)
        {
            return TextLine.Concat(TextLine.Plain(label + " "), TextLine.CashIcon());
        }

        /// <summary>
        /// Fingerprint of the row structure: it changes when vehicles, wagons, routes or last month figures change
        /// (the status and the cargo amounts update in place, so a click is never lost to a rebuild).
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
                    hash = (hash * 397) ^ item.CargoCapacity;
                    hash = (hash * 397) ^ (item.RouteName?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ item.RouteId;
                    hash = (hash * 397) ^ (item.HasLastMonth ? 1 : 0);
                    hash = (hash * 397) ^ item.LastMonthIncome.GetHashCode();
                    hash = (hash * 397) ^ item.MonthlyUpkeep.GetHashCode();
                    hash = (hash * 397) ^ item.SellValue.GetHashCode();
                }

                return hash;
            }
        }
    }
}
