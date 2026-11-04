#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Localization keys (table <see cref="GameUiKeys.Table"/>) for the finance, achievements, contracts, toast,
    /// and game-over summary UI (Issue #66).
    /// </summary>
    public static class GoalsUiKeys
    {
        public const string FinanceToggle = "finance.toggle";

        public const string FinanceTitle = "finance.title";

        public const string FinanceColumnLastMonth = "finance.column.last_month";

        public const string FinanceColumnCurrentMonth = "finance.column.current_month";

        public const string FinanceColumnLastYear = "finance.column.last_year";

        public const string FinanceColumnCurrentYear = "finance.column.current_year";

        public const string FinanceNotAvailable = "finance.not_available";

        public const string FinanceTruckCost = "finance.line.truck_cost";

        public const string FinanceTruckBenefit = "finance.line.truck_benefit";

        public const string FinanceTruckProfit = "finance.line.truck_profit";

        public const string FinanceTrainCost = "finance.line.train_cost";

        public const string FinanceTrainBenefit = "finance.line.train_benefit";

        public const string FinanceTrainProfit = "finance.line.train_profit";

        public const string FinanceTotalCost = "finance.line.total_cost";

        public const string FinanceTotalBenefit = "finance.line.total_benefit";

        public const string FinanceTotalVehiclesProfit = "finance.line.total_vehicles_profit";

        public const string FinanceTruckBought = "finance.line.truck_bought";

        public const string FinanceTrainBought = "finance.line.train_bought";

        public const string FinanceVehiclesSold = "finance.line.vehicles_sold";

        public const string FinanceVehiclesUnlocked = "finance.line.vehicles_unlocked";

        public const string FinanceContractProfit = "finance.line.contract_profit";

        public const string FinanceTotalProfit = "finance.line.total_profit";
        public const string AchievementsToggle = "achievements.toggle";

        public const string AchievementsTitle = "achievements.title";

        public const string AchievementsEmpty = "achievements.empty";

        public const string AchievementsClose = "achievements.close";

        public const string AchievementsSectionGlobal = "achievements.section.global";

        public const string AchievementsSectionCompany = "achievements.section.company";

        public const string AchievementsSectionResources = "achievements.section.resources";

        public const string AchievementsTierBronze = "achievements.tier.bronze";

        public const string AchievementsTierSilver = "achievements.tier.silver";

        public const string AchievementsTierGold = "achievements.tier.gold";

        public const string ContractsToggle = "contracts.toggle";

        public const string ContractsTitle = "contracts.title";

        public const string ContractsAvailable = "contracts.header.available";

        public const string ContractsInProgress = "contracts.header.in_progress";

        public const string ContractsEmpty = "contracts.empty";

        public const string ContractsRowDeliver = "contracts.row.deliver";

        public const string ContractsRowReward = "contracts.row.reward";

        public const string ContractsRowAccepted = "contracts.row.accepted";

        public const string ContractsRowCompleted = "contracts.row.completed";

        public const string ContractsRowFailed = "contracts.row.failed";

        public const string ContractsAccept = "contracts.action.accept";

        public const string ContractsDecline = "contracts.action.decline";

        public const string ContractsCancel = "contracts.action.cancel";

        public const string ContractsClaim = "contracts.action.claim";

        public const string ContractsRemove = "contracts.action.remove";

        public const string ContractsFooterNext = "contracts.footer.next";

        public const string ContractsFooterFull = "contracts.footer.full";

        public const string ActiveContractsTitle = "contracts.active.title";

        public const string ActiveContractsDeliver = "contracts.active.deliver";

        public const string ActiveContractsProgress = "contracts.active.progress";

        public const string ActiveContractsTimeLeft = "contracts.active.time_left";

        public const string ActiveContractsTimeLeftOne = "contracts.active.time_left_one";

        public const string ActiveContractsCompleted = "contracts.active.completed";

        public const string ToastAchievement = "toast.achievement";

        public const string ToastContractOffered = "toast.contract.offered";

        public const string ToastContractCompleted = "toast.contract.completed";

        public const string ToastContractFailed = "toast.contract.failed";

        public const string GameOverScore = "game_over.score";

        public const string GameOverDate = "game_over.date";

        /// <summary>
        /// Keys loaded when the shell starts (achievement and industry names are added from the lists).
        /// </summary>
        public static readonly string[] StaticKeys =
        {
            GameUiKeys.CashTooltip,
            GameUiKeys.PopulationTooltip,
            RoutesUiKeys.Close,
            FinanceToggle,
            FinanceTitle,
            FinanceColumnLastMonth,
            FinanceColumnCurrentMonth,
            FinanceColumnLastYear,
            FinanceColumnCurrentYear,
            FinanceNotAvailable,
            FinanceTruckCost,
            FinanceTruckBenefit,
            FinanceTruckProfit,
            FinanceTrainCost,
            FinanceTrainBenefit,
            FinanceTrainProfit,
            FinanceTotalCost,
            FinanceTotalBenefit,
            FinanceTotalVehiclesProfit,
            FinanceTruckBought,
            FinanceTrainBought,
            FinanceVehiclesSold,
            FinanceVehiclesUnlocked,
            FinanceContractProfit,
            FinanceTotalProfit,
            AchievementsToggle,
            AchievementsTitle,
            AchievementsEmpty,
            AchievementsClose,
            AchievementsSectionGlobal,
            AchievementsSectionCompany,
            AchievementsSectionResources,
            AchievementsTierBronze,
            AchievementsTierSilver,
            AchievementsTierGold,
            ContractsToggle,
            ContractsTitle,
            ContractsAvailable,
            ContractsInProgress,
            ContractsEmpty,
            ContractsRowDeliver,
            ContractsRowReward,
            ContractsRowAccepted,
            ContractsRowCompleted,
            ContractsRowFailed,
            ContractsAccept,
            ContractsDecline,
            ContractsCancel,
            ContractsClaim,
            ContractsRemove,
            ContractsFooterNext,
            ContractsFooterFull,
            ActiveContractsTitle,
            ActiveContractsDeliver,
            ActiveContractsProgress,
            ActiveContractsTimeLeft,
            ActiveContractsTimeLeftOne,
            ActiveContractsCompleted,
            ToastAchievement,
            ToastContractOffered,
            ToastContractCompleted,
            ToastContractFailed,
            GameOverScore,
            GameOverDate
        };
    }
}
