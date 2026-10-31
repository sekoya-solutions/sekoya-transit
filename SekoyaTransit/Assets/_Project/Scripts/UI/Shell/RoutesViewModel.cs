#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Text for the routes window and its station and vehicle windows (Issue #229).
    /// </summary>
    public static class RoutesViewModel
    {
        /// <summary>
        /// The longest route name the name field accepts (matches the Core rule).
        /// </summary>
        public const int MaxNameLength = 32;

        /// <summary>
        /// Formats last month's running cost as a negative amount with the cash icon, or the no-value mark.
        /// </summary>
        public static TextLine FormatRunningCost(RouteListItem item, ShellTextCache text, CultureInfo culture)
        {
            return item.HasLastMonth ? Money(-item.LastMonthRunningCost, false, culture) : NoValue(text);
        }

        /// <summary>
        /// Formats last month's revenue with the cash icon, or the no-value mark.
        /// </summary>
        public static TextLine FormatRevenue(RouteListItem item, ShellTextCache text, CultureInfo culture)
        {
            return item.HasLastMonth ? Money(item.LastMonthRevenue, false, culture) : NoValue(text);
        }

        /// <summary>
        /// Formats last month's profit with an explicit sign and the cash icon, or the no-value mark.
        /// </summary>
        public static TextLine FormatProfit(RouteListItem item, ShellTextCache text, CultureInfo culture)
        {
            return item.HasLastMonth ? Money(item.LastMonthProfit, true, culture) : NoValue(text);
        }

        /// <summary>
        /// Returns the USS modifier that colors the profit: green when positive, red when negative, none otherwise.
        /// </summary>
        public static string? ProfitClass(RouteListItem item)
        {
            if (!item.HasLastMonth || item.LastMonthProfit == 0L)
            {
                return null;
            }

            return item.LastMonthProfit > 0L ? "routes-profit--positive" : "routes-profit--negative";
        }

        /// <summary>
        /// Returns the first "Route N" name (N from 1) that no route uses yet, ignoring case.
        /// </summary>
        public static string SuggestName(IReadOnlyList<RouteListItem> routes, ShellTextCache text, CultureInfo culture)
        {
            if (routes is null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            for (int number = 1; ; number++)
            {
                string candidate = string.Format(culture, text.Get(RoutesUiKeys.NameDefault), number);
                bool taken = false;
                for (int index = 0; index < routes.Count && !taken; index++)
                {
                    taken = string.Equals(routes[index].Name, candidate, StringComparison.OrdinalIgnoreCase);
                }

                if (!taken)
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// Returns the message key of a refused name, or null when the name was accepted.
        /// </summary>
        public static string? NameErrorKey(RouteNameError error)
        {
            switch (error)
            {
                case RouteNameError.Empty:
                    return RoutesUiKeys.NameErrorEmpty;
                case RouteNameError.TooLong:
                    return RoutesUiKeys.NameErrorTooLong;
                case RouteNameError.Taken:
                    return RoutesUiKeys.NameErrorTaken;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns the message key shown when a map click could not add a stop, or null when it was added.
        /// </summary>
        public static string? StopErrorKey(RouteStopAddResult result)
        {
            switch (result)
            {
                case RouteStopAddResult.ModeMismatch:
                    return RoutesUiKeys.StationsModeMismatch;
                case RouteStopAddResult.Repeated:
                    return RoutesUiKeys.StationsRepeated;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Formats a stop card title, such as "Station #4".
        /// </summary>
        public static string FormatStop(RouteStopItem stop, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(culture, text.Get(RoutesUiKeys.StationsStop), stop.StationId.ToString(culture));
        }

        /// <summary>
        /// Formats a vehicle label, such as "Truck Mk2 #12".
        /// </summary>
        public static string FormatVehicle(RouteVehicleItem vehicle, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(RoutesUiKeys.VehicleLabel),
                text.Get(vehicle.TypeNameKey),
                vehicle.VehicleId.ToString(culture));
        }

        /// <summary>
        /// Formats a count button, such as "Add/Remove (3)".
        /// </summary>
        public static string FormatEditButton(string templateKey, int count, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(culture, text.Get(templateKey), count.ToString(culture));
        }

        /// <summary>
        /// Returns a value that changes whenever any routes window row changes.
        /// </summary>
        public static int ComputeFingerprint(IReadOnlyList<RouteListItem> routes)
        {
            if (routes is null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            unchecked
            {
                int hash = routes.Count;
                for (int index = 0; index < routes.Count; index++)
                {
                    hash = (hash * 397) ^ routes[index].GetHashCode();
                    hash = (hash * 397) ^ (routes[index].IsValid ? 1 : 0);
                    hash = (hash * 397) ^ (routes[index].HasLastMonth ? 2 : 0);
                }

                return hash;
            }
        }

        private static TextLine NoValue(ShellTextCache text)
        {
            return TextLine.Plain(text.Get(RoutesUiKeys.NoValue));
        }

        // "-150 [cash icon]", "+150 [cash icon]" or "300 [cash icon]", like the mockup.
        private static TextLine Money(long amount, bool explicitSign, CultureInfo culture)
        {
            string number = CashFormat.FormatAmount(amount, culture);
            if (explicitSign && amount > 0L)
            {
                number = culture.NumberFormat.PositiveSign + number;
            }

            return TextLine.Concat(TextLine.Plain(number + " "), TextLine.CashIcon());
        }
    }
}
