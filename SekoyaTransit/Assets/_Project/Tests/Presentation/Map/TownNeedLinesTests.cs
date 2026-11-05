#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class TownNeedLinesTests
    {
        [TestCase(0, TownNeedLineStatus.Unmet)]
        [TestCase(1, TownNeedLineStatus.Unmet)]
        [TestCase(49, TownNeedLineStatus.Unmet)]
        [TestCase(50, TownNeedLineStatus.PartlyMet)]
        [TestCase(99, TownNeedLineStatus.PartlyMet)]
        [TestCase(100, TownNeedLineStatus.FullyMet)]
        [TestCase(250, TownNeedLineStatus.FullyMet)]
        public void Create_LastMonthSupply_UsesTheGrowthRule(int supply, TownNeedLineStatus expected)
        {
            TownNeedListLine line = TownNeedLines.Create(new TownNeed("wood", lastMonthSupply: supply), CreateCatalog());

            Assert.That(line.Status, Is.EqualTo(expected));
            Assert.That(line.LastMonthSupply, Is.EqualTo(supply));
            Assert.That(line.Demand, Is.EqualTo(100));
            Assert.That(line.CargoNameKey, Is.EqualTo("cargo.wood"));
            Assert.That(line.Level, Is.EqualTo(1));
        }

        private static ContentCatalog CreateCatalog()
        {
            var cargo = new[] { new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1) };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                demandPerNeedPerMonth: new TownNeedLevelAmounts(100, 100, 100));
            return new ContentCatalog(cargo, Array.Empty<IndustryDefinition>(), townNeeds);
        }
    }
}
