#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Balance
{
    /// <summary>
    /// Writes the games of one map size as GitHub-flavoured Markdown: a table of the progression targets of
    /// docs/specs/economy-progression.md with one column per seed, each cell marked pass, fail or not applicable, and a
    /// one-row overview for a recap of several map sizes.
    /// </summary>
    internal static class MarkdownReport
    {
        private static readonly string _pass = "✅";
        private static readonly string _fail = "❌";
        private static readonly string _neutral = "⚪";
        private const int _minFirstMillionYear = 5;
        private const int _minFirstTenMillionYear = 15;
        private const int _maxLevelThreeYear = 10;
        private const int _minUnlockWaitMonths = 6;
        private const int _maxUnlockWaitMonths = 36;

        /// <summary>
        /// Builds the detailed report of one map size.
        /// </summary>
        public static string BuildDetails(IReadOnlyList<GameResult> results, BalanceOptions options, ContentCatalog catalog)
        {
            Tally tally = Evaluate(results, options, catalog, out List<Row> rows, out List<Row> unlockRows);
            var text = new StringBuilder();
            text.Append("### ").Append(SizeName(options.Size)).Append(" map: ")
                .Append(tally.Passed.ToString(CultureInfo.InvariantCulture)).Append(" of ")
                .Append((tally.Passed + tally.Failed).ToString(CultureInfo.InvariantCulture)).Append(" checks pass ")
                .Append(tally.Failed == 0 ? _pass : _fail).Append("\n\n");
            text.Append(SeedsLine(results, options)).Append("\n\n");
            AppendTable(text, "Check", results, rows);
            text.Append('\n');
            AppendTable(text, "Unlock (year available)", results, unlockRows);
            text.Append('\n').Append(_pass).Append(" pass · ").Append(_fail).Append(" fail · ").Append(_neutral)
                .Append(" not counted (out of reach at the end of the timeline, or the wait window is still open)\n");
            return text.ToString();
        }

        /// <summary>
        /// Builds one row of the overview table (columns: map, seeds, checks passed, failed, bankruptcies; the
        /// balance workflow writes the header).
        /// </summary>
        public static string BuildOverviewRow(IReadOnlyList<GameResult> results, BalanceOptions options, ContentCatalog catalog)
        {
            Tally tally = Evaluate(results, options, catalog, out _, out _);
            int bankruptcies = results.Count(result => result.BankruptOn != null);
            return string.Format(
                CultureInfo.InvariantCulture,
                "| {0} | {1} | {2} {3} of {4} | {5} | {6} |\n",
                SizeName(options.Size),
                string.Join(", ", results.Select(result => result.Seed.ToString(CultureInfo.InvariantCulture))),
                tally.Failed == 0 ? _pass : _fail,
                tally.Passed,
                tally.Passed + tally.Failed,
                tally.Failed,
                bankruptcies);
        }

        private static Tally Evaluate(
            IReadOnlyList<GameResult> results,
            BalanceOptions options,
            ContentCatalog catalog,
            out List<Row> rows,
            out List<Row> unlockRows)
        {
            int firstYear = GameDate.FirstYear;
            int lastYear = firstYear + options.Years - 1;
            rows = new List<Row>
            {
                new Row("Bankruptcy", "none", results.Select(r => r.BankruptOn == null
                    ? Cell.Passed("no")
                    : Cell.Failed(r.BankruptOn.Value.ToString()))),
                new Row("First $1M", $"after year {_minFirstMillionYear}", results.Select(r => r.FirstMillionOn == null
                    ? Cell.Passed("not reached")
                    : Cell.Check(r.FirstMillionOn.Value.Year - firstYear >= _minFirstMillionYear, Year(r.FirstMillionOn.Value.Year)))),
                new Row("First $10M", $"after year {_minFirstTenMillionYear}", results.Select(r => r.FirstTenMillionOn == null
                    ? Cell.Passed("not reached")
                    : Cell.Check(r.FirstTenMillionOn.Value.Year - firstYear >= _minFirstTenMillionYear, Year(r.FirstTenMillionOn.Value.Year)))),
                new Row("Score at the end", "–", results.Select(r => Cell.Info(Money(r.Years.Count == 0 ? 0L : r.Years[r.Years.Count - 1].Score)))),
                new Row("Years with new route work", "most years", results.Select(r =>
                {
                    int busy = r.Years.Count(row => row.RoutesOpened > 0 || row.Opportunities > 0);
                    return Cell.Check(busy * 2 > r.Years.Count, $"{busy} of {r.Years.Count}");
                })),
                new Row("First level 3 good delivered to a town", $"by year {_maxLevelThreeYear}", results.Select(r => r.LevelThreeTownDeliveryYear == null
                    ? Cell.Failed("never")
                    : Cell.Check(r.LevelThreeTownDeliveryYear.Value - firstYear <= _maxLevelThreeYear, Year(r.LevelThreeTownDeliveryYear.Value)))),
            };

            unlockRows = new List<Row>();
            foreach (VehicleDefinition vehicle in catalog.Vehicles.Where(v => v.UnlockCost > 0).OrderBy(v => v.AvailableYear))
            {
                bool lastOfMode = !catalog.Vehicles.Any(other => other.Mode == vehicle.Mode && other.AvailableYear > vehicle.AvailableYear);
                unlockRows.Add(new Row(
                    $"{vehicle.Id} ({Year(vehicle.AvailableYear)})",
                    lastOfMode ? $"{_minUnlockWaitMonths}–{_maxUnlockWaitMonths} months, or out of reach" : $"{_minUnlockWaitMonths}–{_maxUnlockWaitMonths} months",
                    results.Select(r => UnlockCell(r, vehicle, lastOfMode, lastYear))));
            }

            var tally = new Tally();
            foreach (Cell cell in rows.Concat(unlockRows).SelectMany(row => row.Cells))
            {
                if (cell.Status == _pass)
                {
                    tally.Passed++;
                }
                else if (cell.Status == _fail)
                {
                    tally.Failed++;
                }
            }

            return tally;
        }

        private static Cell UnlockCell(GameResult result, VehicleDefinition vehicle, bool lastOfMode, int lastYear)
        {
            if (vehicle.AvailableYear > lastYear)
            {
                return Cell.Info("not available yet");
            }

            if (!result.Unlocks.TryGetValue(vehicle.Id, out UnlockRecord? unlock) || unlock.WaitMonths == null)
            {
                int monthsAvailable = ((lastYear - vehicle.AvailableYear) * 12) + 12;
                if (lastOfMode || monthsAvailable <= _maxUnlockWaitMonths)
                {
                    return Cell.Neutral("not bought");
                }

                return Cell.Failed("not bought");
            }

            int wait = unlock.WaitMonths.Value;
            return Cell.Check(
                wait >= _minUnlockWaitMonths && wait <= _maxUnlockWaitMonths,
                wait.ToString(CultureInfo.InvariantCulture) + (wait == 1 ? " month" : " months"));
        }

        private static void AppendTable(StringBuilder text, string firstHeader, IReadOnlyList<GameResult> results, List<Row> rows)
        {
            text.Append("| ").Append(firstHeader).Append(" | Target |");
            foreach (GameResult result in results)
            {
                text.Append(" Seed ").Append(result.Seed.ToString(CultureInfo.InvariantCulture)).Append(" |");
            }

            text.Append("\n| --- | --- |").Append(string.Concat(Enumerable.Repeat(" --- |", results.Count))).Append('\n');
            foreach (Row row in rows)
            {
                text.Append("| ").Append(row.Name).Append(" | ").Append(row.Target).Append(" |");
                foreach (Cell cell in row.Cells)
                {
                    text.Append(' ');
                    if (cell.Status.Length > 0)
                    {
                        text.Append(cell.Status).Append(' ');
                    }

                    text.Append(cell.Text).Append(" |");
                }

                text.Append('\n');
            }
        }

        private static string SeedsLine(IReadOnlyList<GameResult> results, BalanceOptions options)
        {
            GameResult? first = results.Count > 0 ? results[0] : null;
            string towns = first == null ? "0" : first.Towns.ToString(CultureInfo.InvariantCulture);
            string industries = first == null ? "0" : first.Industries.ToString(CultureInfo.InvariantCulture);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} game years per seed, rail {1}; seed {2}: {3} towns, {4} industries.",
                options.Years,
                options.UseRail ? "on" : "off",
                first?.Seed ?? 0UL,
                towns,
                industries);
        }

        private static string SizeName(MapSize size)
        {
            return size switch
            {
                MapSize.Small => "Small",
                MapSize.Medium => "Medium",
                MapSize.Large => "Large",
                _ => size.ToString(),
            };
        }

        private static string Year(int year)
        {
            return year.ToString(CultureInfo.InvariantCulture);
        }

        private static string Money(long amount)
        {
            return "$" + (amount / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        }

        private sealed class Tally
        {
            public int Passed { get; set; }

            public int Failed { get; set; }
        }

        private sealed class Row
        {
            public Row(string name, string target, IEnumerable<Cell> cells)
            {
                Name = name;
                Target = target;
                Cells = cells.ToList();
            }

            public string Name { get; }

            public string Target { get; }

            public List<Cell> Cells { get; }
        }

        private sealed class Cell
        {
            private Cell(string status, string text)
            {
                Status = status;
                Text = text;
            }

            public string Status { get; }

            public string Text { get; }

            public static Cell Passed(string text)
            {
                return new Cell(_pass, text);
            }

            public static Cell Failed(string text)
            {
                return new Cell(_fail, text);
            }

            public static Cell Neutral(string text)
            {
                return new Cell(_neutral, text);
            }

            public static Cell Info(string text)
            {
                return new Cell(string.Empty, text);
            }

            public static Cell Check(bool passed, string text)
            {
                return passed ? Passed(text) : Failed(text);
            }
        }
    }
}
