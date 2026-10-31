#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class TownsViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void FormatSummary_NamedAndUnnamed_ShowsNameAndPopulation()
        {
            ShellTextCache text = CreateText();

            TextLine unnamed = TownsViewModel.FormatSummary(CreateTown(3, 120), text, _culture);
            TextLine named = TownsViewModel.FormatSummary(CreateTown(3, 120, name: "Paris"), text, _culture);

            Assert.That(unnamed.ToPlainText(), Is.EqualTo("#3 · 120"));
            Assert.That(named.ToPlainText(), Is.EqualTo("Paris · 120"));
            CollectionAssert.Contains(named.Segments, TextLineSegment.Population);
        }

        [Test]
        public void FormatGrowthLevel_BelowMax_ShowsLevelAndNextThreshold()
        {
            TownListItem town = CreateTown(1, 420, growthLevel: 0, maxGrowthLevel: 5, nextLevelPopulation: 800);

            TextLine line = TownsViewModel.FormatGrowthLevel(town, CreateText(), _culture);

            Assert.That(line.ToPlainText(), Is.EqualTo("Growth level: 0/5, next at 800"));
            CollectionAssert.Contains(line.Segments, TextLineSegment.Population);
        }

        [Test]
        public void FormatGrowthLevel_AtMax_ShowsMaxWording()
        {
            TownListItem town = CreateTown(1, 6000, growthLevel: 5, maxGrowthLevel: 5, nextLevelPopulation: 0);

            TextLine line = TownsViewModel.FormatGrowthLevel(town, CreateText(), _culture);

            Assert.That(line.ToPlainText(), Is.EqualTo("Growth level: 5/5 (max)"));
        }

        [Test]
        public void FormatLastMonthGrowth_ShowsGainWithPopulationIcon()
        {
            TownListItem town = CreateTown(1, 420, lastMonthGrowth: 60);

            TextLine line = TownsViewModel.FormatLastMonthGrowth(town, CreateText(), _culture);

            Assert.That(line.ToPlainText(), Is.EqualTo("+60 last month"));
            CollectionAssert.Contains(line.Segments, TextLineSegment.Population);
        }

        [Test]
        public void FormatNeedLine_ShowsIconDemandAndLastMonthSupply()
        {
            var need = new TownNeedListLine("wood", "cargo.wood", 1, 32, 40);

            TextLine line = TownsViewModel.FormatNeedLine(need, CreateText(), _culture);

            Assert.That(line.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("wood")));
            Assert.That(line.ToPlainText(), Is.EqualTo(" 40/month (32 supplied last month)"));
        }

        [TestCase(0, TownNeedLineStatus.Unmet, null)]
        [TestCase(1, TownNeedLineStatus.PartlyMet, TownsViewModel.PartlyMetUssClass)]
        [TestCase(39, TownNeedLineStatus.PartlyMet, TownsViewModel.PartlyMetUssClass)]
        [TestCase(40, TownNeedLineStatus.FullyMet, TownsViewModel.FullyMetUssClass)]
        [TestCase(55, TownNeedLineStatus.FullyMet, TownsViewModel.FullyMetUssClass)]
        public void NeedStatus_LastMonthSupply_PicksStatusAndColorClass(
            int supply,
            TownNeedLineStatus expectedStatus,
            string? expectedClass)
        {
            var need = new TownNeedListLine("wood", "cargo.wood", 1, supply, 40);

            Assert.That(need.Status, Is.EqualTo(expectedStatus));
            Assert.That(TownsViewModel.GetNeedStatusClass(need.Status), Is.EqualTo(expectedClass));
        }

        [TestCase(1, GameUiKeys.TownsNeedsRaw)]
        [TestCase(2, GameUiKeys.TownsNeedsProcessed)]
        [TestCase(3, GameUiKeys.TownsNeedsFinished)]
        public void GetNeedsHeaderKey_Level_ReturnsLevelHeading(int level, string expected)
        {
            Assert.That(TownsViewModel.GetNeedsHeaderKey(level), Is.EqualTo(expected));
        }

        [Test]
        public void GetNeedsHeaderKey_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TownsViewModel.GetNeedsHeaderKey(4));
        }

        [Test]
        public void FormatPassengerAndMailLines_ShowGeneratedAndTransported()
        {
            ShellTextCache text = CreateText();
            TownListItem town = CreateTown(1, 120);

            TextLine passengers = TownsViewModel.FormatPassengerLine(town, text, _culture);
            TextLine mail = TownsViewModel.FormatMailLine(town, text, _culture);

            Assert.That(passengers.ToPlainText(), Is.EqualTo("Generated 7 / 4"));
            Assert.That(passengers.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("passengers")));
            Assert.That(mail.ToPlainText(), Is.EqualTo("Generated 3 / 2"));
            Assert.That(mail.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("mail")));
        }

        [Test]
        public void FormatRules_UsesTheDataValues()
        {
            ShellTextCache text = CreateText();
            var rules = new TownGrowthRulesInfo(10, 15, 20, 20, 30, 40, 2);

            Assert.That(
                TownsViewModel.FormatPartlyMetRule(rules, text, _culture).ToPlainText(),
                Is.EqualTo("Partly 10 / 15 / 20 growth"));
            Assert.That(
                TownsViewModel.FormatFullyMetRule(rules, text, _culture).ToPlainText(),
                Is.EqualTo("Fully 20 / 30 / 40 growth"));
            Assert.That(
                TownsViewModel.FormatAllMetRule(rules, text, _culture).ToPlainText(),
                Is.EqualTo("All x2 growth"));
            CollectionAssert.Contains(
                TownsViewModel.FormatAllMetRule(rules, text, _culture).Segments,
                TextLineSegment.Population);
        }

        [Test]
        public void GrowthRules_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownGrowthRulesInfo(-1, 0, 0, 1, 1, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownGrowthRulesInfo(0, 0, 0, 1, 1, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownGrowthRulesInfo(0, 0, 0, 1, 1, 1, 2).GetFullyMet(0));
        }

        [Test]
        public void ComputeFingerprint_ChangesWithStructureNotWithNumbers()
        {
            TownNeedListLine wood = new TownNeedListLine("wood", "k", 1, 1, 10);
            TownNeedListLine planks = new TownNeedListLine("planks", "k", 2, 0, 10);
            TownNeedListLine woodSupplied = new TownNeedListLine("wood", "k", 1, 10, 10);
            int fingerprint = TownsViewModel.ComputeFingerprint(new[] { CreateTown(1, 100, needs: new[] { wood }) });

            Assert.That(
                TownsViewModel.ComputeFingerprint(new[] { CreateTown(1, 250, needs: new[] { woodSupplied }) }),
                Is.EqualTo(fingerprint));
            Assert.That(
                TownsViewModel.ComputeFingerprint(new[] { CreateTown(1, 100, needs: new[] { wood, planks }) }),
                Is.Not.EqualTo(fingerprint));
            Assert.That(
                TownsViewModel.ComputeFingerprint(new[] { CreateTown(1, 100, needs: new[] { planks }) }),
                Is.Not.EqualTo(fingerprint));
            Assert.That(
                TownsViewModel.ComputeFingerprint(new[] { CreateTown(2, 100, needs: new[] { wood }) }),
                Is.Not.EqualTo(fingerprint));
        }

        private static TownListItem CreateTown(
            int townId,
            int population,
            int growthLevel = 0,
            int maxGrowthLevel = 5,
            int nextLevelPopulation = 10000,
            int lastMonthGrowth = 0,
            TownNeedListLine[]? needs = null,
            string name = "")
        {
            return new TownListItem(
                townId,
                0,
                0,
                population,
                growthLevel,
                maxGrowthLevel,
                nextLevelPopulation,
                lastMonthGrowth,
                needs ?? Array.Empty<TownNeedListLine>(),
                7L,
                4L,
                3L,
                2L,
                name);
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GameUiKeys.TownsSummary, "{0} · {1}");
            text.Set(GameUiKeys.TownsGrowthLevel, "Growth level: {0}/{1}, next at {2}");
            text.Set(GameUiKeys.TownsGrowthLevelMax, "Growth level: {0}/{1} (max)");
            text.Set(GameUiKeys.TownsLastMonthGrowth, "+{0} last month");
            text.Set(GameUiKeys.TownsNeedLine, "{0} {1}/month ({2} supplied last month)");
            text.Set(GameUiKeys.TownsPassengerLine, "Generated {0} / {1}");
            text.Set(GameUiKeys.TownsMailLine, "Generated {0} / {1}");
            text.Set(GameUiKeys.TownsRulesPartlyMet, "Partly {0} / {1} / {2} {3}growth");
            text.Set(GameUiKeys.TownsRulesFullyMet, "Fully {0} / {1} / {2} {3}growth");
            text.Set(GameUiKeys.TownsRulesAllMet, "All x{0} {1}growth");
            return text;
        }
    }
}
