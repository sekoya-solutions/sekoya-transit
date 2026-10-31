#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the towns list rows and decides when their structure changed (Issue #178).
    /// </summary>
    public static class TownsViewModel
    {
        /// <summary>
        /// Cargo id whose icon marks the passenger line.
        /// </summary>
        public const string PassengersCargoId = "passengers";

        /// <summary>
        /// Cargo id whose icon marks the mail line.
        /// </summary>
        public const string MailCargoId = "mail";

        /// <summary>
        /// Formats the town summary line (name, population, satisfied / total needs). A town without a name shows its
        /// id instead.
        /// </summary>
        public static TextLine FormatSummary(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.TownsSummary),
                TextLine.Plain(item.Name.Length > 0 ? item.Name : "#" + item.TownId.ToString(culture)),
                TextLine.Concat(TextLine.Plain(item.Population.ToString(culture)), TextLine.PopulationIcon()),
                TextLine.Plain(item.SatisfiedNeeds.ToString(culture)),
                TextLine.Plain(item.TotalNeeds.ToString(culture)));
        }

        /// <summary>
        /// Formats one freight need line (cargo, supplied this month, demand).
        /// </summary>
        public static TextLine FormatNeedLine(TownNeedListLine need, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.TownsNeedLine),
                CargoStackFormat.Name(need.CargoId),
                TextLine.Plain(need.MonthSupply.ToString(culture)),
                TextLine.Plain(need.Demand.ToString(culture)));
        }

        /// <summary>
        /// Formats the passenger line (generated and transported this month) behind the passengers icon.
        /// </summary>
        public static TextLine FormatPassengerLine(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Concat(
                TextLine.Icon(PassengersCargoId),
                TextLine.Plain(
                    string.Format(
                        culture,
                        text.Get(GameUiKeys.TownsPassengerLine),
                        item.MonthPassengersGenerated.ToString(culture),
                        item.MonthPassengersTransported.ToString(culture))));
        }

        /// <summary>
        /// Formats the mail line (generated and transported this month) behind the mail icon.
        /// </summary>
        public static TextLine FormatMailLine(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Concat(
                TextLine.Icon(MailCargoId),
                TextLine.Plain(
                    string.Format(
                        culture,
                        text.Get(GameUiKeys.TownsMailLine),
                        item.MonthMailGenerated.ToString(culture),
                        item.MonthMailTransported.ToString(culture))));
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
