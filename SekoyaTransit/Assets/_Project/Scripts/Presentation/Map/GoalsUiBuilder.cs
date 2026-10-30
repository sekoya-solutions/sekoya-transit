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
                        contract.CargoId,
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
