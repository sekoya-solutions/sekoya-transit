#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Helpers for <see cref="FinanceCategory"/>.
    /// </summary>
    public static class FinanceCategories
    {
        /// <summary>
        /// Number of finance categories.
        /// </summary>
        public const int Count = 7;

        /// <summary>
        /// Number of finance categories before format 33 added <see cref="FinanceCategory.Cheats"/> (Issue #316); older
        /// saves list this many amounts per period.
        /// </summary>
        public const int CountBeforeCheats = 6;

        /// <summary>
        /// Number of finance modes (Issue #296).
        /// </summary>
        public const int ModeCount = 3;

        /// <summary>
        /// Returns whether <paramref name="mode"/> is a defined mode.
        /// </summary>
        public static bool IsModeDefined(FinanceMode mode)
        {
            return (int)mode >= 0 && (int)mode < ModeCount;
        }

        /// <summary>
        /// Returns whether the category records income (otherwise spending).
        /// </summary>
        public static bool IsIncome(FinanceCategory category)
        {
            switch (category)
            {
                case FinanceCategory.DeliveryIncome:
                case FinanceCategory.ContractBonus:
                case FinanceCategory.VehicleSales:
                case FinanceCategory.Cheats:
                    return true;
                case FinanceCategory.RunningCosts:
                case FinanceCategory.VehiclePurchases:
                case FinanceCategory.VehicleUnlocks:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown finance category.");
            }
        }

        /// <summary>
        /// Returns whether <paramref name="category"/> is a defined category.
        /// </summary>
        public static bool IsDefined(FinanceCategory category)
        {
            return (int)category >= 0 && (int)category < Count;
        }
    }
}
