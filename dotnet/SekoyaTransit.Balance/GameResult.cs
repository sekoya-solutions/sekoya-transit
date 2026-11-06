#nullable enable
using System.Collections.Generic;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// What happened in one simulated game.
    /// </summary>
    internal sealed class GameResult
    {
        public GameResult(string profile, string size, ulong seed, int towns, int industries)
        {
            Profile = profile;
            Size = size;
            Seed = seed;
            Towns = towns;
            Industries = industries;
        }

        public string Profile { get; }

        public string Size { get; }

        public ulong Seed { get; }

        public int Towns { get; }

        public int Industries { get; }

        public List<YearRow> Years { get; } = new List<YearRow>();

        public SortedDictionary<string, UnlockRecord> Unlocks { get; } = new SortedDictionary<string, UnlockRecord>();

        public List<string> Routes { get; } = new List<string>();

        public GameDate? BankruptOn { get; set; }

        public GameDate? FirstMillionOn { get; set; }

        public GameDate? FirstTenMillionOn { get; set; }

        public int? LevelThreeTownDeliveryYear { get; set; }

        /// <summary>
        /// Gets the fractional year the best town first reached tiers 2 to 5 (infinity when never).
        /// </summary>
        public double[] BestTownTierYears { get; } = { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };

        /// <summary>
        /// Gets how many towns were at growth level 2 or more on 1 January 1910, level 3 or more in 1920, level 4 or more
        /// in 1930, and level 5 in 1940.
        /// </summary>
        public int[] TownsAtMilestoneLevel { get; } = new int[4];

        /// <summary>
        /// Gets the ids of the contracts the player saw.
        /// </summary>
        public HashSet<int> ContractsSeen { get; } = new HashSet<int>();

        /// <summary>
        /// Gets the contracts claimed by cargo level; index 0 is level 1.
        /// </summary>
        public int[] ClaimedByLevel { get; } = new int[3];

        /// <summary>
        /// Gets the contracts failed by cargo level; index 0 is level 1.
        /// </summary>
        public int[] FailedByLevel { get; } = new int[3];

        /// <summary>
        /// Gets or sets how many failed contracts had no route to the town when they failed.
        /// </summary>
        public int FailedWithoutRoute { get; set; }

        /// <summary>
        /// Gets or sets the sum over failed contracts of the delivered percentage of the amount.
        /// </summary>
        public double FailedDeliveredPercentSum { get; set; }

        /// <summary>
        /// Gets, per decade from the 1900s, the months in which the next route was unaffordable.
        /// </summary>
        public int[] MonthsShortOfMoney { get; } = new int[5];

        /// <summary>
        /// Gets, per decade from the 1900s, the months in which a new route was due and none was left to open.
        /// </summary>
        public int[] MonthsWithoutRoute { get; } = new int[5];

        /// <summary>
        /// Gets or sets the first date a new route was due and none was left to open.
        /// </summary>
        public GameDate? FirstWithoutRoute { get; set; }

        public int ContractsClaimed { get; set; }

        public int ContractsFailed { get; set; }

        public int ContractsDropped { get; set; }
    }

    /// <summary>
    /// End-of-year state of a simulated game.
    /// </summary>
    internal sealed class YearRow
    {
        public int Year { get; set; }

        public long Cash { get; set; }

        public long Score { get; set; }

        public long Income { get; set; }

        public long ContractBonus { get; set; }

        public int RoadVehicles { get; set; }

        public int Trains { get; set; }

        public int Routes { get; set; }

        public int RoutesOpened { get; set; }

        public int VehiclesAdded { get; set; }

        public int Opportunities { get; set; }

        public int MinPopulation { get; set; }

        public int AveragePopulation { get; set; }

        public int MaxPopulation { get; set; }

        public int TownsOver2k { get; set; }

        public int TownsOver5k { get; set; }

        public int NeedsMetPercent { get; set; }

        public int GrownIndustries { get; set; }

        /// <summary>
        /// Gets or sets the rolling hash of every monthly game state so far, or null when hashing is off.
        /// </summary>
        public string? StateHash { get; set; }
    }

    /// <summary>
    /// When a lockable vehicle type became available and when the player bought its unlock.
    /// </summary>
    internal sealed class UnlockRecord
    {
        public UnlockRecord(string typeId, int availableYear, long cost)
        {
            TypeId = typeId;
            AvailableYear = availableYear;
            Cost = cost;
        }

        public string TypeId { get; }

        public int AvailableYear { get; }

        public long Cost { get; }

        public long? CashWhenAvailable { get; set; }

        public GameDate? UnlockedOn { get; set; }

        /// <summary>
        /// Gets the months between January of the available year and the unlock, or null when never unlocked.
        /// </summary>
        public int? WaitMonths
        {
            get
            {
                return UnlockedOn == null
                    ? (int?)null
                    : ((UnlockedOn.Value.Year - AvailableYear) * 12) + UnlockedOn.Value.Month - 1;
            }
        }
    }
}
