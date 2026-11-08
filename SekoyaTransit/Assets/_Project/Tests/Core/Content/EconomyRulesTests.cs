#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.Tests.Content
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

        [TestCase(-1, 4, 30, 25)]
        [TestCase(30, 0, 30, 25)]
        [TestCase(30, 4, 3, 25)]
        [TestCase(30, 4, 0, 25)]
        [TestCase(30, 4, 30, 0)]
        [TestCase(30, 4, 30, -1)]
        [TestCase(30, 4, 30, 101)]
        public void DeliveryPaySettings_InvalidValues_Throw(
            int referenceDistance,
            int minimumDistance,
            int fullPayDistance,
            int beyondFullPayPercent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeliveryPaySettings(referenceDistance, minimumDistance, fullPayDistance, beyondFullPayPercent));
        }

        [TestCase(0, 12, 2, 2)]
        [TestCase(101, 12, 2, 2)]
        [TestCase(80, 0, 2, 2)]
        [TestCase(80, 12, -1, 2)]
        [TestCase(80, 12, 2, 0)]
        public void IndustryGrowthSettings_InvalidValues_Throw(int served, int months, int maxLevel, int multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = new IndustryGrowthSettings(served, months, maxLevel, multiplier));
        }

        [TestCase(0, 4)]
        [TestCase(1, 8)]
        [TestCase(2, 16)]
        [TestCase(5, 16)]
        public void ScaleOutput_MultipliesPerTierAndCapsAtTheMaximum(int level, int expected)
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);

            Assert.That(growth.ScaleOutput(4, level), Is.EqualTo(expected));
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(3, 4)]
        public void GetFactor_IsTheMultiplierToThePowerOfTheTier(int level, int expected)
        {
            var growth = new IndustryGrowthSettings(80, 12, 2, 2);

            Assert.That(growth.GetFactor(level), Is.EqualTo(expected));
        }

        [Test]
        public void GetFactor_Disabled_IsAlwaysOne()
        {
            Assert.That(IndustryGrowthSettings.Disabled.GetFactor(3), Is.EqualTo(1));
        }
    }
}
