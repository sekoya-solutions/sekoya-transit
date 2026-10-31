#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class AchievementsViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void MilestonePosition_SpacesTheTrophiesEvenly()
        {
            Assert.That(AchievementsViewModel.MilestonePosition(0), Is.EqualTo(1f / 6f).Within(1e-6f));
            Assert.That(AchievementsViewModel.MilestonePosition(1), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(AchievementsViewModel.MilestonePosition(2), Is.EqualTo(5f / 6f).Within(1e-6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AchievementsViewModel.MilestonePosition(3));
        }

        [TestCase(0L, 0f)]
        [TestCase(500L, 1f / 12f)]
        [TestCase(1000L, 1f / 6f)]
        [TestCase(5500L, 1f / 3f)]
        [TestCase(10000L, 0.5f)]
        [TestCase(55000L, 2f / 3f)]
        [TestCase(99999L, 5f / 6f)]
        [TestCase(100000L, 1f)]
        [TestCase(250000L, 1f)]
        public void Fill_IsLinearBetweenEvenlySpacedTrophies(long current, float expected)
        {
            float fill = AchievementsViewModel.Fill(current, Milestones(1000L, 10000L, 100000L, completed: 0));

            Assert.That(fill, Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void BuildRows_LabelledTrack_ShowsLabelValueAndMilestones()
        {
            ShellTextCache text = CreateText();
            var item = new AchievementTrackItem(
                AchievementSection.Company,
                "achievement.income",
                null,
                null,
                2_500_000L,
                Milestones(1_000_000L, 10_000_000L, 100_000_000L, completed: 1));

            var rows = AchievementsViewModel.BuildRows(new[] { item }, text, _culture);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Section, Is.EqualTo(AchievementSection.Company));
            Assert.That(rows[0].Label.ToPlainText(), Is.EqualTo("Money earned"));
            Assert.That(rows[0].ValueText, Is.EqualTo("2,500,000"));
            Assert.That(rows[0].Milestones[0].Title, Is.EqualTo("Profit Surge"));
            Assert.That(rows[0].Milestones[0].GoalText, Is.EqualTo("1,000,000"));
            Assert.That(rows[0].Milestones[0].IsUnlocked, Is.True);
            Assert.That(rows[0].Milestones[0].DateText, Is.EqualTo("1901-02-03"));
            Assert.That(rows[0].Milestones[1].IsUnlocked, Is.False);
            Assert.That(rows[0].Milestones[1].DateText, Is.Null);
            Assert.That(rows[0].Fill, Is.GreaterThan(1f / 6f).And.LessThan(0.5f));
        }

        [Test]
        public void BuildRows_CargoTrack_ShowsTheCargoIconAndName()
        {
            ShellTextCache text = CreateText();
            var item = new AchievementTrackItem(
                AchievementSection.Resources,
                null,
                "wood",
                "Wood",
                10L,
                Milestones(1000L, 10000L, 100000L, completed: 0));

            var rows = AchievementsViewModel.BuildRows(new[] { item }, text, _culture);

            Assert.That(rows[0].Label.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("wood")));
            Assert.That(rows[0].Label.ToPlainText(), Is.Empty);
        }

        [Test]
        public void BuildRows_UnresolvedTitle_FallsBackToTheKey()
        {
            ShellTextCache text = CreateText();
            var item = new AchievementTrackItem(
                AchievementSection.Company,
                "achievement.unknown",
                null,
                null,
                1L,
                Milestones(1L, 2L, 3L, completed: 0));

            var rows = AchievementsViewModel.BuildRows(new[] { item }, text, _culture);

            Assert.That(rows[0].Label.ToPlainText(), Is.EqualTo("achievement.unknown"));
        }

        [Test]
        public void BuildRows_EmptyListGivesNoRows()
        {
            Assert.That(
                AchievementsViewModel.BuildRows(Array.Empty<AchievementTrackItem>(), CreateText(), _culture),
                Is.Empty);
        }

        [Test]
        public void SectionKey_NamesEverySection()
        {
            Assert.That(
                AchievementsViewModel.SectionKey(AchievementSection.Global),
                Is.EqualTo(GoalsUiKeys.AchievementsSectionGlobal));
            Assert.That(
                AchievementsViewModel.SectionKey(AchievementSection.Company),
                Is.EqualTo(GoalsUiKeys.AchievementsSectionCompany));
            Assert.That(
                AchievementsViewModel.SectionKey(AchievementSection.Resources),
                Is.EqualTo(GoalsUiKeys.AchievementsSectionResources));
        }

        [Test]
        public void TrackRows_WithTheSameContent_AreEqual()
        {
            ShellTextCache text = CreateText();
            var item = new AchievementTrackItem(
                AchievementSection.Company,
                "achievement.income",
                null,
                null,
                5L,
                Milestones(1L, 10L, 100L, completed: 1));

            var first = AchievementsViewModel.BuildRows(new[] { item }, text, _culture);
            var second = AchievementsViewModel.BuildRows(new[] { item }, text, _culture);

            Assert.That(first[0], Is.EqualTo(second[0]));
        }

        [Test]
        public void MilestoneItem_IncompleteIgnoresCompletionDate()
        {
            var item = new AchievementMilestoneItem(5L, "k", false, "1900-01-01");

            Assert.That(item.CompletionDate, Is.Null);
        }

        [Test]
        public void TrackItem_InvalidValues_Throw()
        {
            AchievementMilestoneItem[] milestones = Milestones(1L, 2L, 3L, completed: 0);

            Assert.Throws<ArgumentException>(
                () => new AchievementTrackItem(AchievementSection.Company, null, null, null, 0L, milestones));
            Assert.Throws<ArgumentException>(
                () => new AchievementTrackItem(AchievementSection.Company, "k", "wood", "Wood", 0L, milestones));
            Assert.Throws<ArgumentException>(
                () => new AchievementTrackItem(AchievementSection.Resources, null, "wood", null, 0L, milestones));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AchievementTrackItem(AchievementSection.Company, "k", null, null, -1L, milestones));
            Assert.Throws<ArgumentException>(
                () => new AchievementTrackItem(
                    AchievementSection.Company,
                    "k",
                    null,
                    null,
                    0L,
                    new[] { milestones[0], milestones[1] }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementMilestoneItem(0L, "k", false, null));
            Assert.Throws<ArgumentException>(() => new AchievementMilestoneItem(1L, " ", false, null));
        }

        private static AchievementMilestoneItem[] Milestones(long bronze, long silver, long gold, int completed)
        {
            return new[]
            {
                new AchievementMilestoneItem(bronze, "achievement.income.bronze", completed > 0, "1901-02-03"),
                new AchievementMilestoneItem(silver, "achievement.income.silver", completed > 1, "1902-02-03"),
                new AchievementMilestoneItem(gold, "achievement.income.gold", completed > 2, "1903-02-03")
            };
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set("achievement.income", "Money earned");
            text.Set("achievement.income.bronze", "Profit Surge");
            text.Set("achievement.income.silver", "Money Machine");
            text.Set("achievement.income.gold", "Cash Empire");
            return text;
        }
    }
}
