#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the industries list rows and decides when their structure changed (Issue #178).
    /// </summary>
    public static class IndustriesViewModel
    {
        /// <summary>
        /// Formats one industry row (name, instance, level, stock, produced and transported this month).
        /// </summary>
        public static TextLine FormatRow(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesRow),
                TextLine.Plain(text.Get(item.NameKey)),
                TextLine.Plain(item.InstanceIndex.ToString(culture)),
                TextLine.Plain(item.Level.ToString(culture)),
                FormatStockDetail(item, culture),
                TextLine.Plain(item.MonthProduced.ToString(culture)),
                TextLine.Plain(item.MonthTransported.ToString(culture)));
        }

        /// <summary>
        /// Formats the stock: "Stock 0/cap" when empty, "[icon] cargo amount/cap" for one stack, or all stacks with
        /// the total over capacity.
        /// </summary>
        public static TextLine FormatStockDetail(IndustryListItem item, CultureInfo culture)
        {
            string capacity = item.CargoCapacity.ToString(culture);
            if (item.CargoStacks.Count == 0)
            {
                return TextLine.Plain("Stock 0/" + capacity);
            }

            if (item.CargoStacks.Count == 1)
            {
                CargoStackListLine stack = item.CargoStacks[0];
                return TextLine.Concat(
                    CargoStackFormat.Name(stack.CargoId),
                    TextLine.Plain(" " + stack.Amount.ToString(culture) + "/" + capacity));
            }

            return TextLine.Concat(
                CargoStackFormat.FormatStacks(item.CargoStacks, culture),
                TextLine.Plain(" (" + item.CargoAmount.ToString(culture) + "/" + capacity + ")"));
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
