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

        public const string FinanceHeadingCurrent = "finance.heading.current";

        public const string FinanceHeadingPrevious = "finance.heading.previous";

        public const string FinanceTotalIncome = "finance.total_income";

        public const string FinanceTotalSpending = "finance.total_spending";

        public const string FinanceNetResult = "finance.net_result";

        public const string FinanceDeliveryIncome = "finance.category.delivery_income";

        public const string FinanceContractBonus = "finance.category.contract_bonus";

        public const string FinanceVehicleSales = "finance.category.vehicle_sales";

        public const string FinanceRunningCosts = "finance.category.running_costs";

        public const string FinanceVehiclePurchases = "finance.category.vehicle_purchases";

        public const string FinanceVehicleUnlocks = "finance.category.vehicle_unlocks";

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
            FinanceToggle,
            FinanceTitle,
            FinanceHeadingCurrent,
            FinanceHeadingPrevious,
            FinanceTotalIncome,
            FinanceTotalSpending,
            FinanceNetResult,
            FinanceDeliveryIncome,
            FinanceContractBonus,
            FinanceVehicleSales,
            FinanceRunningCosts,
            FinanceVehiclePurchases,
            FinanceVehicleUnlocks,
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
            ToastAchievement,
            ToastContractOffered,
            ToastContractCompleted,
            ToastContractFailed,
            GameOverScore,
            GameOverDate
        };
    }
}
