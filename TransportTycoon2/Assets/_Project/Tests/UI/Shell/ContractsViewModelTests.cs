#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    [TestFixture]
    public sealed class ContractsViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildRows_TownContract_ShowsCargoTargetProgressDeadlineAndBonus()
        {
            ShellTextCache text = CreateText();
            var items = new[]
            {
                new ContractListItem(7, "wood", ContractTargetKind.Town, 2, null, 10, 12, 30L, 120, "1901-05-06", 1_440L)
            };

            var rows = ContractsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].ContractId, Is.EqualTo(7));
            Assert.That(rows[0].Line.ToPlainText(), Is.EqualTo("wood to town #2 · 30 / 120 · by 1901-05-06 · bonus 1,440"));
            Assert.That(rows[0].Line.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("wood")));
            Assert.That(rows[0].TileX, Is.EqualTo(10));
            Assert.That(rows[0].TileY, Is.EqualTo(12));
        }

        [Test]
        public void BuildRows_IndustryContract_UsesTheLocalizedIndustryName()
        {
            ShellTextCache text = CreateText();
            var items = new[]
            {
                new ContractListItem(
                    9,
                    "planks",
                    ContractTargetKind.Industry,
                    1,
                    "industry.sawmill",
                    3,
                    4,
                    0L,
                    50,
                    "1902-01-01",
                    900L)
            };

            var rows = ContractsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows[0].Line.ToPlainText(), Is.EqualTo("planks to Sawmill #1 · 0 / 50 · by 1902-01-01 · bonus 900"));
        }

        [Test]
        public void BuildRows_DeliveredAboveRequired_IsCappedAtRequired()
        {
            ShellTextCache text = CreateText();
            var items = new[]
            {
                new ContractListItem(1, "wood", ContractTargetKind.Town, 0, null, 0, 0, 500L, 120, "1901-01-01", 10L)
            };

            var rows = ContractsViewModel.BuildRows(items, text, _culture);

            Assert.That(rows[0].Line.ToPlainText(), Does.Contain("120 / 120"));
        }

        [Test]
        public void BuildRows_NoContracts_GivesNoRowsSoThePanelShowsItsEmptyState()
        {
            var rows = ContractsViewModel.BuildRows(Array.Empty<ContractListItem>(), CreateText(), _culture);

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public void ListItem_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(
                () => new ContractListItem(1, " ", ContractTargetKind.Town, 0, null, 0, 0, 0L, 1, "d", 0L));
            Assert.Throws<ArgumentException>(
                () => new ContractListItem(1, "wood", ContractTargetKind.Industry, 0, null, 0, 0, 0L, 1, "d", 0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ContractListItem(1, "wood", ContractTargetKind.Town, 0, null, 0, 0, 0L, 0, "d", 0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ContractListItem(1, "wood", ContractTargetKind.Town, 0, null, 0, 0, -1L, 1, "d", 0L));
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.ContractsRowTown, "{0} to town #{1} · {2} / {3} · by {4} · bonus {5}");
            text.Set(GoalsUiKeys.ContractsRowIndustry, "{0} to {1} #{2} · {3} / {4} · by {5} · bonus {6}");
            text.Set("industry.sawmill", "Sawmill");
            return text;
        }
    }
}
