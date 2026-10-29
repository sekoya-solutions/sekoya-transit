#nullable enable
using System;
using System.Globalization;
using System.IO;
using TransportTycoon.Core.World;

namespace TransportTycoon.Balance
{
    /// <summary>
    /// Command-line options of the balance simulation.
    /// </summary>
    internal sealed class BalanceOptions
    {
        private BalanceOptions()
        {
        }

        /// <summary>
        /// Gets the folder holding the content JSON files (the shipped content by default).
        /// </summary>
        public string DataDirectory { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the map size.
        /// </summary>
        public MapSize Size { get; private set; } = MapSize.Small;

        /// <summary>
        /// Gets the world seeds to play, one game each.
        /// </summary>
        public ulong[] Seeds { get; private set; } = { 1UL, 2UL, 3UL };

        /// <summary>
        /// Gets the number of game years per game.
        /// </summary>
        public int Years { get; private set; } = 20;

        /// <summary>
        /// Gets whether the player may build rail routes.
        /// </summary>
        public bool UseRail { get; private set; } = true;

        /// <summary>
        /// Gets the shortest freight route, in tiles, that the player serves by rail.
        /// </summary>
        public int RailMinDistance { get; private set; } = 35;

        /// <summary>
        /// Gets the longest route, in tiles, the player considers.
        /// </summary>
        public int MaxRouteDistance { get; private set; } = 70;

        /// <summary>
        /// Gets how many new routes the player opens per game month at most (human building pace).
        /// </summary>
        public int RoutesPerMonth { get; private set; } = 1;

        /// <summary>
        /// Gets whether the player replaces outdated vehicles.
        /// </summary>
        public bool ReplaceVehicles { get; private set; } = true;

        /// <summary>
        /// Gets whether every route and decision is printed.
        /// </summary>
        public bool Verbose { get; private set; }

        /// <summary>
        /// Parses arguments; returns null and prints usage on an unknown argument.
        /// </summary>
        public static BalanceOptions? Parse(string[] args)
        {
            var options = new BalanceOptions { DataDirectory = FindShippedContent() };
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                string Next()
                {
                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException($"Missing value after {argument}.");
                    }

                    return args[++index];
                }

                switch (argument)
                {
                    case "--data":
                        options.DataDirectory = Next();
                        break;
                    case "--size":
                        options.Size = Next() switch
                        {
                            "small" => MapSize.Small,
                            "medium" => MapSize.Medium,
                            "large" => MapSize.Large,
                            string other => throw new ArgumentException($"Unknown map size '{other}'."),
                        };
                        break;
                    case "--seeds":
                        options.Seeds = Array.ConvertAll(
                            Next().Split(','),
                            seed => ulong.Parse(seed, NumberStyles.None, CultureInfo.InvariantCulture));
                        break;
                    case "--years":
                        options.Years = int.Parse(Next(), NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    case "--no-rail":
                        options.UseRail = false;
                        break;
                    case "--rail-min-distance":
                        options.RailMinDistance = int.Parse(Next(), NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    case "--max-route-distance":
                        options.MaxRouteDistance = int.Parse(Next(), NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    case "--routes-per-month":
                        options.RoutesPerMonth = int.Parse(Next(), NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    case "--no-replace":
                        options.ReplaceVehicles = false;
                        break;
                    case "--verbose":
                        options.Verbose = true;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument '{argument}'. See dotnet/TransportTycoon.Balance/README.md.");
                        return null;
                }
            }

            return options;
        }

        // The shipped content sits at TransportTycoon2/Assets/_Project/Content/Data above the build output.
        private static string FindShippedContent()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, "TransportTycoon2", "Assets", "_Project", "Content", "Data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return string.Empty;
        }
    }
}
