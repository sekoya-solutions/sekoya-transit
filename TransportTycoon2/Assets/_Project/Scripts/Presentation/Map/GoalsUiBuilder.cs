#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Builds the finance, achievements, and contracts UI data and the goal toasts from Core state (Issue #66).
    /// Read-only: nothing here mutates the simulation.
    /// </summary>
    public static class GoalsUiBuilder
    {
        /// <summary>
        /// Builds the finance recap: this year and last year, income categories first.
        /// </summary>
        public static FinanceRecap BuildFinanceRecap(GeneratedWorld world)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            FinanceLedger ledger = world.Finance;
            return new FinanceRecap(
                BuildFinanceYear(ledger.Year, ledger.Current),
                BuildFinanceYear(ledger.Year == 0 ? 0 : ledger.Year - 1, ledger.Previous));
        }

        /// <summary>
        /// Builds one row per catalog achievement with on-demand progress from Core.
        /// </summary>
        public static IReadOnlyList<AchievementListItem> BuildAchievementItems(
            GeneratedWorld world,
            ContentCatalog catalog)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var items = new AchievementListItem[catalog.Achievements.Count];
            for (int index = 0; index < items.Length; index++)
            {
                AchievementDefinition definition = catalog.Achievements[index];
                string? completionDate = null;
                bool completed = world.IsAchievementCompleted(definition.Id);
                if (completed)
                {
                    for (int completion = 0; completion < world.AchievementCompletions.Count; completion++)
                    {
                        if (string.Equals(
                                world.AchievementCompletions[completion].AchievementId,
                                definition.Id,
                                StringComparison.Ordinal))
                        {
                            completionDate = world.AchievementCompletions[completion].Date.ToString();
                            break;
                        }
                    }
                }

                items[index] = new AchievementListItem(
                    definition.NameKey,
                    Achievements.GetProgress(world, definition),
                    definition.Goal,
                    completed,
                    completionDate);
            }

            return items;
        }

        /// <summary>
        /// Builds one row per open contract; the bonus uses the same Core formula as the payout.
        /// </summary>
        public static IReadOnlyList<ContractListItem> BuildContractItems(
            GeneratedWorld world,
            ContentCatalog catalog)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var items = new List<ContractListItem>(world.OpenContracts.Count);
            for (int index = 0; index < world.OpenContracts.Count; index++)
            {
                Contract contract = world.OpenContracts[index];
                if (!TryResolveTarget(
                        world,
                        catalog,
                        contract.TargetKind,
                        contract.TargetIndex,
                        out ContractTargetKind kind,
                        out int number,
                        out string? nameKey,
                        out TileCoordinate coordinate))
                {
                    continue;
                }

                long bonus = 0L;
                if (catalog.TryGetCargo(contract.CargoId, out CargoDefinition? cargo) && cargo != null)
                {
                    bonus = Contracts.ComputeReward(catalog.Contracts, cargo, contract.Amount);
                }

                items.Add(
                    new ContractListItem(
                        contract.Id,
                        CargoDisplayName(contract.CargoId),
                        kind,
                        number,
                        nameKey,
                        coordinate.X,
                        coordinate.Y,
                        contract.Delivered,
                        contract.Amount,
                        contract.Deadline.ToString(),
                        bonus));
            }

            return items;
        }

        /// <summary>
        /// Builds the toast for a completed achievement.
        /// </summary>
        public static ToastMessage BuildAchievementToast(AchievementCompletedEvent completed)
        {
            return new ToastMessage(
                GoalsUiKeys.ToastAchievement,
                new[] { ToastArgument.FromKey(completed.NameKey) });
        }

        /// <summary>
        /// Builds the toast for an offered contract, or null when its target no longer exists.
        /// </summary>
        public static ToastMessage? BuildContractOfferedToast(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractOfferedEvent offered)
        {
            return BuildContractToast(
                world,
                catalog,
                offered.CargoId,
                offered.Amount,
                offered.TargetKind,
                offered.TargetIndex,
                GoalsUiKeys.ToastContractOfferedTown,
                GoalsUiKeys.ToastContractOfferedIndustry,
                ToastArgument.FromText(offered.Deadline.ToString()));
        }

        /// <summary>
        /// Builds the toast for a completed contract, or null when its target no longer exists.
        /// </summary>
        public static ToastMessage? BuildContractCompletedToast(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractCompletedEvent completed)
        {
            return BuildContractToast(
                world,
                catalog,
                completed.CargoId,
                completed.Amount,
                completed.TargetKind,
                completed.TargetIndex,
                GoalsUiKeys.ToastContractCompletedTown,
                GoalsUiKeys.ToastContractCompletedIndustry,
                ToastArgument.FromAmount(completed.Reward));
        }

        /// <summary>
        /// Builds the toast for an expired contract, or null when its target no longer exists.
        /// </summary>
        public static ToastMessage? BuildContractExpiredToast(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractExpiredEvent expired)
        {
            return BuildContractToast(
                world,
                catalog,
                expired.CargoId,
                expired.Amount,
                expired.TargetKind,
                expired.TargetIndex,
                GoalsUiKeys.ToastContractExpiredTown,
                GoalsUiKeys.ToastContractExpiredIndustry,
                null);
        }

        private static FinanceYearRecap BuildFinanceYear(int year, FinanceYearTotals totals)
        {
            var lines = new List<FinanceRecapLine>(FinanceCategories.Count);
            AppendLine(lines, totals, FinanceCategory.DeliveryIncome, GoalsUiKeys.FinanceDeliveryIncome);
            AppendLine(lines, totals, FinanceCategory.ContractBonus, GoalsUiKeys.FinanceContractBonus);
            AppendLine(lines, totals, FinanceCategory.VehicleSales, GoalsUiKeys.FinanceVehicleSales);
            AppendLine(lines, totals, FinanceCategory.RunningCosts, GoalsUiKeys.FinanceRunningCosts);
            AppendLine(lines, totals, FinanceCategory.VehiclePurchases, GoalsUiKeys.FinanceVehiclePurchases);
            AppendLine(lines, totals, FinanceCategory.VehicleUnlocks, GoalsUiKeys.FinanceVehicleUnlocks);
            return new FinanceYearRecap(year, lines);
        }

        private static void AppendLine(
            List<FinanceRecapLine> lines,
            FinanceYearTotals totals,
            FinanceCategory category,
            string labelKey)
        {
            lines.Add(new FinanceRecapLine(labelKey, totals.Get(category), FinanceCategories.IsIncome(category)));
        }

        private static ToastMessage? BuildContractToast(
            GeneratedWorld world,
            ContentCatalog catalog,
            string cargoId,
            int amount,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            string townKey,
            string industryKey,
            ToastArgument? extra)
        {
            if (!TryResolveTarget(
                    world,
                    catalog,
                    targetKind,
                    targetIndex,
                    out ContractTargetKind kind,
                    out int number,
                    out string? nameKey,
                    out _))
            {
                return null;
            }

            var arguments = new List<ToastArgument>
            {
                ToastArgument.FromText(CargoDisplayName(cargoId)),
                ToastArgument.FromAmount(amount)
            };
            if (kind == ContractTargetKind.Town)
            {
                arguments.Add(ToastArgument.FromText(number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            else
            {
                arguments.Add(ToastArgument.FromKey(nameKey!));
                arguments.Add(ToastArgument.FromText(number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            if (extra.HasValue)
            {
                arguments.Add(extra.Value);
            }

            return new ToastMessage(kind == ContractTargetKind.Town ? townKey : industryKey, arguments);
        }

        private static bool TryResolveTarget(
            GeneratedWorld world,
            ContentCatalog catalog,
            DeliveryDestinationKind targetKind,
            int targetIndex,
            out ContractTargetKind kind,
            out int number,
            out string? nameKey,
            out TileCoordinate coordinate)
        {
            kind = ContractTargetKind.Town;
            number = 0;
            nameKey = null;
            coordinate = default;
            if (targetKind == DeliveryDestinationKind.Town)
            {
                if (targetIndex < 0 || targetIndex >= world.Towns.Count)
                {
                    return false;
                }

                Town town = world.Towns[targetIndex];
                number = town.Id;
                coordinate = town.Coordinate;
                return true;
            }

            if (targetKind != DeliveryDestinationKind.Industry
                || targetIndex < 0
                || targetIndex >= world.Industries.Count)
            {
                return false;
            }

            PlacedIndustry industry = world.Industries[targetIndex];
            if (!catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) || definition == null)
            {
                return false;
            }

            kind = ContractTargetKind.Industry;
            number = industry.InstanceIndex;
            nameKey = definition.NameKey;
            coordinate = industry.Coordinate;
            return true;
        }

        private static string CargoDisplayName(string cargoId)
        {
            return cargoId.Replace('_', ' ');
        }
    }
}
