#nullable enable

namespace TransportTycoon.UI.Shell
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

        public const string AchievementsRow = "achievements.row";

        public const string AchievementsRowCompleted = "achievements.row.completed";

        public const string ContractsToggle = "contracts.toggle";

        public const string ContractsTitle = "contracts.title";

        public const string ContractsEmpty = "contracts.empty";

        public const string ContractsRowTown = "contracts.row.town";

        public const string ContractsRowIndustry = "contracts.row.industry";

        public const string ToastAchievement = "toast.achievement";

        public const string ToastContractOfferedTown = "toast.contract.offered.town";

        public const string ToastContractOfferedIndustry = "toast.contract.offered.industry";

        public const string ToastContractCompletedTown = "toast.contract.completed.town";

        public const string ToastContractCompletedIndustry = "toast.contract.completed.industry";

        public const string ToastContractExpiredTown = "toast.contract.expired.town";

        public const string ToastContractExpiredIndustry = "toast.contract.expired.industry";

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
            AchievementsRow,
            AchievementsRowCompleted,
            ContractsToggle,
            ContractsTitle,
            ContractsEmpty,
            ContractsRowTown,
            ContractsRowIndustry,
            ToastAchievement,
            ToastContractOfferedTown,
            ToastContractOfferedIndustry,
            ToastContractCompletedTown,
            ToastContractCompletedIndustry,
            ToastContractExpiredTown,
            ToastContractExpiredIndustry,
            GameOverScore,
            GameOverDate
        };
    }
}
