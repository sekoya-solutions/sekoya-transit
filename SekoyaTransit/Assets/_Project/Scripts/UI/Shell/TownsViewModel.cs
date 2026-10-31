#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the towns panel cards and rules text, and decides when the card structure changed (Issue #178,
    /// Issue #242).
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
        /// USS class of a need line that was partly met last month (blue).
        /// </summary>
        public const string PartlyMetUssClass = "towns-need--partly-met";

        /// <summary>
        /// USS class of a need line that was fully met last month (green).
        /// </summary>
        public const string FullyMetUssClass = "towns-need--fully-met";

        /// <summary>
        /// Formats the town summary line (name and population). A town without a name shows its id instead.
        /// </summary>
        public static TextLine FormatSummary(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.TownsSummary),
                TextLine.Plain(item.Name.Length > 0 ? item.Name : "#" + item.TownId.ToString(culture)),
                Population(item.Population, culture));
        }

        /// <summary>
        /// Formats "Growth level: N/M, next at X [population icon]", or the max-level wording at the last tier.
        /// </summary>
        public static TextLine FormatGrowthLevel(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            TextLine level = TextLine.Plain(item.GrowthLevel.ToString(culture));
            TextLine maxLevel = TextLine.Plain(item.MaxGrowthLevel.ToString(culture));
            if (item.IsMaxGrowthLevel)
            {
                return TextLine.Format(text.Get(GameUiKeys.TownsGrowthLevelMax), level, maxLevel);
            }

            return TextLine.Format(
                text.Get(GameUiKeys.TownsGrowthLevel),
                level,
                maxLevel,
                Population(item.NextLevelPopulation, culture));
        }

        /// <summary>
        /// Formats "+N [population icon] last month" (inhabitants gained from needs at the end of last month).
        /// </summary>
        public static TextLine FormatLastMonthGrowth(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.TownsLastMonthGrowth),
                Population(item.LastMonthGrowth, culture));
        }

        /// <summary>
        /// Returns the localization key of a need level heading: Raw, Processed, or Finished needs.
        /// </summary>
        public static string GetNeedsHeaderKey(int level)
        {
            switch (level)
            {
                case 1:
                    return GameUiKeys.TownsNeedsRaw;
                case 2:
                    return GameUiKeys.TownsNeedsProcessed;
                case 3:
                    return GameUiKeys.TownsNeedsFinished;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Need level must be 1, 2 or 3.");
            }
        }

        /// <summary>
        /// Formats one need line: the cargo icon (name in its tooltip), the demand per month, and the units supplied
        /// last month.
        /// </summary>
        public static TextLine FormatNeedLine(TownNeedListLine need, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.TownsNeedLine),
                CargoStackFormat.Name(need.CargoId),
                TextLine.Plain(need.Demand.ToString(culture)),
                TextLine.Plain(need.LastMonthSupply.ToString(culture)));
        }

        /// <summary>
        /// Returns the USS class that colors a need line by last month's supply, or null for an unmet need.
        /// </summary>
        public static string? GetNeedStatusClass(TownNeedLineStatus status)
        {
            switch (status)
            {
                case TownNeedLineStatus.FullyMet:
                    return FullyMetUssClass;
                case TownNeedLineStatus.PartlyMet:
                    return PartlyMetUssClass;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Formats the passenger line (generated and transported this month) behind the passengers icon.
        /// </summary>
        public static TextLine FormatPassengerLine(TownListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Concat(
                CargoStackFormat.Name(PassengersCargoId),
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
                CargoStackFormat.Name(MailCargoId),
                TextLine.Plain(
                    string.Format(
                        culture,
                        text.Get(GameUiKeys.TownsMailLine),
                        item.MonthMailGenerated.ToString(culture),
                        item.MonthMailTransported.ToString(culture))));
        }

        /// <summary>
        /// Formats the partly met rule: the growth per level, then the population icon.
        /// </summary>
        public static TextLine FormatPartlyMetRule(TownGrowthRulesInfo rules, ShellTextCache text, CultureInfo culture)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return TextLine.Format(
                text.Get(GameUiKeys.TownsRulesPartlyMet),
                TextLine.Plain(rules.GetPartlyMet(1).ToString(culture)),
                TextLine.Plain(rules.GetPartlyMet(2).ToString(culture)),
                TextLine.Plain(rules.GetPartlyMet(3).ToString(culture)),
                TextLine.PopulationIcon());
        }

        /// <summary>
        /// Formats the fully met rule: the growth per level, then the population icon.
        /// </summary>
        public static TextLine FormatFullyMetRule(TownGrowthRulesInfo rules, ShellTextCache text, CultureInfo culture)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return TextLine.Format(
                text.Get(GameUiKeys.TownsRulesFullyMet),
                TextLine.Plain(rules.GetFullyMet(1).ToString(culture)),
                TextLine.Plain(rules.GetFullyMet(2).ToString(culture)),
                TextLine.Plain(rules.GetFullyMet(3).ToString(culture)),
                TextLine.PopulationIcon());
        }

        /// <summary>
        /// Formats the all-needs rule: the multiplier, then the population icon.
        /// </summary>
        public static TextLine FormatAllMetRule(TownGrowthRulesInfo rules, ShellTextCache text, CultureInfo culture)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return TextLine.Format(
                text.Get(GameUiKeys.TownsRulesAllMet),
                TextLine.Plain(rules.AllNeedsMetMultiplier.ToString(culture)),
                TextLine.PopulationIcon());
        }

        /// <summary>
        /// Fingerprint of the card structure: it changes when towns or their need counts per level change, not when
        /// only the numbers or colors shown in a card change.
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
                    for (int level = TownNeedListLine.MinLevel; level <= TownNeedListLine.MaxLevel; level++)
                    {
                        hash = (hash * 397) ^ item.CountNeeds(level);
                    }
                }

                return hash;
            }
        }

        private static TextLine Population(int amount, CultureInfo culture)
        {
            return TextLine.Concat(TextLine.Plain(amount.ToString(culture)), TextLine.PopulationIcon());
        }
    }
}
