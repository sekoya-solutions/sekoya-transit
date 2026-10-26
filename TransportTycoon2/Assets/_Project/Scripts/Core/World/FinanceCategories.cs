#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Helpers for <see cref="FinanceCategory"/>.
    /// </summary>
    public static class FinanceCategories
    {
        /// <summary>
        /// Number of finance categories.
        /// </summary>
        public const int Count = 6;

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
