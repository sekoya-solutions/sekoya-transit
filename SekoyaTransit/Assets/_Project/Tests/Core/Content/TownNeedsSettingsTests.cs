#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.Tests.Content
{
    [TestFixture]
    public sealed class TownNeedsSettingsTests
    {
        private static readonly TownNeedTier[] _tiers =
        {
            new TownNeedTier(0, 3, 0, 0),
            new TownNeedTier(800, 3, 1, 0),
            new TownNeedTier(1500, 4, 1, 0)
        };

        [Test]
        public void Constructor_GrowthValuesOmitted_UsesDefaults()
        {
            TownNeedsSettings settings = Create();

            Assert.That(settings.PopulationPerPartlyMetNeed, Is.EqualTo(new TownNeedLevelAmounts(10, 15, 20)));
            Assert.That(settings.PopulationPerFullyMetNeed, Is.EqualTo(new TownNeedLevelAmounts(20, 30, 40)));
            Assert.That(settings.AllNeedsMetGrowthMultiplier, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_PartialAboveFull_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(
                partlyMet: new TownNeedLevelAmounts(10, 31, 20),
                fullyMet: new TownNeedLevelAmounts(20, 30, 40)));
        }

        [Test]
        public void Constructor_FullyMetZero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(
                partlyMet: new TownNeedLevelAmounts(0, 0, 0),
                fullyMet: new TownNeedLevelAmounts(20, 30, 0)));
        }

        [Test]
        public void Constructor_MultiplierBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(multiplier: 0));
        }

        [Test]
        public void LevelAmounts_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownNeedLevelAmounts(1, -1, 1));
        }

        [TestCase(1, 10)]
        [TestCase(2, 20)]
        [TestCase(3, 30)]
        public void LevelAmountsGet_Level_ReturnsThatLevel(int level, int expected)
        {
            Assert.That(new TownNeedLevelAmounts(10, 20, 30).Get(level), Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void LevelAmountsGet_OutOfRange_Throws(int level)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownNeedLevelAmounts(10, 20, 30).Get(level));
        }

        [TestCase(0, 0)]
        [TestCase(799, 0)]
        [TestCase(800, 1)]
        [TestCase(1499, 1)]
        [TestCase(1500, 2)]
        [TestCase(10000, 2)]
        public void GetTierIndexForPopulation_Population_ReturnsTierIndex(int population, int expected)
        {
            Assert.That(Create().GetTierIndexForPopulation(population), Is.EqualTo(expected));
        }

        private static TownNeedsSettings Create(
            TownNeedLevelAmounts? partlyMet = null,
            TownNeedLevelAmounts? fullyMet = null,
            int multiplier = TownNeedsSettings.DefaultAllNeedsMetGrowthMultiplier)
        {
            return new TownNeedsSettings(
                100,
                500,
                10000,
                _tiers,
                populationPerPartlyMetNeed: partlyMet,
                populationPerFullyMetNeed: fullyMet,
                allNeedsMetGrowthMultiplier: multiplier);
        }
    }
}
