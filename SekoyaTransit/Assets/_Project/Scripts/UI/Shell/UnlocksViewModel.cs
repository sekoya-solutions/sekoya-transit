#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Builds the vehicles unlock window (Issue #178, reworked in Issue #286): one column per vehicle category, one card
    /// per vehicle type, and decides when the structure changed.
    /// </summary>
    public static class UnlocksViewModel
    {
        /// <summary>
        /// Builds the four columns (trucks, trains, boats, planes). Boats and planes have no vehicles yet, so each shows
        /// one greyed "Coming soon" card; the types of each mode keep the order of <paramref name="unlocks"/>.
        /// </summary>
        public static IReadOnlyList<UnlockColumnRow> BuildColumns(
            IReadOnlyList<VehicleUnlockListItem> unlocks,
            ShellTextCache text,
            CultureInfo culture)
        {
            if (unlocks is null)
            {
                throw new ArgumentNullException(nameof(unlocks));
            }

            var trucks = new List<UnlockCardRow>();
            var trains = new List<UnlockCardRow>();
            for (int index = 0; index < unlocks.Count; index++)
            {
                VehicleUnlockListItem item = unlocks[index];
                (item.Kind == VehicleListKind.Rail ? trains : trucks).Add(BuildCard(item, text, culture));
            }

            return new[]
            {
                new UnlockColumnRow(text.Get(GameUiKeys.UnlocksColumnTrucks), trucks),
                new UnlockColumnRow(text.Get(GameUiKeys.UnlocksColumnTrains), trains),
                new UnlockColumnRow(text.Get(GameUiKeys.UnlocksColumnBoats), new[] { BuildComingSoon(text) }),
                new UnlockColumnRow(text.Get(GameUiKeys.UnlocksColumnPlanes), new[] { BuildComingSoon(text) }),
            };
        }

        /// <summary>
        /// Builds the card of one vehicle type: header with the year, stat lines, and the owned / buy / not-yet footer.
        /// </summary>
        public static UnlockCardRow BuildCard(VehicleUnlockListItem item, ShellTextCache text, CultureInfo culture)
        {
            bool future = item.Status == VehicleUnlockRowStatus.Future;
            string header = string.Format(
                culture,
                text.Get(future ? GameUiKeys.UnlocksCardUnlocks : GameUiKeys.UnlocksCardUnlocked),
                text.Get(item.NameKey),
                item.AvailableYear.ToString(culture));

            UnlockCardKind kind;
            string footer;
            switch (item.Status)
            {
                case VehicleUnlockRowStatus.Future:
                    kind = UnlockCardKind.Future;
                    footer = text.Get(GameUiKeys.UnlocksStatusFuture);
                    break;
                case VehicleUnlockRowStatus.Unlockable:
                    kind = UnlockCardKind.Buyable;
                    footer = CashFormat.FormatLabeled(text.Get(GameUiKeys.UnlocksAction), item.UnlockCost, culture);
                    break;
                default:
                    kind = UnlockCardKind.Owned;
                    footer = text.Get(GameUiKeys.UnlocksStatusOwned);
                    break;
            }

            return new UnlockCardRow(
                item.TypeId,
                header,
                FormatStats(item, text, culture),
                kind,
                footer,
                item.CanAffordUnlock);
        }

        /// <summary>
        /// Formats the stat lines of a card: capacity (per wagon for a train), speed, wagons for a train, price, upkeep.
        /// </summary>
        public static IReadOnlyList<string> FormatStats(
            VehicleUnlockListItem item,
            ShellTextCache text,
            CultureInfo culture)
        {
            VehicleUnlockStats stats = item.Stats;
            bool rail = item.Kind == VehicleListKind.Rail;
            var lines = new List<string>
            {
                string.Format(
                    culture,
                    text.Get(rail ? GameUiKeys.UnlocksStatWagonCapacity : GameUiKeys.UnlocksStatCapacity),
                    stats.CargoCapacity.ToString(culture)),
                string.Format(culture, text.Get(GameUiKeys.UnlocksStatSpeed), FormatSpeed(stats, culture)),
            };
            if (rail)
            {
                lines.Add(string.Format(
                    culture,
                    text.Get(GameUiKeys.UnlocksStatMaxWagons),
                    stats.MaxWagons.ToString(culture)));
            }

            lines.Add(CashFormat.FormatLabeled(text.Get(GameUiKeys.UnlocksStatPrice), stats.Purchase, culture));
            lines.Add(FormatUpkeep(stats, text, culture));
            return lines;
        }

        /// <summary>
        /// Formats the upkeep line with the upkeep of one vehicle, never a value per unit (Issue #314): a single amount
        /// when the lowest and highest upkeep are equal (a truck), otherwise the range from a train with 1 wagon to a
        /// train with its maximum wagons.
        /// </summary>
        public static string FormatUpkeep(VehicleUnlockStats stats, ShellTextCache text, CultureInfo culture)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (stats.MinUpkeepPerMonth == stats.MaxUpkeepPerMonth)
            {
                return CashFormat.FormatLabeled(text.Get(GameUiKeys.UnlocksStatUpkeep), stats.MinUpkeepPerMonth, culture);
            }

            return string.Format(
                culture,
                text.Get(GameUiKeys.UnlocksStatUpkeepRange),
                CashFormat.FormatAmount(stats.MinUpkeepPerMonth, culture),
                CashFormat.FormatAmount(stats.MaxUpkeepPerMonth, culture));
        }

        /// <summary>
        /// Formats the speed in tiles per second with one decimal (5.4).
        /// </summary>
        public static string FormatSpeed(VehicleUnlockStats stats, CultureInfo culture)
        {
            return (stats.SpeedTenthsTilesPerSecond / 10m).ToString("0.0", culture);
        }

        /// <summary>
        /// Converts a data speed (thousandths of a tile per simulation tick) to tenths of a tile per second.
        /// </summary>
        public static int SpeedTenthsPerSecond(int speedThousandthsPerTick, double tickSeconds)
        {
            if (tickSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "Tick duration must be positive.");
            }

            return (int)Math.Round(speedThousandthsPerTick / 100d / tickSeconds, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Fingerprint of the card structure: it changes when a status, cost, affordability, or stat changes (a buy button
        /// exists only for buyable cards).
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
                    hash = (hash * 397) ^ item.Stats.GetHashCode();
                }

                return hash;
            }
        }

        private static UnlockCardRow BuildComingSoon(ShellTextCache text)
        {
            return new UnlockCardRow(
                null,
                text.Get(GameUiKeys.UnlocksComingSoon),
                Array.Empty<string>(),
                UnlockCardKind.ComingSoon,
                string.Empty,
                false);
        }
    }

    /// <summary>
    /// What the footer of an unlock card shows.
    /// </summary>
    public enum UnlockCardKind
    {
        /// <summary>
        /// Starting type or already unlocked: "Already owned".
        /// </summary>
        Owned = 0,

        /// <summary>
        /// Year reached, not bought yet: a buy button.
        /// </summary>
        Buyable = 1,

        /// <summary>
        /// Year not reached: greyed out, "Not yet available".
        /// </summary>
        Future = 2,

        /// <summary>
        /// Category without vehicles yet (boats, planes): greyed out, no stats.
        /// </summary>
        ComingSoon = 3,
    }

    /// <summary>
    /// One painted unlock card.
    /// </summary>
    public sealed class UnlockCardRow
    {
        /// <summary>
        /// Initializes a card.
        /// </summary>
        public UnlockCardRow(
            string? typeId,
            string header,
            IReadOnlyList<string> statLines,
            UnlockCardKind kind,
            string footer,
            bool canBuy)
        {
            TypeId = typeId;
            Header = header ?? throw new ArgumentNullException(nameof(header));
            StatLines = statLines ?? throw new ArgumentNullException(nameof(statLines));
            Kind = kind;
            Footer = footer ?? throw new ArgumentNullException(nameof(footer));
            CanBuy = canBuy;
        }

        /// <summary>
        /// Gets the vehicle type id, or null for a "Coming soon" card.
        /// </summary>
        public string? TypeId { get; }

        /// <summary>
        /// Gets the header: name and unlock year.
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// Gets the stat lines shown beside the sprite.
        /// </summary>
        public IReadOnlyList<string> StatLines { get; }

        /// <summary>
        /// Gets the footer kind.
        /// </summary>
        public UnlockCardKind Kind { get; }

        /// <summary>
        /// Gets the footer text (owned text, buy button text, or not-yet text).
        /// </summary>
        public string Footer { get; }

        /// <summary>
        /// Gets whether the buy button is enabled (company cash covers the unlock cost).
        /// </summary>
        public bool CanBuy { get; }

        /// <summary>
        /// Gets whether the card is greyed out (year not reached, or a category without vehicles).
        /// </summary>
        public bool IsGreyedOut => Kind == UnlockCardKind.Future || Kind == UnlockCardKind.ComingSoon;
    }

    /// <summary>
    /// One column of the unlock window.
    /// </summary>
    public sealed class UnlockColumnRow
    {
        /// <summary>
        /// Initializes a column.
        /// </summary>
        public UnlockColumnRow(string title, IReadOnlyList<UnlockCardRow> cards)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
        }

        /// <summary>
        /// Gets the column title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the cards, in order.
        /// </summary>
        public IReadOnlyList<UnlockCardRow> Cards { get; }
    }
}
