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
        public void Constructor_ValidNeeds_StoresSnapshot()
        {
            var needs = new[]
            {
                new TownNeedListLine("wood", "cargo.wood", 4, 10),
                new TownNeedListLine("wheat", "cargo.wheat", 10, 10)
            };

            var item = new TownListItem(
                townId: 2,
                tileX: 5,
                tileY: 7,
                population: 420,
                satisfiedNeeds: 1,
                totalNeeds: 2,
                needs,
                monthPassengersGenerated: 12,
                monthPassengersTransported: 8,
                monthMailGenerated: 3,
                monthMailTransported: 1);

            Assert.That(item.TownId, Is.EqualTo(2));
            Assert.That(item.TileX, Is.EqualTo(5));
            Assert.That(item.TileY, Is.EqualTo(7));
            Assert.That(item.Population, Is.EqualTo(420));
            Assert.That(item.SatisfiedNeeds, Is.EqualTo(1));
            Assert.That(item.TotalNeeds, Is.EqualTo(2));
            Assert.That(item.Needs.Count, Is.EqualTo(2));
            Assert.That(item.Needs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(item.MonthPassengersGenerated, Is.EqualTo(12L));
            Assert.That(item.MonthPassengersTransported, Is.EqualTo(8L));
            Assert.That(item.MonthMailGenerated, Is.EqualTo(3L));
            Assert.That(item.MonthMailTransported, Is.EqualTo(1L));
        }

        [Test]
        public void Constructor_SatisfiedExceedsTotal_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TownListItem(
                    0,
                    0,
                    0,
                    100,
                    satisfiedNeeds: 2,
                    totalNeeds: 1,
                    Array.Empty<TownNeedListLine>(),
                    0,
                    0,
                    0,
                    0));
        }

        [Test]
        public void Equals_SameValues_True()
        {
            var needs = new[] { new TownNeedListLine("wood", "cargo.wood", 1, 10) };
            var left = new TownListItem(1, 2, 3, 100, 0, 1, needs, 1, 0, 0, 0);
            var right = new TownListItem(1, 2, 3, 100, 0, 1, needs, 1, 0, 0, 0);
            Assert.That(left.Equals(right), Is.True);
        }
    }
}
