#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Formats the industries panel lines and decides when its structure changed (Issue #178, Issue #282). Instance
    /// numbers and growth levels are shown from 1 although Core counts from 0.
    /// </summary>
    public static class IndustriesViewModel
    {
        /// <summary>
        /// Formats the card title: "Forest #1 - Growth level 1/3", or "Sawmill #1" when the industry cannot grow.
        /// </summary>
        public static TextLine FormatSummary(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            TextLine name = TextLine.Plain(text.Get(item.NameKey));
            TextLine number = TextLine.Plain(DisplayNumber(item.InstanceIndex).ToString(culture));
            if (!item.CanGrow)
            {
                return TextLine.Format(text.Get(GameUiKeys.IndustriesSummary), name, number);
            }

            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesSummaryGrowth),
                name,
                number,
                TextLine.Plain(DisplayNumber(item.GrowthTier).ToString(culture)),
                TextLine.Plain(DisplayNumber(item.MaxGrowthTier).ToString(culture)));
        }

        /// <summary>
        /// Formats "Current stock/max stock: 40/40 [icon]".
        /// </summary>
        public static TextLine FormatStockLine(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesStock),
                TextLine.Plain(item.CargoAmount.ToString(culture)),
                TextLine.Plain(item.CargoCapacity.ToString(culture)),
                FormatOutputIcons(item));
        }

        /// <summary>
        /// Formats "Last month production: 500 [icon]".
        /// </summary>
        public static TextLine FormatProductionLine(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesProduction),
                TextLine.Plain(item.LastMonthProduced.ToString(culture)),
                FormatOutputIcons(item));
        }

        /// <summary>
        /// Formats "Last month delivered: 450 [icon]".
        /// </summary>
        public static TextLine FormatDeliveredLine(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesDelivered),
                TextLine.Plain(item.LastMonthTransported.ToString(culture)),
                FormatOutputIcons(item));
        }

        /// <summary>
        /// Formats the growth progress: "Next level progress: 2/12 months", the max-level note, or the note that
        /// processing industries do not grow.
        /// </summary>
        public static TextLine FormatProgressLine(IndustryListItem item, ShellTextCache text, CultureInfo culture)
        {
            if (!item.CanGrow)
            {
                return TextLine.Plain(text.Get(GameUiKeys.IndustriesProgressNone));
            }

            if (item.GrowthTier >= item.MaxGrowthTier)
            {
                return TextLine.Plain(text.Get(GameUiKeys.IndustriesProgressMax));
            }

            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesProgress),
                TextLine.Plain(item.ServedMonths.ToString(culture)),
                TextLine.Plain(item.MonthsToGrow.ToString(culture)));
        }

        /// <summary>
        /// Formats the rules text from the growth data.
        /// </summary>
        public static TextLine FormatRules(IndustryGrowthRulesInfo rules, ShellTextCache text, CultureInfo culture)
        {
            if (rules is null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return TextLine.Format(
                text.Get(GameUiKeys.IndustriesRulesIntro),
                TextLine.Plain(rules.ServedPercent.ToString(culture)),
                TextLine.Plain(rules.MonthsToGrow.ToString(culture)),
                TextLine.Plain(rules.LevelMultiplier.ToString(culture)),
                TextLine.Plain(rules.LevelCount.ToString(culture)));
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

        private static int DisplayNumber(int zeroBased)
        {
            return zeroBased + 1;
        }

        private static TextLine FormatOutputIcons(IndustryListItem item)
        {
            IReadOnlyList<string> cargoIds = item.OutputCargoIds;
            if (cargoIds.Count == 0)
            {
                return TextLine.Empty;
            }

            var icons = new TextLine[cargoIds.Count];
            for (int index = 0; index < cargoIds.Count; index++)
            {
                icons[index] = CargoStackFormat.Name(cargoIds[index]);
            }

            return TextLine.Join(" ", icons);
        }
    }
}
