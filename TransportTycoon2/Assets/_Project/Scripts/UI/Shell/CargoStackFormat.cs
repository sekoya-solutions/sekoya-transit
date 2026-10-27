#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats cargo names and stacks for the vehicle, industry, and town lists (Issue #178).
    /// </summary>
    public static class CargoStackFormat
    {
        /// <summary>
        /// Returns the display name of a cargo (its id with spaces instead of underscores).
        /// </summary>
        public static string DisplayName(string cargoId)
        {
            return cargoId.Replace('_', ' ');
        }

        /// <summary>
        /// Returns "amount name, amount name", or an empty string when there are no stacks.
        /// </summary>
        public static string FormatStacks(IReadOnlyList<CargoStackListLine>? stacks, CultureInfo culture)
        {
            if (stacks == null || stacks.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[stacks.Count];
            for (int index = 0; index < stacks.Count; index++)
            {
                CargoStackListLine stack = stacks[index];
                parts[index] = stack.Amount.ToString(culture) + " " + DisplayName(stack.CargoId);
            }

            return string.Join(", ", parts);
        }
    }
}
