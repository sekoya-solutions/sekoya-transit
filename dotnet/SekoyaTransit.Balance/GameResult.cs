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
        public GameResult(ulong seed, int towns, int industries)
        {
            Seed = seed;
            Towns = towns;
            Industries = industries;
        }

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
