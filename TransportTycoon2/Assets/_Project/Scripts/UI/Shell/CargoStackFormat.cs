#nullable enable
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats cargo names and stacks for the vehicle, industry, and town lists (Issue #178); cargo names carry their
    /// icon (Issue #182).
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
        /// Returns the cargo icon followed by its display name.
        /// </summary>
        public static TextLine Name(string cargoId)
        {
            return TextLine.Cargo(cargoId, DisplayName(cargoId));
        }

        /// <summary>
        /// Returns "amount [icon] name, amount [icon] name", or an empty line when there are no stacks.
        /// </summary>
        public static TextLine FormatStacks(IReadOnlyList<CargoStackListLine>? stacks, CultureInfo culture)
        {
            if (stacks == null || stacks.Count == 0)
            {
                return TextLine.Empty;
            }

            var parts = new TextLine[stacks.Count];
            for (int index = 0; index < stacks.Count; index++)
            {
                CargoStackListLine stack = stacks[index];
                parts[index] = TextLine.Concat(
                    TextLine.Plain(stack.Amount.ToString(culture) + " "),
                    Name(stack.CargoId));
            }

            return TextLine.Join(", ", parts);
        }
    }
}
