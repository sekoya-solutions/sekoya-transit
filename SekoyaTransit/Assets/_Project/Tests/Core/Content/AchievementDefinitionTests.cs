#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Content
{
    [TestFixture]
    public sealed class AchievementDefinitionTests
    {
        [TestCase(0L)]
        [TestCase(-5L)]
        public void MilestoneConstructor_NonPositiveGoal_Throws(long goal)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementMilestone(goal, "k"));
        }

        [Test]
        public void MilestoneConstructor_BlankNameKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AchievementMilestone(1L, " "));
        }

        [Test]
        public void Constructor_UnknownKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AchievementDefinition("a", (AchievementKind)99, Milestones(1L, 2L, 3L), "label"));
        }

        [Test]
        public void Constructor_BlankId_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(" ", AchievementKind.LifetimeIncome, Milestones(1L, 2L, 3L), "label"));
        }

        [Test]
        public void Constructor_WrongMilestoneCount_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(
                    "a",
                    AchievementKind.LifetimeIncome,
                    new[] { new AchievementMilestone(1L, "b"), new AchievementMilestone(2L, "s") },
                    "label"));
        }

        [TestCase(5L, 5L, 9L)]
        [TestCase(5L, 9L, 9L)]
        [TestCase(9L, 5L, 10L)]
        public void Constructor_GoalsNotStrictlyIncreasing_Throws(long bronze, long silver, long gold)
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(
                    "a",
                    AchievementKind.LifetimeIncome,
                    Milestones(bronze, silver, gold),
                    "label"));
        }

        [Test]
        public void Constructor_CargoSoldWithoutCargo_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition("a", AchievementKind.CargoSold, Milestones(1L, 2L, 3L)));
        }

        [Test]
        public void Constructor_CargoSoldWithLabel_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(
                    "a",
                    AchievementKind.CargoSold,
                    Milestones(1L, 2L, 3L),
                    "label",
                    "wood"));
        }

        [Test]
        public void Constructor_OtherKindWithCargo_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(
                    "a",
                    AchievementKind.LifetimeIncome,
                    Milestones(1L, 2L, 3L),
                    "label",
                    "wood"));
        }

        [Test]
        public void Constructor_OtherKindWithoutLabel_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition("a", AchievementKind.TrainCount, Milestones(1L, 2L, 3L)));
        }

        [Test]
        public void Constructor_GlobalGoalAboveOneHundredPercent_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(
                    "a",
                    AchievementKind.AchievementsUnlocked,
                    Milestones(20L, 50L, 101L),
                    "label"));
        }

        [Test]
        public void Constructor_GlobalScalingWithMapSize_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition(
                    "a",
                    AchievementKind.AchievementsUnlocked,
                    Milestones(20L, 50L, 100L),
                    "label",
                    scalesWithMapSize: true));
        }

        [Test]
        public void Constructor_ValidCargoTrack_ExposesValues()
        {
            var definition = new AchievementDefinition(
                "cargo_wood",
                AchievementKind.CargoSold,
                Milestones(10L, 20L, 30L),
                cargoId: "wood",
                scalesWithMapSize: true);

            Assert.That(definition.Id, Is.EqualTo("cargo_wood"));
            Assert.That(definition.Kind, Is.EqualTo(AchievementKind.CargoSold));
            Assert.That(definition.CargoId, Is.EqualTo("wood"));
            Assert.That(definition.LabelKey, Is.Null);
            Assert.That(definition.ScalesWithMapSize, Is.True);
            Assert.That(definition.Milestones, Has.Count.EqualTo(AchievementDefinition.MilestoneCount));
            Assert.That(definition.Milestones[2].Goal, Is.EqualTo(30L));
            Assert.That(definition.Milestones[2].NameKey, Is.EqualTo("n2"));
        }

        [Test]
        public void GetMilestoneId_JoinsTrackIdAndTier()
        {
            var definition = new AchievementDefinition(
                "lifetime_income",
                AchievementKind.LifetimeIncome,
                Milestones(1L, 2L, 3L),
                "label");

            Assert.That(definition.GetMilestoneId(AchievementTier.Bronze), Is.EqualTo("lifetime_income.bronze"));
            Assert.That(definition.GetMilestoneId(AchievementTier.Silver), Is.EqualTo("lifetime_income.silver"));
            Assert.That(definition.GetMilestoneId(AchievementTier.Gold), Is.EqualTo("lifetime_income.gold"));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.GetMilestoneId((AchievementTier)3));
        }

        [Test]
        public void GoalScale_AppliesPerMapSizeRoundedDownAndAtLeastOne()
        {
            var scale = new AchievementGoalScale(50, 100, 250);

            Assert.That(scale.Apply(1001L, MapSize.Small), Is.EqualTo(500L));
            Assert.That(scale.Apply(1001L, MapSize.Medium), Is.EqualTo(1001L));
            Assert.That(scale.Apply(1001L, MapSize.Large), Is.EqualTo(2502L));
            Assert.That(scale.Apply(1L, MapSize.Small), Is.EqualTo(1L));
        }

        [TestCase(0, 100, 100)]
        [TestCase(100, -1, 100)]
        [TestCase(100, 100, 0)]
        public void GoalScale_NonPositivePercent_Throws(int small, int medium, int large)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementGoalScale(small, medium, large));
        }

        [Test]
        public void Catalog_SecondGlobalTrack_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new ContentCatalog(
                    Array.Empty<CargoDefinition>(),
                    Array.Empty<IndustryDefinition>(),
                    achievements: new[]
                    {
                        new AchievementDefinition("g1", AchievementKind.AchievementsUnlocked, Milestones(1L, 2L, 3L), "l"),
                        new AchievementDefinition("g2", AchievementKind.AchievementsUnlocked, Milestones(1L, 2L, 3L), "l")
                    }));
        }

        [Test]
        public void Catalog_UnknownCargo_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new ContentCatalog(
                    Array.Empty<CargoDefinition>(),
                    Array.Empty<IndustryDefinition>(),
                    achievements: new[]
                    {
                        new AchievementDefinition("c", AchievementKind.CargoSold, Milestones(1L, 2L, 3L), cargoId: "wood")
                    }));
        }

        [Test]
        public void Catalog_NoGoalScale_IsNeutral()
        {
            var catalog = new ContentCatalog(Array.Empty<CargoDefinition>(), Array.Empty<IndustryDefinition>());

            Assert.That(catalog.AchievementGoalScale.GetPercent(MapSize.Large), Is.EqualTo(100));
        }

        private static AchievementMilestone[] Milestones(long bronze, long silver, long gold)
        {
            return new[]
            {
                new AchievementMilestone(bronze, "n0"),
                new AchievementMilestone(silver, "n1"),
                new AchievementMilestone(gold, "n2")
            };
        }
    }
}
