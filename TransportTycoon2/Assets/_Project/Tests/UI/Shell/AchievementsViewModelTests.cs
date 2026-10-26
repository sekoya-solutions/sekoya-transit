#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    [TestFixture]
    public sealed class AchievementsViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildRows_OpenAchievement_ShowsProgressOverGoal()
        {
            ShellTextCache text = CreateText();
            var items = new[] { new AchievementListItem("achievement.cash", 250_000L, 1_000_000L, false, null) };

            var rows = AchievementsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Text, Is.EqualTo("Hold cash · 250,000 / 1,000,000"));
            Assert.That(rows[0].IsCompleted, Is.False);
        }

        [Test]
        public void BuildRows_CompletedAchievement_ShowsCappedProgressAndDate()
        {
            ShellTextCache text = CreateText();
            var items = new[] { new AchievementListItem("achievement.cash", 5_000_000L, 1_000_000L, true, "1905-03-04") };

            var rows = AchievementsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows[0].Text, Is.EqualTo("Hold cash · 1,000,000 / 1,000,000 · Completed 1905-03-04"));
            Assert.That(rows[0].IsCompleted, Is.True);
        }

        [Test]
        public void BuildRows_UnresolvedName_FallsBackToTheKey()
        {
            ShellTextCache text = CreateText();
            var items = new[] { new AchievementListItem("achievement.unknown", 1L, 10L, false, null) };

            var rows = AchievementsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows[0].Text, Does.StartWith("achievement.unknown"));
        }

        [Test]
        public void BuildRows_KeepsInputOrderAndEmptyListGivesNoRows()
        {
            ShellTextCache text = CreateText();
            var items = new[]
            {
                new AchievementListItem("achievement.b", 1L, 2L, false, null),
                new AchievementListItem("achievement.a", 1L, 2L, false, null)
            };

            var rows = AchievementsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows[0].Text, Does.StartWith("achievement.b"));
            Assert.That(rows[1].Text, Does.StartWith("achievement.a"));
            Assert.That(AchievementsViewModel.BuildRows(Array.Empty<AchievementListItem>(), text, _culture), Is.Empty);
        }

        [Test]
        public void ListItem_IncompleteIgnoresCompletionDate()
        {
            var item = new AchievementListItem("achievement.x", 1L, 2L, false, "1900-01-01");

            Assert.That(item.CompletionDate, Is.Null);
        }

        [Test]
        public void ListItem_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => new AchievementListItem(" ", 0L, 1L, false, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementListItem("a", -1L, 1L, false, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementListItem("a", 0L, 0L, false, null));
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.AchievementsRow, "{0} · {1} / {2}");
            text.Set(GoalsUiKeys.AchievementsRowCompleted, "{0} · {1} / {2} · Completed {3}");
            text.Set("achievement.cash", "Hold cash");
            return text;
        }
    }
}
