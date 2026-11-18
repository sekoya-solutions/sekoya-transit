#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Writes the Markdown report of <see cref="ContractPlayer"/> games: a yearly table per seed, the unlocks bought,
    /// every contract decision with its reason, and the routes built.
    /// </summary>
    internal static class ContractReport
    {
        public static string Write(BalanceOptions options, IReadOnlyList<ContractGameResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("# Contract player simulation");
            text.AppendLine();
            text.AppendLine(Invariant($"Map {options.Size}, {options.Years} years from 1900, seeds {string.Join(", ", options.Seeds)}."));
            text.AppendLine();
            text.AppendLine("Columns: cash on 31 December; income = deliveries + contract rewards of the year; net = change in cash over the year; " +
                            "routes = running road/rail routes (+ built this year); possible = town needs without a route that a route plan could still serve " +
                            "(\"none\" means no more route is possible); contracts = taken/fulfilled/failed this year (declined, cancelled), then totals taken/fulfilled; " +
                            "towns L1-L5 and primary industries by level (L1 to the top level in the data) = how many are at each level; " +
                            "waited for cash = contracts whose routes waited for money that year and the longest wait in months (the player decides once a month, " +
                            "so 1 means it waited up to one month), counting contracts still waiting on 31 December; the goal is never more than 1 month.");
            foreach (ContractGameResult result in results)
            {
                WriteGame(text, result);
            }

            return text.ToString();
        }

        private static void WriteGame(StringBuilder text, ContractGameResult result)
        {
            text.AppendLine();
            text.AppendLine(Invariant($"## Seed {result.Seed}: {result.Towns} towns, {result.Industries} industries"));
            text.AppendLine();
            text.AppendLine(Invariant(
                $"Bankrupt: {result.BankruptOn?.ToString() ?? "no"}. Contracts taken {result.Taken}, fulfilled {result.Fulfilled}, failed {result.Failed}, ") +
                Invariant($"declined {result.Declined}, cancelled {result.Cancelled}."));
            int missed = result.Years.Count(row => Longest(row) > 1);
            text.AppendLine();
            text.AppendLine(Invariant($"Cash wait goal (never more than 1 month): {(missed == 0 ? "met every year" : missed + " year(s) missed")}, longest wait {result.Years.Select(Longest).DefaultIfEmpty(0).Max()} month(s)."));
            text.AppendLine();
            string industryHeader = "Industries " + string.Join("/", Enumerable.Range(1, result.IndustryLevelCount).Select(level => "L" + level.ToString(CultureInfo.InvariantCulture)));
            text.AppendLine(Invariant($"| Year | Cash | Income | Net | Routes road/rail (+new, +sets on existing) | Possible | Contracts taken/ok/failed (decl., canc.) | Totals taken/ok | Towns L1/L2/L3/L4/L5 | {industryHeader} | Unlock bought | Waited for cash: contracts, longest (months) | Wait goal (1 month at most) |"));
            text.AppendLine("| --- | ---: | ---: | ---: | --- | ---: | --- | --- | --- | --- | --- | --- | --- |");
            foreach (ContractYearRow row in result.Years)
            {
                string possible = row.PossibleRoutes == 0 ? "none" : row.PossibleRoutes.ToString(CultureInfo.InvariantCulture);
                string towns = string.Join("/", Enumerable.Range(1, 5).Select(level => row.TownLevels.GetValueOrDefault(level)));
                string industries = string.Join("/", Enumerable.Range(1, result.IndustryLevelCount).Select(level => row.IndustryLevels.GetValueOrDefault(level)));
                string unlocks = row.Unlocks.Count == 0 ? "-" : string.Join(", ", row.Unlocks);
                text.AppendLine(Invariant(
                    $"| {row.Year} | {row.Cash:N0} | {row.Income:N0} | {row.CashChange:N0} | {row.RoadRoutes}/{row.RailRoutes} (+{row.RoadBuilt}/+{row.RailBuilt}, +{row.TopUpSets} sets) | {possible} | ") +
                    Invariant($"{row.Taken}/{row.Fulfilled}/{row.Failed} ({row.Declined}, {row.Cancelled}) | {row.TotalTaken}/{row.TotalFulfilled} | {towns} | {industries} | {unlocks} | {row.CashWaits.Count}, {Longest(row)} | {(Longest(row) <= 1 ? "met" : "missed")} |"));
            }

            text.AppendLine();
            text.AppendLine("### Cargo flow");
            text.AppendLine();
            text.AppendLine("Primary produced / loaded: units made by primary industries in the year and units loaded onto vehicles there. " +
                            "To towns: units delivered to towns by cargo level (raw/processed/finished). To industries: feeder deliveries. " +
                            "Route income by decade built: the year's delivery income of the routes built in the 1900s/1910s/1920s/1930s (route count).");
            text.AppendLine();
            text.AppendLine("| Year | Primary produced | Primary loaded | Loaded % | To towns raw/proc./fin. | To industries | Delivery income | Contract rewards | Route income by decade built |");
            text.AppendLine("| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- |");
            foreach (ContractYearRow row in result.Years)
            {
                (long produced, long loaded) = result.PrimaryFlow.GetValueOrDefault(row.Year);
                string percent = produced == 0 ? "-" : (100 * loaded / produced).ToString(CultureInfo.InvariantCulture);
                string towns = string.Join("/", Enumerable.Range(1, 3).Select(level => row.DeliveredToTowns.GetValueOrDefault(level).ToString("N0", CultureInfo.InvariantCulture)));
                string decades = string.Join(
                    ", ",
                    row.IncomeByDecadeBuilt.Select(pair => Invariant($"{pair.Key}s {pair.Value:N0} ({row.RoutesByDecadeBuilt[pair.Key]})")));
                text.AppendLine(Invariant($"| {row.Year} | {produced:N0} | {loaded:N0} | {percent} | {towns} | {row.DeliveredToIndustries:N0} | ") +
                                Invariant($"{row.DeliveryIncome:N0} | {row.ContractBonus:N0} | {decades} |"));
            }

            text.AppendLine();
            text.AppendLine("### Raw resources");
            text.AppendLine();
            text.AppendLine("Per raw cargo: units loaded / produced in the year (loaded %), stock left at its primary industries on 31 December " +
                            "as a share of their capacity, and routes leaving them. A cargo is short when nearly all of it is loaded and almost no stock is left. " +
                            "Before Issue #332 an industry loaded at most 1 unit per tick in total (about 3,000 a month); since then each waiting vehicle loads 1 unit per tick. Industry-months from 2,700 units loaded are counted for comparison. " +
                            "Town raw needs with a route: fully / partly / not met in November (December closes on the next year's first tick).");
            text.AppendLine();
            List<string> rawCargos = result.Years.SelectMany(row => row.RawStock.Keys).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
            text.AppendLine("| Year | " + string.Join(" | ", rawCargos) + " | Raw needs full/partly/unmet | Max loaded by one industry in a month | Industry-months with 2,700+ loaded |");
            text.AppendLine("| --- | " + string.Join(" | ", rawCargos.Select(_ => "---")) + " | --- | ---: | ---: |");
            foreach (ContractYearRow row in result.Years)
            {
                var cells = new List<string>();
                foreach (string cargo in rawCargos)
                {
                    (long produced, long loaded) = result.RawFlow.GetValueOrDefault((row.Year, cargo));
                    (long stock, long capacity) = row.RawStock.GetValueOrDefault(cargo);
                    string loadedPercent = produced == 0 ? "-" : (100 * loaded / produced).ToString(CultureInfo.InvariantCulture);
                    string stockPercent = capacity == 0 ? "-" : (100 * stock / capacity).ToString(CultureInfo.InvariantCulture);
                    cells.Add(Invariant($"{loaded:N0}/{produced:N0} ({loadedPercent} %), stock {stockPercent} %, {row.RawRoutes.GetValueOrDefault(cargo)} routes"));
                }

                text.AppendLine(Invariant($"| {row.Year} | ") + string.Join(" | ", cells) +
                                Invariant($" | {row.RawNeedsFull}/{row.RawNeedsPartly}/{row.RawNeedsUnmet} | {result.LoadCap.GetValueOrDefault(row.Year).Max:N0} | {result.LoadCap.GetValueOrDefault(row.Year).AtCap}/{result.LoadCap.GetValueOrDefault(row.Year).Months} |"));
            }

            text.AppendLine();
            text.AppendLine("### Contracts by cargo level");
            text.AppendLine();
            text.AppendLine("Taken = accepted or served (the starting ones included). Failed: average share of the amount delivered by the deadline.");
            text.AppendLine();
            text.AppendLine("| Cargo level | Taken | Fulfilled | Failed | Fulfilled % | Failed: delivered % | Declined + cancelled |");
            text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (int level in result.Events.Select(e => e.Level).Where(level => level > 0).Distinct().OrderBy(level => level))
            {
                List<ContractEvent> events = result.Events.Where(e => e.Level == level).ToList();
                int taken = events.Count(e => e.Kind is "accepted" or "served");
                int fulfilled = events.Count(e => e.Kind == "fulfilled");
                List<ContractEvent> failed = events.Where(e => e.Kind == "failed").ToList();
                int dropped = events.Count(e => e.Kind is "declined" or "cancelled");
                string fulfilledPercent = taken == 0 ? "-" : (100 * fulfilled / taken).ToString(CultureInfo.InvariantCulture);
                string deliveredPercent = failed.Count == 0 ? "-" : ((int)failed.Average(e => 100.0 * e.Delivered / e.Amount)).ToString(CultureInfo.InvariantCulture);
                text.AppendLine(Invariant($"| {level} | {taken} | {fulfilled} | {failed.Count} | {fulfilledPercent} | {deliveredPercent} | {dropped} |"));
            }

            text.AppendLine();
            text.AppendLine("### Finished goods chains");
            text.AppendLine();
            text.AppendLine("Per finished good: input delivered to its factories in the year, finished units made, and finished units loaded onto vehicles; " +
                            "then contracts of that good fulfilled/failed in the year.");
            text.AppendLine();
            text.AppendLine("| Year | " + string.Join(" | ", result.FinishedChains.Select(chain => Invariant($"{chain.Finished} ({chain.Input} in)"))) + " |");
            text.AppendLine("| --- | " + string.Join(" | ", result.FinishedChains.Select(_ => "---")) + " |");
            foreach (ContractYearRow row in result.Years)
            {
                var cells = new List<string>();
                foreach ((string finished, string input) in result.FinishedChains)
                {
                    (long made, long loaded) = result.RawFlow.GetValueOrDefault((row.Year, finished));
                    long delivered = row.DeliveredToIndustriesByCargo.GetValueOrDefault(input);
                    int ok = result.Events.Count(e => e.Cargo == finished && e.Kind == "fulfilled" && e.Date.Year == row.Year);
                    int ko = result.Events.Count(e => e.Cargo == finished && e.Kind == "failed" && e.Date.Year == row.Year);
                    cells.Add(Invariant($"in {delivered:N0}, made {made:N0}, loaded {loaded:N0}, contracts {ok}/{ko}"));
                }

                text.AppendLine(Invariant($"| {row.Year} | ") + string.Join(" | ", cells) + " |");
            }

            text.AppendLine();
            text.AppendLine("### Unlocks bought");
            text.AppendLine();
            if (result.UnlocksBought.Count == 0)
            {
                text.AppendLine("None.");
            }

            foreach ((string typeId, int availableYear, long cost, var boughtOn) in result.UnlocksBought)
            {
                text.AppendLine(Invariant($"- {typeId}: available {availableYear}, bought {boughtOn} for {cost:N0} ({boughtOn.Year - availableYear} year(s) after it appeared)"));
            }

            text.AppendLine();
            text.AppendLine("### Contracts declined, cancelled or failed");
            text.AppendLine();
            List<ContractEvent> problems = result.Events.Where(e => e.Kind is "declined" or "cancelled" or "failed").ToList();
            if (problems.Count == 0)
            {
                text.AppendLine("None.");
            }

            foreach (ContractEvent problem in problems)
            {
                text.AppendLine(Invariant($"- {problem.Date} {problem.Kind} {problem.Contract}: {problem.Detail}"));
            }

            text.AppendLine();
            text.AppendLine("### Contracts accepted or served");
            text.AppendLine();
            foreach (ContractEvent accepted in result.Events.Where(e => e.Kind is "accepted" or "served"))
            {
                text.AppendLine(Invariant($"- {accepted.Date} {accepted.Kind} {accepted.Contract}: {accepted.Detail}"));
            }

            text.AppendLine();
            text.AppendLine("### Routes built");
            text.AppendLine();
            foreach (string route in result.Routes)
            {
                text.AppendLine("- " + route);
            }
        }

        // The longest cash wait of the year: contracts served after waiting, or still waiting on 31 December.
        private static int Longest(ContractYearRow row)
        {
            return Math.Max(row.CashWaits.DefaultIfEmpty(0).Max(), row.OpenCashWait);
        }

        private static string Invariant(FormattableString value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
