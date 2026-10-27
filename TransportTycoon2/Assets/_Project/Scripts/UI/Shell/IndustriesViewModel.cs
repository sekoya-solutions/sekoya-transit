#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats the industries list rows and decides when their structure changed (Issue #178).
    /// </summary>
    public static class IndustriesViewModel
    {
        /// <summary>
        /// Formats one industry row (name, instance, level, stock, produced and transported this month).
        /// </summary>
        public static string FormatRow(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.IndustriesRow),
                text.Get(item.NameKey),
                item.InstanceIndex.ToString(culture),
                item.Level.ToString(culture),
                FormatStockDetail(item, culture),
                item.MonthProduced.ToString(culture),
                item.MonthTransported.ToString(culture));
        }

        /// <summary>
        /// Formats the stock: "Stock 0/cap" when empty, "cargo amount/cap" for one stack, or all stacks with the
        /// total over capacity.
        /// </summary>
        public static string FormatStockDetail(IndustryListItem item, CultureInfo culture)
        {
            string capacity = item.CargoCapacity.ToString(culture);
            if (item.CargoStacks.Count == 0)
            {
                return "Stock 0/" + capacity;
            }

            if (item.CargoStacks.Count == 1)
            {
                CargoStackListLine stack = item.CargoStacks[0];
                return CargoStackFormat.DisplayName(stack.CargoId)
                    + " "
                    + stack.Amount.ToString(culture)
                    + "/"
                    + capacity;
            }

            return CargoStackFormat.FormatStacks(item.CargoStacks, culture)
                + " ("
                + item.CargoAmount.ToString(culture)
                + "/"
                + capacity
                + ")";
        }

        /// <summary>
        /// Fingerprint of the row structure: it changes when industry instances appear or disappear.
        /// </summary>
        public static int ComputeFingerprint(IReadOnlyList<IndustryListItem> industries)
        {
            if (industries is null)
            {
                throw new ArgumentNullException(nameof(industries));
            }

            unchecked
            {
                int hash = industries.Count;
                for (int index = 0; index < industries.Count; index++)
                {
                    IndustryListItem item = industries[index];
                    hash = (hash * 397) ^ item.IndustryId.GetHashCode();
                    hash = (hash * 397) ^ item.InstanceIndex;
                }

                return hash;
            }
        }
    }
}
