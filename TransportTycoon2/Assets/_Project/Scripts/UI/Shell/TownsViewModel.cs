#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats the towns list rows and decides when their structure changed (Issue #178).
    /// </summary>
    public static class TownsViewModel
    {
        /// <summary>
        /// Formats the town summary line (id, population, satisfied / total needs).
        /// </summary>
        public static string FormatSummary(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.TownsSummary),
                item.TownId.ToString(culture),
                item.Population.ToString(culture),
                item.SatisfiedNeeds.ToString(culture),
                item.TotalNeeds.ToString(culture));
        }

        /// <summary>
        /// Formats one freight need line (cargo, supplied this month, demand).
        /// </summary>
        public static string FormatNeedLine(TownNeedListLine need, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.TownsNeedLine),
                CargoStackFormat.DisplayName(need.CargoId),
                need.MonthSupply.ToString(culture),
                need.Demand.ToString(culture));
        }

        /// <summary>
        /// Formats the passenger line (generated and transported this month).
        /// </summary>
        public static string FormatPassengerLine(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.TownsPassengerLine),
                item.MonthPassengersGenerated.ToString(culture),
                item.MonthPassengersTransported.ToString(culture));
        }

        /// <summary>
        /// Formats the mail line (generated and transported this month).
        /// </summary>
        public static string FormatMailLine(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return string.Format(
                culture,
                text.Get(GameUiKeys.TownsMailLine),
                item.MonthMailGenerated.ToString(culture),
                item.MonthMailTransported.ToString(culture));
        }

        /// <summary>
        /// Fingerprint of the row structure: it changes when towns or their need counts change, not when only
        /// the numbers shown in a row change.
        /// </summary>
        public static int ComputeFingerprint(IReadOnlyList<TownListItem> towns)
        {
            if (towns is null)
            {
                throw new ArgumentNullException(nameof(towns));
            }

            unchecked
            {
                int hash = towns.Count;
                for (int index = 0; index < towns.Count; index++)
                {
                    TownListItem item = towns[index];
                    hash = (hash * 397) ^ item.TownId;
                    hash = (hash * 397) ^ item.Needs.Count;
                }

                return hash;
            }
        }
    }
}
