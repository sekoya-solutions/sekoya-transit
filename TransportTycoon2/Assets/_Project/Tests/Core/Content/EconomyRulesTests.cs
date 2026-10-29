#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.Tests.Content
{
    [TestFixture]
    public sealed class EconomyRulesTests
    {
        [Test]
        public void EconomySettings_WithoutRules_PaysFlatAndDoesNotGrow()
        {
            var economy = new EconomySettings(5000L);

            Assert.That(economy.DeliveryPay.IsFlat, Is.True);
            Assert.That(economy.IndustryGrowth.MaxLevel, Is.Zero);
        }

        [TestCase(-1, 4)]
        [TestCase(30, 0)]
        public void DeliveryPaySettings_InvalidValues_Throw(int referenceDistance, int minimumDistance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeliveryPaySettings(referenceDistance, minimumDistance));
        }

        [TestCase(0, 12, 6, 25)]
        [TestCase(101, 12, 6, 25)]
        [TestCase(60, 0, 6, 25)]
        [TestCase(60, 12, -1, 25)]
        [TestCase(60, 12, 6, -1)]
        public void IndustryGrowthSettings_InvalidValues_Throw(int served, int months, int maxLevel, int step)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new IndustryGrowthSettings(served, months, maxLevel, step));
        }

        [TestCase(0, 4)]
        [TestCase(1, 5)]
        [TestCase(2, 6)]
        [TestCase(3, 7)]
        [TestCase(6, 10)]
        public void ScaleOutput_AddsAStepPerLevel(int level, int expected)
        {
            var growth = new IndustryGrowthSettings(60, 12, 6, 25);

            Assert.That(growth.ScaleOutput(4, level), Is.EqualTo(expected));
        }

        [Test]
        public void ScaleOutput_RoundsDown()
        {
            var growth = new IndustryGrowthSettings(60, 12, 6, 25);

            Assert.That(growth.ScaleOutput(3, 1), Is.EqualTo(3));
            Assert.That(growth.ScaleOutput(3, 2), Is.EqualTo(4));
        }
    }
}
