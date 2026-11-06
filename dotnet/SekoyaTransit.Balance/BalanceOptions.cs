#nullable enable
using System;
using System.Globalization;
using System.IO;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Balance
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
        /// Gets the map sizes to play.
        /// </summary>
        public MapSize[] Sizes { get; private set; } = { MapSize.Small, MapSize.Medium, MapSize.Large };

        /// <summary>
        /// Gets the player profiles to play.
        /// </summary>
        public PlayerProfile[] Profiles { get; private set; } = PlayerProfile.All;

        /// <summary>
        /// Gets the world seeds to play, one game each.
        /// </summary>
        public ulong[] Seeds { get; private set; } = { 1UL, 2UL, 3UL };

        /// <summary>
        /// Gets the number of game years per game.
        /// </summary>
        public int Years { get; private set; } = 50;

        /// <summary>
        /// Gets whether the player may build rail routes.
        /// </summary>
        public bool UseRail { get; private set; } = true;

        /// <summary>
        /// Gets the shortest freight route, in tiles, that the player serves by rail.
        /// </summary>
        public int RailMinDistance { get; private set; } = 35;

        /// <summary>
        /// Gets the longest route, in tiles, the player considers; 0 uses a length that suits the map size.
        /// </summary>
        public int MaxRouteDistance { get; private set; }

        /// <summary>
        /// Gets the game months between two new routes; 0 uses the profile's pace.
        /// </summary>
        public int MonthsPerRoute { get; private set; }

        /// <summary>
        /// Gets whether each game's yearly table and unlocks are printed.
        /// </summary>
        public bool Tables { get; private set; }

        /// <summary>
        /// Gets whether every route and decision is printed.
        /// </summary>
        public bool Verbose { get; private set; }

        /// <summary>
        /// Gets how many games run at the same time; 0 means one per processor thread, up to one per game.
        /// </summary>
        public int Parallel { get; private set; }

        /// <summary>
        /// Gets whether the full game state is hashed every game month and the hashes printed by year.
        /// </summary>
        public bool StateHash { get; private set; }

        /// <summary>
        /// Gets how many games really run at the same time: verbose games stay sequential so their lines do not mix.
        /// </summary>
        public int GamesAtOnce
        {
            get
            {
                if (Verbose)
                {
                    return 1;
                }

                int requested = Parallel > 0 ? Parallel : Environment.ProcessorCount;
                return Math.Clamp(requested, 1, Math.Max(1, Seeds.Length * Sizes.Length * Profiles.Length));
            }
        }

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
                    case "--sizes":
                        options.Sizes = Array.ConvertAll(Next().Split(','), name => name switch
                        {
                            "small" => MapSize.Small,
                            "medium" => MapSize.Medium,
                            "large" => MapSize.Large,
                            string other => throw new ArgumentException($"Unknown map size '{other}'."),
                        });
                        break;
                    case "--profiles":
                        options.Profiles = Array.ConvertAll(Next().Split(','), PlayerProfile.Parse);
                        break;
                    case "--tables":
                        options.Tables = true;
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
                    case "--months-per-route":
                        options.MonthsPerRoute = int.Parse(Next(), NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    case "--verbose":
                        options.Verbose = true;
                        break;
                    case "--parallel":
                        options.Parallel = int.Parse(Next(), NumberStyles.None, CultureInfo.InvariantCulture);
                        break;
                    case "--state-hash":
                        options.StateHash = true;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument '{argument}'. See dotnet/SekoyaTransit.Balance/README.md.");
                        return null;
                }
            }

            return options;
        }

        // The shipped content sits at SekoyaTransit/Assets/_Project/Content/Data above the build output.
        private static string FindShippedContent()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, "SekoyaTransit", "Assets", "_Project", "Content", "Data");
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
