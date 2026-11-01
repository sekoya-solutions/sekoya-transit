#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class ActiveContractsViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildRows_Running_ShowsTheSentenceTheProgressAndTheTimeLeft()
        {
            var items = new[] { Item(4, delivered: 1800L, required: 2500, daysLeft: 92, completed: false) };

            var rows = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].ContractId, Is.EqualTo(4));
            Assert.That(rows[0].Completed, Is.False);
            Assert.That(rows[0].Description.ToPlainText(), Is.EqualTo("Deliver  to Oakton"));
            CollectionAssert.Contains(rows[0].Description.Segments, TextLineSegment.CargoIcon("wood"));
            Assert.That(rows[0].Progress.ToPlainText(), Is.EqualTo("Progress: 1,800/2,500"));
            Assert.That(rows[0].TimeLeft.ToPlainText(), Is.EqualTo("Time left: 92 days"));
            Assert.That(rows[0].TileX, Is.EqualTo(10));
            Assert.That(rows[0].TileY, Is.EqualTo(12));
        }

        [Test]
        public void BuildRows_OneDayLeft_UsesTheSingularText()
        {
            var items = new[] { Item(1, delivered: 5L, required: 50, daysLeft: 1, completed: false) };

            var rows = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].TimeLeft.ToPlainText(), Is.EqualTo("Time left: 1 day"));
        }

        [Test]
        public void BuildRows_DeliveredAboveTheGoal_ShowsTheGoalAsProgress()
        {
            var items = new[] { Item(1, delivered: 70L, required: 50, daysLeft: 10, completed: false) };

            var rows = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Progress.ToPlainText(), Is.EqualTo("Progress: 50/50"));
        }

        [Test]
        public void BuildRows_Completed_ReplacesTheProgressByTheClaimPromptAndHidesTheTimeLeft()
        {
            var items = new[] { Item(2, delivered: 250L, required: 250, daysLeft: 0, completed: true) };

            var rows = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Completed, Is.True);
            Assert.That(rows[0].Progress.ToPlainText(), Is.EqualTo("Completed! Click to claim Reward"));
            Assert.That(rows[0].TimeLeft.IsEmpty, Is.True);
        }

        [Test]
        public void BuildRows_KeepsTheInputOrder()
        {
            var items = new[]
            {
                Item(9, delivered: 0L, required: 50, daysLeft: 5, completed: false),
                Item(3, delivered: 50L, required: 50, daysLeft: 0, completed: true)
            };

            var rows = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].ContractId, Is.EqualTo(9));
            Assert.That(rows[1].ContractId, Is.EqualTo(3));
        }

        [Test]
        public void BuildRows_SameItems_BuildEqualRows()
        {
            var items = new[] { Item(1, delivered: 5L, required: 50, daysLeft: 3, completed: false) };

            var first = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);
            var second = ActiveContractsViewModel.BuildRows(items, CreateText(), _culture);
            var changed = ActiveContractsViewModel.BuildRows(
                new[] { Item(1, delivered: 6L, required: 50, daysLeft: 3, completed: false) },
                CreateText(),
                _culture);

            Assert.That(second[0], Is.EqualTo(first[0]));
            Assert.That(changed[0], Is.Not.EqualTo(first[0]));
        }

        [Test]
        public void BuildRows_Empty_ReturnsNoRows()
        {
            var rows = ActiveContractsViewModel.BuildRows(Array.Empty<ActiveContractItem>(), CreateText(), _culture);

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public void ActiveContractItem_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => new ActiveContractItem(1, " ", "Oakton", 0, 0, 0L, 5, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveContractItem(1, "wood", "Oakton", 0, 0, -1L, 5, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveContractItem(1, "wood", "Oakton", 0, 0, 0L, 0, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveContractItem(1, "wood", "Oakton", 0, 0, 0L, 5, -1, false));
        }

        private static ActiveContractItem Item(int id, long delivered, int required, int daysLeft, bool completed)
        {
            return new ActiveContractItem(id, "wood", "Oakton", 10, 12, delivered, required, daysLeft, completed);
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.ActiveContractsTitle, "Active contracts:");
            text.Set(GoalsUiKeys.ActiveContractsDeliver, "Deliver {0} to {1}");
            text.Set(GoalsUiKeys.ActiveContractsProgress, "Progress: {0}/{1}");
            text.Set(GoalsUiKeys.ActiveContractsTimeLeft, "Time left: {0} days");
            text.Set(GoalsUiKeys.ActiveContractsTimeLeftOne, "Time left: {0} day");
            text.Set(GoalsUiKeys.ActiveContractsCompleted, "Completed! Click to claim Reward");
            return text;
        }
    }
}
