#nullable enable
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats the route (orders) section of the vehicles panel (Issue #178).
    /// </summary>
    public static class OrdersViewModel
    {
        /// <summary>
        /// Formats the "editing vehicle" line.
        /// </summary>
        public static string FormatSelected(int vehicleId, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(culture, text.Get(GameUiKeys.OrdersSelected), vehicleId.ToString(culture));
        }

        /// <summary>
        /// Formats one stop line; <paramref name="index"/> is zero-based and shown one-based.
        /// </summary>
        public static string FormatStop(int index, int stationId, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.OrdersStop),
                (index + 1).ToString(culture),
                stationId.ToString(culture));
        }
    }
}
