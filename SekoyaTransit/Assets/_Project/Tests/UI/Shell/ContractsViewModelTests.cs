#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class ContractsViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildRows_Proposal_ShowsTheSentenceTheRewardAndAcceptAndDecline()
        {
            var items = new[] { Item(7, ContractUiStatus.Proposal, canAccept: true) };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].ContractId, Is.EqualTo(7));
            Assert.That(rows[0].Status, Is.EqualTo(ContractUiStatus.Proposal));
            Assert.That(rows[0].Description.ToPlainText(), Is.EqualTo("Deliver 250  to Oakton in 12 months"));
            CollectionAssert.Contains(rows[0].Description.Segments, TextLineSegment.CargoIcon("wood"));
            Assert.That(rows[0].Detail.ToPlainText(), Is.EqualTo("Reward: 2,500, 100"));
            CollectionAssert.AreEqual(
                new[] { TextLineSegment.Cash, TextLineSegment.Population },
                IconKinds(rows[0].Detail));
            Assert.That(rows[0].Locked, Is.False);
            Assert.That(rows[0].Buttons.Count, Is.EqualTo(2));
            Assert.That(rows[0].Buttons[0], Is.EqualTo(new ContractRowButton(ContractUiAction.Accept, TextLine.Plain("Accept"), true)));
            Assert.That(rows[0].Buttons[1], Is.EqualTo(new ContractRowButton(ContractUiAction.Decline, TextLine.Plain("Decline"), true)));
            Assert.That(rows[0].TileX, Is.EqualTo(10));
            Assert.That(rows[0].TileY, Is.EqualTo(12));
        }

        [Test]
        public void BuildRows_ProposalThatCannotBeAccepted_IsGreyedOutButCanStillBeDeclined()
        {
            var items = new[] { Item(1, ContractUiStatus.Proposal, canAccept: false) };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Locked, Is.True);
            Assert.That(rows[0].Buttons[0].Action, Is.EqualTo(ContractUiAction.Accept));
            Assert.That(rows[0].Buttons[0].Enabled, Is.False);
            Assert.That(rows[0].Buttons[1].Action, Is.EqualTo(ContractUiAction.Decline));
            Assert.That(rows[0].Buttons[1].Enabled, Is.True);
        }

        [Test]
        public void BuildRows_Accepted_ShowsProgressAndDeadlineWithCancel()
        {
            var items = new[] { Item(2, ContractUiStatus.Accepted, delivered: 180L, dateText: "1900/03/15") };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Detail.ToPlainText(), Is.EqualTo("Accepted: 180 / 250 until 1900/03/15"));
            Assert.That(rows[0].Locked, Is.False);
            Assert.That(rows[0].Buttons.Count, Is.EqualTo(1));
            Assert.That(rows[0].Buttons[0], Is.EqualTo(new ContractRowButton(ContractUiAction.Cancel, TextLine.Plain("Cancel"), true)));
        }

        [Test]
        public void BuildRows_Completed_ShowsTheCompletionDateWithTheRewardButton()
        {
            var items = new[] { Item(3, ContractUiStatus.Completed, delivered: 300L, dateText: "1900/03/25") };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            // Delivered above the amount is capped at the amount.
            Assert.That(rows[0].Detail.ToPlainText(), Is.EqualTo("Completed: 250 / 250 at 1900/03/25"));
            Assert.That(rows[0].Buttons[0].Action, Is.EqualTo(ContractUiAction.ClaimReward));
            Assert.That(rows[0].Buttons[0].Label.ToPlainText(), Is.EqualTo("Reward 2,500, 100"));
            CollectionAssert.AreEqual(
                new[] { TextLineSegment.Cash, TextLineSegment.Population },
                IconKinds(rows[0].Buttons[0].Label));
        }

        [Test]
        public void BuildRows_TownAtItsMaximum_ShowsTheRewardWithoutAPopulationGain()
        {
            var items = new[] { Item(5, ContractUiStatus.Proposal, populationGain: 0) };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Detail.ToPlainText(), Is.EqualTo("Reward: 2,500"));
            CollectionAssert.AreEqual(new[] { TextLineSegment.Cash }, IconKinds(rows[0].Detail));
        }

        [Test]
        public void BuildRows_NamesTheCargoByItsIconOnly()
        {
            var items = new[] { Item(6, ContractUiStatus.Proposal, cargoId: "iron_ore") };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Description.ToPlainText(), Does.Not.Contain("iron"));
            CollectionAssert.Contains(rows[0].Description.Segments, TextLineSegment.CargoIcon("iron_ore"));
        }

        [Test]
        public void BuildRows_Failed_ShowsTheFinalCountWithRemove()
        {
            var items = new[] { Item(4, ContractUiStatus.Failed, delivered: 245L, dateText: "1900/03/31") };

            var rows = ContractsViewModel.BuildRows(items, CreateText(), _culture);

            Assert.That(rows[0].Detail.ToPlainText(), Is.EqualTo("Failed: 245 / 250 at 1900/03/31"));
            Assert.That(rows[0].Buttons[0], Is.EqualTo(new ContractRowButton(ContractUiAction.Remove, TextLine.Plain("Remove"), true)));
        }

        [Test]
        public void BuildRows_NoContracts_GivesNoRowsSoThePanelShowsItsEmptyState()
        {
            var rows = ContractsViewModel.BuildRows(Array.Empty<ContractListItem>(), CreateText(), _culture);

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public void Rows_WithTheSameContentAreEqual()
        {
            var items = new[] { Item(7, ContractUiStatus.Proposal, canAccept: true) };

            var first = ContractsViewModel.BuildRows(items, CreateText(), _culture);
            var second = ContractsViewModel.BuildRows(items, CreateText(), _culture);
            var greyed = ContractsViewModel.BuildRows(
                new[] { Item(7, ContractUiStatus.Proposal, canAccept: false) },
                CreateText(),
                _culture);

            Assert.That(first[0], Is.EqualTo(second[0]));
            Assert.That(first[0], Is.Not.EqualTo(greyed[0]));
        }

        [Test]
        public void Header_ShowsTheContractsInTheListAndTheAcceptedCountAgainstTheirLimits()
        {
            ContractListItem item = Item(1, ContractUiStatus.Accepted);
            var data = new ContractsPanelData(new[] { item, item }, 3, 3, 6);

            Assert.That(ContractsViewModel.FormatAvailable(data, CreateText(), _culture), Is.EqualTo("Available: 2/6"));
            Assert.That(ContractsViewModel.FormatInProgress(data, CreateText(), _culture), Is.EqualTo("In progress: 3/3"));
        }

        [Test]
        public void FormatFooter_SaysWhenTheNextContractAppearsOrThatTheListIsFull()
        {
            ShellTextCache text = CreateText();
            ContractListItem item = Item(1, ContractUiStatus.Proposal, canAccept: true);
            var open = new ContractsPanelData(new[] { item }, 0, 3, 6);
            var full = new ContractsPanelData(new[] { item }, 0, 3, 1);

            Assert.That(ContractsViewModel.FormatFooter(open, text), Is.EqualTo("New contract will appear next month"));
            Assert.That(full.IsFull, Is.True);
            Assert.That(ContractsViewModel.FormatFooter(full, text), Is.EqualTo("The contract list is full"));
        }

        [Test]
        public void ListItem_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => Item(1, ContractUiStatus.Proposal, cargoId: " "));
            Assert.Throws<ArgumentOutOfRangeException>(() => Item(1, ContractUiStatus.Proposal, required: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Item(1, ContractUiStatus.Accepted, delivered: -1L));
            Assert.Throws<ArgumentOutOfRangeException>(() => Item(1, ContractUiStatus.Proposal, reward: -1L));
            Assert.Throws<ArgumentOutOfRangeException>(() => Item(1, ContractUiStatus.Proposal, durationMonths: 0));
        }

        private static ContractListItem Item(
            int id,
            ContractUiStatus status,
            string cargoId = "wood",
            long delivered = 0L,
            int required = 250,
            int durationMonths = 12,
            string dateText = "",
            long reward = 2_500L,
            int populationGain = 100,
            bool canAccept = true)
        {
            return new ContractListItem(
                id,
                cargoId,
                "Oakton",
                10,
                12,
                status,
                delivered,
                required,
                durationMonths,
                dateText,
                reward,
                populationGain,
                canAccept);
        }

        // The kinds of the cash and population icons of a line, in order.
        private static TextLineSegment[] IconKinds(TextLine line)
        {
            var icons = new System.Collections.Generic.List<TextLineSegment>();
            for (int index = 0; index < line.Segments.Count; index++)
            {
                TextLineSegmentKind kind = line.Segments[index].Kind;
                if (kind == TextLineSegmentKind.CashIcon || kind == TextLineSegmentKind.PopulationIcon)
                {
                    icons.Add(line.Segments[index]);
                }
            }

            return icons.ToArray();
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.ContractsAvailable, "Available: {0}/{1}");
            text.Set(GoalsUiKeys.ContractsInProgress, "In progress: {0}/{1}");
            text.Set(GoalsUiKeys.ContractsRowDeliver, "Deliver {0} {1} to {2} in {3} months");
            text.Set(GoalsUiKeys.ContractsRowReward, "Reward: {0}");
            text.Set(GoalsUiKeys.ContractsRowAccepted, "Accepted: {0} / {1} until {2}");
            text.Set(GoalsUiKeys.ContractsRowCompleted, "Completed: {0} / {1} at {2}");
            text.Set(GoalsUiKeys.ContractsRowFailed, "Failed: {0} / {1} at {2}");
            text.Set(GoalsUiKeys.ContractsAccept, "Accept");
            text.Set(GoalsUiKeys.ContractsDecline, "Decline");
            text.Set(GoalsUiKeys.ContractsCancel, "Cancel");
            text.Set(GoalsUiKeys.ContractsClaim, "Reward {0}");
            text.Set(GoalsUiKeys.ContractsRemove, "Remove");
            text.Set(GoalsUiKeys.ContractsFooterNext, "New contract will appear next month");
            text.Set(GoalsUiKeys.ContractsFooterFull, "The contract list is full");
            return text;
        }
    }
}
