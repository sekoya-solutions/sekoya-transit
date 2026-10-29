#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using SekoyaTransit.Content;
using SekoyaTransit.Core.Content;
using UnityEngine;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Entry point: plays one game per seed and prints a yearly table plus a summary (see README.md).
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            BalanceOptions? options = BalanceOptions.Parse(args);
            if (options == null)
            {
                return 2;
            }

            if (!Directory.Exists(options.DataDirectory))
            {
                Console.Error.WriteLine("Content folder not found; pass --data <folder with the content JSON files>.");
                return 2;
            }

            ContentCatalog catalog = LoadCatalog(options.DataDirectory);
            Console.WriteLine($"Content: {options.DataDirectory}");
            Console.WriteLine($"Map {options.Size}, {options.Years} years, rail {(options.UseRail ? "on" : "off")}");
            foreach (ulong seed in options.Seeds)
            {
                GameResult result = new ScriptedPlayer(catalog, options, seed).Play();
                PrintGame(result, options.Verbose);
            }

            return 0;
        }

        private static ContentCatalog LoadCatalog(string directory)
        {
            TextAsset Read(string name)
            {
                return new TextAsset(name, File.ReadAllText(Path.Combine(directory, name + ".json")));
            }

            return ContentLoader.Load(
                Read("cargo"),
                Read("industries"),
                Read("towns"),
                Read("economy"),
                Read("vehicles"),
                Read("achievements"),
                Read("contracts"),
                Read("worldgen"));
        }

        private static void PrintGame(GameResult result, bool verbose)
        {
            Console.WriteLine();
            Console.WriteLine($"## Seed {result.Seed}: {result.Towns} towns, {result.Industries} industries");
            Console.WriteLine("year |       cash |      score |  income/yr | contracts | road/rail | routes +new | +veh | open | pop min/avg/max | >=2k/>=5k | needs met % | grown ind.");
            foreach (YearRow row in result.Years)
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | {1,10:N0} | {2,10:N0} | {3,10:N0} | {4,9:N0} | {5,4}/{6,-4} | {7,6} +{8,-3} | {9,4} | {10,4} | {11}/{12}/{13} | {14}/{15} | {16,11} | {17}",
                    row.Year,
                    row.Cash,
                    row.Score,
                    row.Income,
                    row.ContractBonus,
                    row.RoadVehicles,
                    row.Trains,
                    row.Routes,
                    row.RoutesOpened,
                    row.VehiclesAdded,
                    row.Opportunities,
                    row.MinPopulation,
                    row.AveragePopulation,
                    row.MaxPopulation,
                    row.TownsOver2k,
                    row.TownsOver5k,
                    row.NeedsMetPercent,
                    row.GrownIndustries));
            }

            Console.WriteLine();
            Console.WriteLine("Unlocks (available year, cost, cash when available, unlocked, wait):");
            foreach (UnlockRecord unlock in result.Unlocks.Values.OrderBy(u => u.AvailableYear))
            {
                string cash = unlock.CashWhenAvailable?.ToString("N0", CultureInfo.InvariantCulture) ?? "-";
                string unlocked = unlock.UnlockedOn?.ToString() ?? "never";
                string wait = unlock.WaitMonths == null ? "-" : unlock.WaitMonths.Value.ToString(CultureInfo.InvariantCulture) + " months";
                Console.WriteLine($"  {unlock.TypeId}: {unlock.AvailableYear}, {unlock.Cost.ToString("N0", CultureInfo.InvariantCulture)}, {cash}, {unlocked}, {wait}");
            }

            int busyYears = result.Years.Count(row => row.RoutesOpened > 0 || row.Opportunities > 0);
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  bankrupt: {result.BankruptOn?.ToString() ?? "no"}");
            Console.WriteLine($"  first $1M: {result.FirstMillionOn?.ToString() ?? "never"}, first $10M: {result.FirstTenMillionOn?.ToString() ?? "never"}");
            Console.WriteLine($"  years with new route work (opened or open opportunity): {busyYears} of {result.Years.Count}");
            Console.WriteLine($"  first level 3 delivery to a town: {result.LevelThreeTownDeliveryYear?.ToString(CultureInfo.InvariantCulture) ?? "never"}");
            if (verbose)
            {
                Console.WriteLine("Routes by lifetime income:");
                foreach (string route in result.Routes)
                {
                    Console.WriteLine("  " + route);
                }
            }
        }
    }
}
