#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class TownListItemTests
    {
        [Test]
        public void Constructor_ValidValues_StoresSnapshot()
        {
            var needs = new[]
            {
                new TownNeedListLine("wood", "cargo.wood", 1, 4, 10),
                new TownNeedListLine("wheat", "cargo.wheat", 1, 10, 10),
                new TownNeedListLine("planks", "cargo.planks", 2, 0, 10)
            };

            var item = new TownListItem(
                townId: 2,
                tileX: 5,
                tileY: 7,
                population: 920,
                growthLevel: 1,
                maxGrowthLevel: 5,
                nextLevelPopulation: 1500,
                lastMonthGrowth: 30,
                needs,
                monthPassengersGenerated: 12,
                monthPassengersTransported: 8,
                monthMailGenerated: 3,
                monthMailTransported: 1);

            Assert.That(item.TownId, Is.EqualTo(2));
            Assert.That(item.TileX, Is.EqualTo(5));
            Assert.That(item.TileY, Is.EqualTo(7));
            Assert.That(item.Population, Is.EqualTo(920));
            Assert.That(item.GrowthLevel, Is.EqualTo(1));
            Assert.That(item.MaxGrowthLevel, Is.EqualTo(5));
            Assert.That(item.NextLevelPopulation, Is.EqualTo(1500));
            Assert.That(item.IsMaxGrowthLevel, Is.False);
            Assert.That(item.LastMonthGrowth, Is.EqualTo(30));
            Assert.That(item.Needs.Count, Is.EqualTo(3));
            Assert.That(item.CountNeeds(1), Is.EqualTo(2));
            Assert.That(item.CountNeeds(2), Is.EqualTo(1));
            Assert.That(item.CountNeeds(3), Is.EqualTo(0));
            Assert.That(item.MonthPassengersGenerated, Is.EqualTo(12L));
            Assert.That(item.MonthPassengersTransported, Is.EqualTo(8L));
            Assert.That(item.MonthMailGenerated, Is.EqualTo(3L));
            Assert.That(item.MonthMailTransported, Is.EqualTo(1L));
        }

        [Test]
        public void Constructor_GrowthLevelAboveMax_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(growthLevel: 6, maxGrowthLevel: 5, next: 0));
        }

        [Test]
        public void Constructor_NextLevelNotAbovePopulation_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(growthLevel: 0, maxGrowthLevel: 5, next: 100));
        }

        [Test]
        public void Constructor_NextLevelAtMax_MustBeZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(growthLevel: 5, maxGrowthLevel: 5, next: 800));
            Assert.That(Create(growthLevel: 5, maxGrowthLevel: 5, next: 0).IsMaxGrowthLevel, Is.True);
        }

        [Test]
        public void Constructor_NegativeLastMonthGrowth_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(lastMonthGrowth: -1));
        }

        [Test]
        public void NeedLine_LevelOutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownNeedListLine("wood", "cargo.wood", 0, 0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TownNeedListLine("wood", "cargo.wood", 4, 0, 10));
        }

        [Test]
        public void Equals_SameValues_True()
        {
            Assert.That(Create().Equals(Create()), Is.True);
            Assert.That(Create().Equals(Create(lastMonthGrowth: 20)), Is.False);
        }

        private static TownListItem Create(
            int growthLevel = 0,
            int maxGrowthLevel = 5,
            int next = 800,
            int lastMonthGrowth = 0)
        {
            var needs = new[] { new TownNeedListLine("wood", "cargo.wood", 1, 1, 10) };
            return new TownListItem(
                1,
                2,
                3,
                100,
                growthLevel,
                maxGrowthLevel,
                next,
                lastMonthGrowth,
                needs,
                1,
                0,
                0,
                0);
        }
    }
}
