#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Summarizes the games of every map size and checks them against the balance targets of
    /// docs/specs/economy-progression.md. Town milestones use each player's worst game; unlock waits use Steady's median.
    /// </summary>
    internal static class BalanceReport
    {
        private const int _maxTownsPercent = 50;
        private const int _firstWaitMonths = 12;
        private const int _lastWaitMonths = 48;
        private static readonly int[] _milestoneYears = { 1910, 1920, 1930, 1940 };
        private static readonly int[] _routeYears = { 1900, 1905, 1910, 1920, 1930 };

        /// <summary>
        /// Prints each map size's results and targets; returns how many targets failed.
        /// </summary>
        public static int Print(IReadOnlyList<GameResult> games)
        {
            int failed = 0;
            foreach (IGrouping<string, GameResult> size in games.GroupBy(game => game.Size))
            {
                List<GameResult> sizeGames = size.ToList();
                List<GameResult> steady = sizeGames.Where(game => game.Profile == PlayerProfile.Steady.Name).ToList();
                Console.WriteLine();
                Console.WriteLine($"# {size.Key}");
                foreach (IGrouping<string, GameResult> profile in sizeGames.GroupBy(game => game.Profile))
                {
                    List<GameResult> list = profile.ToList();
                    Console.WriteLine($"{profile.Key}: {list.Count} games, {list.Count(game => game.BankruptOn != null)} bankrupt");
                    Console.WriteLine("  best town reaches levels 2 / 3 / 4 / 5: " + string.Join(" / ", Enumerable.Range(0, _milestoneYears.Length).Select(index => Year(Median(list.Select(game => game.BestTownTierYears[index]))))));
                    Console.WriteLine("  contracts seen / claimed / failed / dropped: " + string.Join(" / ", new[]
                    {
                        Median(list.Select(game => (double)game.ContractsSeen.Count)),
                        Median(list.Select(game => (double)game.ContractsClaimed)),
                        Median(list.Select(game => (double)game.ContractsFailed)),
                        Median(list.Select(game => (double)game.ContractsDropped)),
                    }.Select(value => value.ToString("F0", CultureInfo.InvariantCulture))));
                    GameResult first = list[0];
                    Console.WriteLine($"  first game, contracts claimed / failed by level: {first.ClaimedByLevel[0]}/{first.FailedByLevel[0]} {first.ClaimedByLevel[1]}/{first.FailedByLevel[1]} {first.ClaimedByLevel[2]}/{first.FailedByLevel[2]}");
                    double delivered = first.ContractsFailed == 0 ? 0.0 : first.FailedDeliveredPercentSum / first.ContractsFailed;
                    Console.WriteLine($"  first game, failed contracts: {first.FailedWithoutRoute} with no route, {delivered.ToString("F0", CultureInfo.InvariantCulture)} % delivered on average");
                    YearRow last = first.Years[first.Years.Count - 1];
                    Console.WriteLine($"  first game, at the end: {last.RoadVehicles} trucks, {last.Trains} trains, {last.Routes} routes, {last.Cash.ToString("N0", CultureInfo.InvariantCulture)} cash");
                    foreach (UnlockRecord unlock in first.Unlocks.Values.OrderBy(unlock => unlock.AvailableYear))
                    {
                        Console.WriteLine($"  first game, unlock {unlock.TypeId}: appears {unlock.AvailableYear}, costs {unlock.Cost.ToString("N0", CultureInfo.InvariantCulture)}, bought {unlock.UnlockedOn?.ToString() ?? "never"}, after {Months(WaitOf(unlock))} months");
                    }

                    Console.WriteLine("  first game, months per decade from the 1900s, short of money for the next route: " + string.Join(" / ", first.MonthsShortOfMoney) + "; no route left: " + string.Join(" / ", first.MonthsWithoutRoute));
                    Console.WriteLine("  first game, routes at the end of 1900 / 1905 / 1910 / 1920 / 1930: " + string.Join(" / ", _routeYears.Select(year => first.Years.First(row => row.Year == year).Routes)));
                }

                if (steady.Count > 0)
                {
                    PrintUnlocks(steady);
                }

                failed += PrintTargets(sizeGames, steady);
            }

            return failed;
        }

        private static void PrintUnlocks(List<GameResult> steady)
        {
            Console.WriteLine("steady median wait per unlock:");
            foreach (UnlockRecord unlock in steady[0].Unlocks.Values.OrderBy(unlock => unlock.AvailableYear))
            {
                double wait = Median(steady.Select(game => WaitOf(game.Unlocks[unlock.TypeId])));
                Console.WriteLine($"  {unlock.TypeId}: {unlock.AvailableYear}, {unlock.Cost.ToString("N0", CultureInfo.InvariantCulture)}, {Months(wait)} months");
            }
        }

        private static int PrintTargets(List<GameResult> games, List<GameResult> steady)
        {
            var checks = new List<(string Name, bool Pass, string Detail)>();
            if (steady.Count > 0)
            {
                var waits = steady[0].Unlocks.Keys
                    .Select(id => Median(steady.Select(game => WaitOf(game.Unlocks[id]))))
                    .ToList();
                checks.Add((
                    "steady buys each unlock after 1 to 4 years",
                    waits.All(wait => wait >= _firstWaitMonths && wait <= _lastWaitMonths),
                    $"{Months(waits.Min())} to {Months(waits.Max())} months"));
            }

            foreach (IGrouping<string, GameResult> profile in games.GroupBy(game => game.Profile))
            {
                for (int index = 0; index < _milestoneYears.Length; index++)
                {
                    int milestone = index;
                    GameResult worst = profile.OrderByDescending(game => game.TownsAtMilestoneLevel[milestone]).First();
                    int towns = worst.TownsAtMilestoneLevel[milestone];
                    checks.Add((
                        $"{profile.Key} {_milestoneYears[milestone]}: level {milestone + 2}+ towns at most 50 %",
                        towns * 100 <= worst.Towns * _maxTownsPercent,
                        $"{towns} of {worst.Towns} ({100.0 * towns / worst.Towns:F0} %)"));
                }
            }

            foreach (IGrouping<string, GameResult> profile in games.GroupBy(game => game.Profile))
            {
                int months = profile.Sum(game => game.MonthsWithoutRoute.Sum());
                string first = profile.Min(game => game.FirstWithoutRoute)?.ToString() ?? "never";
                checks.Add(($"{profile.Key} always has a route left", months == 0, $"{months} months without one, first {first}"));
            }

            int bankrupt = games.Count(game => game.BankruptOn != null);
            checks.Add(("no bankruptcy", bankrupt == 0, $"{bankrupt} of {games.Count} games"));
            Console.WriteLine("targets:");
            foreach ((string name, bool pass, string detail) in checks)
            {
                Console.WriteLine($"  [{(pass ? "pass" : "FAIL")}] {name}: {detail}");
            }

            return checks.Count(check => !check.Pass);
        }

        private static double WaitOf(UnlockRecord unlock)
        {
            return unlock.WaitMonths ?? double.PositiveInfinity;
        }

        private static double Median(IEnumerable<double> values)
        {
            List<double> sorted = values.OrderBy(value => value).ToList();
            return sorted[sorted.Count / 2];
        }

        private static string Year(double year)
        {
            return double.IsPositiveInfinity(year) ? "never" : year.ToString("F0", CultureInfo.InvariantCulture);
        }

        private static string Months(double months)
        {
            return double.IsPositiveInfinity(months) ? "never" : months.ToString("F0", CultureInfo.InvariantCulture);
        }
    }
}
