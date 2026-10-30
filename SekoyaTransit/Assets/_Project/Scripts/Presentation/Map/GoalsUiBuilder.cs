#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Map
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
        /// Builds one item per catalog achievement track (Issue #223) with on-demand progress, the goals for this map,
        /// and each milestone's completion date from Core.
        /// </summary>
        public static IReadOnlyList<AchievementTrackItem> BuildAchievementItems(
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

            var completionDates = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int completion = 0; completion < world.AchievementCompletions.Count; completion++)
            {
                AchievementCompletion record = world.AchievementCompletions[completion];
                completionDates[record.AchievementId] = record.Date.ToString();
            }

            var items = new AchievementTrackItem[catalog.Achievements.Count];
            for (int index = 0; index < items.Length; index++)
            {
                AchievementDefinition definition = catalog.Achievements[index];
                var milestones = new AchievementMilestoneItem[AchievementDefinition.MilestoneCount];
                for (int tier = 0; tier < milestones.Length; tier++)
                {
                    var achievementTier = (AchievementTier)tier;
                    bool completed = completionDates.TryGetValue(
                        definition.GetMilestoneId(achievementTier),
                        out string? completionDate);
                    milestones[tier] = new AchievementMilestoneItem(
                        Achievements.GetGoal(world, catalog, definition, achievementTier),
                        definition.Milestones[tier].NameKey,
                        completed,
                        completionDate);
                }

                items[index] = new AchievementTrackItem(
                    ResolveSection(definition.Kind),
                    definition.LabelKey,
                    definition.CargoId,
                    definition.CargoId != null ? CargoLabel(definition.CargoId) : null,
                    Achievements.GetProgress(world, catalog, definition),
                    milestones);
            }

            return items;
        }

        /// <summary>
        /// Builds the contracts panel data (Issue #227): one item per contract, the accepted count, and the limits of
        /// the map size; the reward uses the same Core formula as the payout.
        /// </summary>
        public static ContractsPanelData BuildContractsPanel(GeneratedWorld world, ContentCatalog catalog)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            ContractLimits limits = catalog.Contracts.GetLimits(world.Map.Size);
            bool canAccept = Contracts.HasAcceptSlot(world, catalog);
            var items = new List<ContractListItem>(world.ContractList.Count);
            for (int index = 0; index < world.ContractList.Count; index++)
            {
                Contract contract = world.ContractList[index];
                if (contract.TownIndex >= world.Towns.Count)
                {
                    continue;
                }

                Town town = world.Towns[contract.TownIndex];
                long reward = 0L;
                if (catalog.TryGetCargo(contract.CargoId, out CargoDefinition? cargo) && cargo != null)
                {
                    reward = Contracts.ComputeReward(catalog.Contracts, cargo, contract.Amount);
                }

                items.Add(
                    new ContractListItem(
                        contract.Id,
                        contract.CargoId,
                        town.Name,
                        town.Coordinate.X,
                        town.Coordinate.Y,
                        ResolveStatus(contract.Status),
                        contract.Delivered,
                        contract.Amount,
                        catalog.Contracts.DurationMonths,
                        ResolveDateText(contract),
                        reward,
                        canAccept));
            }

            return new ContractsPanelData(
                items,
                world.AcceptedContractCount,
                limits.MaxAccepted,
                limits.MaxContracts);
        }

        /// <summary>
        /// Builds the toast for a completed achievement milestone: its trophy icon, tier, and title (for example "Bronze achievement:
        /// Log Haul").
        /// </summary>
        public static ToastMessage BuildAchievementToast(AchievementCompletedEvent completed)
        {
            return new ToastMessage(
                GoalsUiKeys.ToastAchievement,
                new[] { ToastArgument.FromKey(TierKey(completed.Tier)), ToastArgument.FromKey(completed.NameKey) },
                TierIcon(completed.Tier));
        }

        /// <summary>
        /// Builds the toast for a new contract proposal, or null when its town no longer exists.
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
                offered.TownIndex,
                GoalsUiKeys.ToastContractOffered);
        }

        /// <summary>
        /// Builds the toast for a completed contract, or null when its town no longer exists.
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
                completed.TownIndex,
                GoalsUiKeys.ToastContractCompleted);
        }

        /// <summary>
        /// Builds the toast for a failed contract, or null when its town no longer exists.
        /// </summary>
        public static ToastMessage? BuildContractFailedToast(
            GeneratedWorld world,
            ContentCatalog catalog,
            ContractFailedEvent failed)
        {
            return BuildContractToast(
                world,
                catalog,
                failed.CargoId,
                failed.Amount,
                failed.TownIndex,
                GoalsUiKeys.ToastContractFailed);
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
            int townIndex,
            string templateKey)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (townIndex < 0 || townIndex >= world.Towns.Count)
            {
                return null;
            }

            return new ToastMessage(
                templateKey,
                new[]
                {
                    ToastArgument.FromText(CargoDisplayName(cargoId)),
                    ToastArgument.FromAmount(amount),
                    ToastArgument.FromText(world.Towns[townIndex].Name)
                });
        }

        private static ContractUiStatus ResolveStatus(ContractStatus status)
        {
            switch (status)
            {
                case ContractStatus.Proposal:
                    return ContractUiStatus.Proposal;
                case ContractStatus.Accepted:
                    return ContractUiStatus.Accepted;
                case ContractStatus.Completed:
                    return ContractUiStatus.Completed;
                default:
                    return ContractUiStatus.Failed;
            }
        }

        // The deadline of a running contract, the completion or failure date of a settled one, nothing for a proposal.
        private static string ResolveDateText(Contract contract)
        {
            switch (contract.Status)
            {
                case ContractStatus.Accepted:
                    return contract.Deadline.ToString();
                case ContractStatus.Completed:
                case ContractStatus.Failed:
                    return contract.SettledDate.ToString();
                default:
                    return string.Empty;
            }
        }

        private static string CargoDisplayName(string cargoId)
        {
            return cargoId.Replace('_', ' ');
        }

        // A cargo track label: the cargo display name with an upper-case first letter ("Canned food").
        private static string CargoLabel(string cargoId)
        {
            string name = CargoDisplayName(cargoId);
            return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static AchievementSection ResolveSection(AchievementKind kind)
        {
            switch (kind)
            {
                case AchievementKind.AchievementsUnlocked:
                    return AchievementSection.Global;
                case AchievementKind.CargoSold:
                    return AchievementSection.Resources;
                default:
                    return AchievementSection.Company;
            }
        }

        private static ToastIcon TierIcon(AchievementTier tier)
        {
            switch (tier)
            {
                case AchievementTier.Bronze:
                    return ToastIcon.TrophyBronze;
                case AchievementTier.Silver:
                    return ToastIcon.TrophySilver;
                case AchievementTier.Gold:
                    return ToastIcon.TrophyGold;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown achievement tier.");
            }
        }

        private static string TierKey(AchievementTier tier)
        {
            switch (tier)
            {
                case AchievementTier.Bronze:
                    return GoalsUiKeys.AchievementsTierBronze;
                case AchievementTier.Silver:
                    return GoalsUiKeys.AchievementsTierSilver;
                case AchievementTier.Gold:
                    return GoalsUiKeys.AchievementsTierGold;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown achievement tier.");
            }
        }
    }
}
