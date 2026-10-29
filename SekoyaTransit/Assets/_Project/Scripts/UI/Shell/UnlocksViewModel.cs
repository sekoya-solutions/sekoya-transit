#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the vehicle unlocks list rows and decides when their structure changed (Issue #178).
    /// </summary>
    public static class UnlocksViewModel
    {
        /// <summary>
        /// Formats the row summary for the item's status (future, unlockable, or unlocked).
        /// </summary>
        public static string FormatSummary(VehicleUnlockListItem item, ShellTextCache text, CultureInfo culture)
        {
            string name = text.Get(item.NameKey);
            switch (item.Status)
            {
                case VehicleUnlockRowStatus.Future:
                    return string.Format(
                        culture,
                        text.Get(GameUiKeys.UnlocksRowFuture),
                        name,
                        item.AvailableYear.ToString(culture),
                        CashFormat.FormatAmount(item.UnlockCost, culture));
                case VehicleUnlockRowStatus.Unlockable:
                    return string.Format(
                        culture,
                        text.Get(GameUiKeys.UnlocksRowUnlockable),
                        name,
                        CashFormat.FormatAmount(item.UnlockCost, culture));
                case VehicleUnlockRowStatus.Unlocked:
                    return string.Format(culture, text.Get(GameUiKeys.UnlocksRowUnlocked), name);
                default:
                    return name;
            }
        }

        /// <summary>
        /// Formats the unlock button text: the cost while unlockable, "Unlocked" otherwise.
        /// </summary>
        public static string FormatAction(VehicleUnlockListItem item, ShellTextCache text, CultureInfo culture)
        {
            if (item.Status != VehicleUnlockRowStatus.Unlockable)
            {
                return text.Get(GameUiKeys.UnlocksActionUnlocked);
            }

            return CashFormat.FormatLabeled(text.Get(GameUiKeys.UnlocksAction), item.UnlockCost, culture);
        }

        /// <summary>
        /// Fingerprint of the row structure: it changes when a status, cost, or affordability changes (an
        /// unlock button exists only for unlockable rows).
        /// </summary>
        public static int ComputeFingerprint(IReadOnlyList<VehicleUnlockListItem> unlocks)
        {
            if (unlocks is null)
            {
                throw new ArgumentNullException(nameof(unlocks));
            }

            unchecked
            {
                int hash = unlocks.Count;
                for (int index = 0; index < unlocks.Count; index++)
                {
                    VehicleUnlockListItem item = unlocks[index];
                    hash = (hash * 397) ^ item.TypeId.GetHashCode();
                    hash = (hash * 397) ^ (int)item.Status;
                    hash = (hash * 397) ^ item.UnlockCost.GetHashCode();
                    hash = (hash * 397) ^ item.CanAffordUnlock.GetHashCode();
                }

                return hash;
            }
        }
    }
}
