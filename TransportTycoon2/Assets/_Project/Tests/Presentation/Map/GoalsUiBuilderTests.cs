#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class GoalsUiBuilderTests
    {
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly string[] _planksCargoIds = { "planks" };
        private static readonly GameDate _january = new GameDate(1900, 1, 1);
        private static readonly GameDate _deadline = new GameDate(1901, 1, 1);

        [Test]
        public void BuildFinanceRecap_ListsEveryCategoryForBothYearsIncomeFirst()
        {
            GeneratedWorld world = CreateWorld(out _);
            world.Finance.AdvanceTo(1900);
            world.CreditCompanyCash(700L, FinanceCategory.DeliveryIncome);
            world.TryDebitCompanyCash(200L, FinanceCategory.VehiclePurchases);
            world.Finance.AdvanceTo(1901);
            world.CreditCompanyCash(30L, FinanceCategory.ContractBonus);

            FinanceRecap recap = GoalsUiBuilder.BuildFinanceRecap(world);

            Assert.That(recap.Current.Year, Is.EqualTo(1901));
            Assert.That(recap.Previous.Year, Is.EqualTo(1900));
            Assert.That(recap.Current.Lines.Count, Is.EqualTo(FinanceCategories.Count));
            Assert.That(recap.Current.TotalIncome, Is.EqualTo(30L));
            Assert.That(recap.Previous.TotalIncome, Is.EqualTo(700L));
            Assert.That(recap.Previous.TotalSpending, Is.EqualTo(200L));
            Assert.That(recap.Previous.Net, Is.EqualTo(500L));
            Assert.That(recap.Current.Lines[0].LabelKey, Is.EqualTo(GoalsUiKeys.FinanceDeliveryIncome));
            Assert.That(recap.Current.Lines[0].IsIncome, Is.True);
            Assert.That(recap.Current.Lines[5].LabelKey, Is.EqualTo(GoalsUiKeys.FinanceVehicleUnlocks));
            Assert.That(recap.Current.Lines[5].IsIncome, Is.False);
        }

        [Test]
        public void BuildFinanceRecap_BeforeTheFirstTick_HasUnsetYears()
        {
            GeneratedWorld world = CreateWorld(out _);

            FinanceRecap recap = GoalsUiBuilder.BuildFinanceRecap(world);

            Assert.That(recap.Current.Year, Is.Zero);
            Assert.That(recap.Previous.Year, Is.Zero);
            Assert.That(recap.Current.Net, Is.Zero);
        }

        [Test]
        public void BuildAchievementItems_ReportsProgressAndCompletionDate()
        {
            GeneratedWorld world = CreateWorld(out ContentCatalog catalog);
            Achievements.AdvanceAll(world, catalog, new GameDate(1902, 3, 4));

            var items = GoalsUiBuilder.BuildAchievementItems(world, catalog);

            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].NameKey, Is.EqualTo("achievement.rich"));
            Assert.That(items[0].IsCompleted, Is.True);
            Assert.That(items[0].CompletionDate, Is.EqualTo(new GameDate(1902, 3, 4).ToString()));
            Assert.That(items[0].Current, Is.EqualTo(world.CompanyCash));
            Assert.That(items[1].IsCompleted, Is.False);
            Assert.That(items[1].CompletionDate, Is.Null);
            Assert.That(items[1].Current, Is.Zero);
            Assert.That(items[1].Goal, Is.EqualTo(10L));
        }

        [Test]
        public void BuildAchievementItems_ReadsProgressWithoutChangingTheWorld()
        {
            GeneratedWorld world = CreateWorld(out ContentCatalog catalog);

            GoalsUiBuilder.BuildAchievementItems(world, catalog);

            Assert.That(world.AchievementCompletions, Is.Empty);
        }

        [Test]
        public void BuildContractItems_UsesTheCorePayoutFormulaForTheBonus()
        {
            GeneratedWorld world = CreateWorld(out ContentCatalog catalog);
            world.OfferContract("wood", DeliveryDestinationKind.Town, 0, 50, _january, _deadline);
            int sawmill = IndexOfIndustry(world, "sawmill");
            world.OfferContract("wood", DeliveryDestinationKind.Industry, sawmill, 20, _january, _deadline);
            catalog.TryGetCargo("wood", out CargoDefinition? wood);

            var items = GoalsUiBuilder.BuildContractItems(world, catalog);

            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].TargetKind, Is.EqualTo(ContractTargetKind.Town));
            Assert.That(items[0].TargetNumber, Is.EqualTo(world.Towns[0].Id));
            Assert.That(items[0].TileX, Is.EqualTo(world.Towns[0].Coordinate.X));
            Assert.That(items[0].TileY, Is.EqualTo(world.Towns[0].Coordinate.Y));
            Assert.That(items[0].Bonus, Is.EqualTo(Contracts.ComputeReward(catalog.Contracts, wood!, 50)));
            Assert.That(items[0].Required, Is.EqualTo(50));
            Assert.That(items[0].Deadline, Is.EqualTo(_deadline.ToString()));
            Assert.That(items[1].TargetKind, Is.EqualTo(ContractTargetKind.Industry));
            Assert.That(items[1].TargetNameKey, Is.EqualTo("industry.sawmill"));
            Assert.That(items[1].TileX, Is.EqualTo(world.Industries[sawmill].Coordinate.X));
        }

        [Test]
        public void BuildContractItems_NoOpenContracts_IsEmpty()
        {
            GeneratedWorld world = CreateWorld(out ContentCatalog catalog);

            Assert.That(GoalsUiBuilder.BuildContractItems(world, catalog), Is.Empty);
        }

        [Test]
        public void BuildAchievementToast_NamesTheAchievementByKey()
        {
            var toast = GoalsUiBuilder.BuildAchievementToast(
                new AchievementCompletedEvent("rich", "achievement.rich", _january));

            Assert.That(toast.TemplateKey, Is.EqualTo(GoalsUiKeys.ToastAchievement));
            Assert.That(toast.Arguments[0].Kind, Is.EqualTo(ToastArgumentKind.LocalizationKey));
            Assert.That(toast.Arguments[0].Text, Is.EqualTo("achievement.rich"));
        }

        [Test]
        public void BuildContractToasts_PickTheTownOrIndustryTemplate()
        {
            GeneratedWorld world = CreateWorld(out ContentCatalog catalog);
            int sawmill = IndexOfIndustry(world, "sawmill");

            ToastMessage? offeredTown = GoalsUiBuilder.BuildContractOfferedToast(
                world,
                catalog,
                new ContractOfferedEvent(1, "wood", DeliveryDestinationKind.Town, 0, 50, _deadline));
            ToastMessage? completedIndustry = GoalsUiBuilder.BuildContractCompletedToast(
                world,
                catalog,
                new ContractCompletedEvent(
                    2,
                    "wood",
                    DeliveryDestinationKind.Industry,
                    sawmill,
                    20,
                    160L,
                    0,
                    _january));
            ToastMessage? expiredTown = GoalsUiBuilder.BuildContractExpiredToast(
                world,
                catalog,
                new ContractExpiredEvent(3, "wood", DeliveryDestinationKind.Town, 0, 50, 10L, _deadline));

            Assert.That(offeredTown!.TemplateKey, Is.EqualTo(GoalsUiKeys.ToastContractOfferedTown));
            Assert.That(offeredTown.Arguments.Count, Is.EqualTo(4));
            Assert.That(completedIndustry!.TemplateKey, Is.EqualTo(GoalsUiKeys.ToastContractCompletedIndustry));
            Assert.That(completedIndustry.Arguments[2].Kind, Is.EqualTo(ToastArgumentKind.LocalizationKey));
            Assert.That(completedIndustry.Arguments[2].Text, Is.EqualTo("industry.sawmill"));
            Assert.That(completedIndustry.Arguments[4].Amount, Is.EqualTo(160L));
            Assert.That(expiredTown!.TemplateKey, Is.EqualTo(GoalsUiKeys.ToastContractExpiredTown));
        }

        [Test]
        public void BuildContractToast_TargetNoLongerExists_ReturnsNull()
        {
            GeneratedWorld world = CreateWorld(out ContentCatalog catalog);

            ToastMessage? toast = GoalsUiBuilder.BuildContractOfferedToast(
                world,
                catalog,
                new ContractOfferedEvent(1, "wood", DeliveryDestinationKind.Town, 999, 50, _deadline));

            Assert.That(toast, Is.Null);
        }

        private static int IndexOfIndustry(GeneratedWorld world, string industryId)
        {
            for (int index = 0; index < world.Industries.Count; index++)
            {
                if (world.Industries[index].IndustryId == industryId)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("Industry not found: " + industryId);
        }

        private static GeneratedWorld CreateWorld(out ContentCatalog catalog)
        {
            catalog = new ContentCatalog(
                new[]
                {
                    new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 3, 1),
                    new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 5, 2)
                },
                new[]
                {
                    new IndustryDefinition(
                        "forest",
                        "industry.forest",
                        1,
                        Array.Empty<CargoAmountDefinition>(),
                        new[] { new CargoAmountDefinition("wood", 4) }),
                    new IndustryDefinition(
                        "sawmill",
                        "industry.sawmill",
                        2,
                        new[] { new CargoAmountDefinition("wood", 2) },
                        new[] { new CargoAmountDefinition("planks", 1) })
                },
                new TownNeedsSettings(
                    100,
                    500,
                    10000,
                    new[] { new TownNeedTier(0, 1, 0, 0) },
                    demandPerNeedPerMonth: 10,
                    populationPerSatisfiedNeed: 20),
                achievements: new[]
                {
                    new AchievementDefinition("rich", "achievement.rich", AchievementKind.CompanyCash, 1L),
                    new AchievementDefinition("fleet", "achievement.fleet", AchievementKind.VehicleCount, 10L)
                },
                contracts: new ContractSettings(3, 50, 200, 12, 1.5m, 100));
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _woodCargoIds, 1, 0),
                new IndustryPlacementRule("sawmill", 2, _woodCargoIds, _planksCargoIds, 1, 80)
            };
            var settings = new WorldGenerationSettings(new TownPlacementSettings(MapSize.Small, 3, 16), 4, rules);
            return WorldGenerator.Generate(settings, 66001UL, catalog.TownNeeds, catalog);
        }
    }
}
