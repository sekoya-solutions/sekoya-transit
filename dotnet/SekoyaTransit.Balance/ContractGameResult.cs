#nullable enable
using System.Collections.Generic;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// What happened in one game played by the <see cref="ContractPlayer"/>.
    /// </summary>
    internal sealed class ContractGameResult
    {
        public ContractGameResult(ulong seed, int towns, int industries)
        {
            Seed = seed;
            Towns = towns;
            Industries = industries;
        }

        public ulong Seed { get; }

        public int Towns { get; }

        public int Industries { get; }

        public List<ContractYearRow> Years { get; } = new List<ContractYearRow>();

        public List<(string TypeId, int AvailableYear, long Cost, GameDate BoughtOn)> UnlocksBought { get; } =
            new List<(string, int, long, GameDate)>();

        public List<ContractEvent> Events { get; } = new List<ContractEvent>();

        public SortedDictionary<int, (long Produced, long Transported)> PrimaryFlow { get; } =
            new SortedDictionary<int, (long, long)>();

        public Dictionary<(int Year, string Cargo), (long Produced, long Transported)> RawFlow { get; } =
            new Dictionary<(int, string), (long, long)>();

        public int IndustryLevelCount { get; set; } = 3;

        public SortedDictionary<int, (long Max, int AtCap, int Months)> LoadCap { get; } = new SortedDictionary<int, (long, int, int)>();

        public List<(string Finished, string Input)> FinishedChains { get; } = new List<(string, string)>();

        public List<string> Routes { get; } = new List<string>();

        public GameDate? BankruptOn { get; set; }

        public int Taken { get; set; }

        public int Fulfilled { get; set; }

        public int Failed { get; set; }

        public int Declined { get; set; }

        public int Cancelled { get; set; }
    }

    /// <summary>
    /// One game year of a <see cref="ContractPlayer"/> game, recorded on 31 December.
    /// </summary>
    internal sealed class ContractYearRow
    {
        public int Year { get; set; }

        public long Cash { get; set; }

        public long Income { get; set; }

        public long CashChange { get; set; }

        public int RoadRoutes { get; set; }

        public int RailRoutes { get; set; }

        public int TopUpSets { get; set; }

        public int RoadBuilt { get; set; }

        public int RailBuilt { get; set; }

        public int PossibleRoutes { get; set; }

        public int Taken { get; set; }

        public int Fulfilled { get; set; }

        public int Failed { get; set; }

        public int Declined { get; set; }

        public int Cancelled { get; set; }

        public int TotalTaken { get; set; }

        public int TotalFulfilled { get; set; }

        public int OpenContracts { get; set; }

        public int WaitingProposals { get; set; }

        public SortedDictionary<int, int> TownLevels { get; } = new SortedDictionary<int, int>();

        public SortedDictionary<int, int> IndustryLevels { get; } = new SortedDictionary<int, int>();

        public List<string> Unlocks { get; } = new List<string>();

        public List<int> CashWaits { get; } = new List<int>();

        public int OpenCashWait { get; set; }

        public long DeliveryIncome { get; set; }

        public long ContractBonus { get; set; }

        public SortedDictionary<int, long> DeliveredToTowns { get; } = new SortedDictionary<int, long>();

        public long DeliveredToIndustries { get; set; }

        public SortedDictionary<string, long> DeliveredToIndustriesByCargo { get; } = new SortedDictionary<string, long>(System.StringComparer.Ordinal);

        public SortedDictionary<int, long> IncomeByDecadeBuilt { get; } = new SortedDictionary<int, long>();

        public SortedDictionary<int, int> RoutesByDecadeBuilt { get; } = new SortedDictionary<int, int>();

        public SortedDictionary<string, (long Stock, long Capacity)> RawStock { get; } =
            new SortedDictionary<string, (long, long)>(System.StringComparer.Ordinal);

        public SortedDictionary<string, int> RawRoutes { get; } = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        public int RawNeedsFull { get; set; }

        public int RawNeedsPartly { get; set; }

        public int RawNeedsUnmet { get; set; }
    }

    /// <summary>
    /// A contract decision or outcome: accepted, served, declined, cancelled or failed, with its reason.
    /// </summary>
    internal sealed class ContractEvent
    {
        public ContractEvent(GameDate date, string contract, string kind, string detail)
        {
            Date = date;
            Contract = contract;
            Kind = kind;
            Detail = detail;
        }

        public GameDate Date { get; }

        public string Contract { get; }

        public string Kind { get; }

        public string Detail { get; }

        public string Cargo { get; set; } = string.Empty;

        public int Level { get; set; }

        public int Amount { get; set; }

        public long Delivered { get; set; }
    }
}
