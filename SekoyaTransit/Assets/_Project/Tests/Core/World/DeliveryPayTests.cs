#nullable enable
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class DeliveryPayTests
    {
        private static readonly DeliveryPaySettings _pay = new DeliveryPaySettings(30, 4, 30, 100);
        private static readonly DeliveryPaySettings _curve = new DeliveryPaySettings(20, 4, 30, 25);

        [Test]
        public void ResolveDistance_UsesManhattanTiles()
        {
            int distance = DeliveryPay.ResolveDistance(new TileCoordinate(2, 3), new TileCoordinate(12, 23), _pay);

            Assert.That(distance, Is.EqualTo(30));
        }

        [Test]
        public void ResolveDistance_CloseOrUnknownOrigin_PaysTheMinimumDistance()
        {
            Assert.That(DeliveryPay.ResolveDistance(new TileCoordinate(5, 5), new TileCoordinate(6, 6), _pay), Is.EqualTo(4));
            Assert.That(DeliveryPay.ResolveDistance(null, new TileCoordinate(6, 6), _pay), Is.EqualTo(4));
        }

        [Test]
        public void Compute_AtReferenceDistance_PaysThePrice()
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(8, 10, 30, _pay, ref remainder);

            Assert.That(income, Is.EqualTo(80L));
            Assert.That(remainder, Is.Zero);
        }

        [Test]
        public void Compute_ScalesWithDistance()
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(8, 10, 60, _pay, ref remainder);

            Assert.That(income, Is.EqualTo(160L));
        }

        [Test]
        public void Compute_UnitByUnit_PaysTheSameAsOneDelivery()
        {
            int remainder = 0;
            long unitByUnit = 0L;
            for (int unit = 0; unit < 10; unit++)
            {
                unitByUnit += DeliveryPay.Compute(6, 1, 17, _pay, ref remainder);
            }

            int wholeRemainder = 0;
            long whole = DeliveryPay.Compute(6, 10, 17, _pay, ref wholeRemainder);

            // 6 x 10 x 17 / 30 = 34: the fraction carries from unit to unit instead of being lost.
            Assert.That(whole, Is.EqualTo(34L));
            Assert.That(unitByUnit, Is.EqualTo(whole));
            Assert.That(remainder, Is.EqualTo(wholeRemainder));
        }

        [TestCase(4, 4)]
        [TestCase(30, 30)]
        [TestCase(31, 30)]
        [TestCase(34, 31)]
        [TestCase(60, 37)]
        [TestCase(178, 67)]
        public void PaidDistance_BeyondTheFullPayDistance_CountsOnlyTheShare(int distance, int expected)
        {
            Assert.That(DeliveryPay.PaidDistance(distance, _curve), Is.EqualTo(expected));
        }

        [Test]
        public void PaidDistance_FollowsTheSettings()
        {
            var other = new DeliveryPaySettings(10, 2, 50, 40);

            Assert.That(DeliveryPay.PaidDistance(50, other), Is.EqualTo(50));
            Assert.That(DeliveryPay.PaidDistance(100, other), Is.EqualTo(70));
        }

        [Test]
        public void PaidDistance_AtOneHundredPercent_IsTheStraightLine()
        {
            Assert.That(DeliveryPay.PaidDistance(500, _pay), Is.EqualTo(500));
        }

        [Test]
        public void Compute_BeyondTheFullPayDistance_UsesThePaidDistanceAndKeepsTheFraction()
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(5, 10, 178, _curve, ref remainder);

            Assert.That(income, Is.EqualTo(167L));
            Assert.That(remainder, Is.EqualTo(10));
        }

        [Test]
        public void Compute_ReferenceDistance_ComesFromTheSettings()
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(5, 10, 30, new DeliveryPaySettings(50, 4, 30, 25), ref remainder);

            Assert.That(income, Is.EqualTo(30L));
        }

        [Test]
        public void Compute_OnTheCurve_DoesNotDependOnHowUnloadingIsSplit()
        {
            int remainder = 0;
            long unitByUnit = 0;
            for (int unit = 0; unit < 10; unit++)
            {
                unitByUnit += DeliveryPay.Compute(6, 1, 77, _curve, ref remainder);
            }

            int wholeRemainder = 0;
            long whole = DeliveryPay.Compute(6, 10, 77, _curve, ref wholeRemainder);

            Assert.That(unitByUnit, Is.EqualTo(whole));
            Assert.That(remainder, Is.EqualTo(wholeRemainder));
        }

        [Test]
        public void Compute_LongerDistance_NeverPaysLess()
        {
            long previous = 0;
            for (int distance = 4; distance <= 1024; distance++)
            {
                int remainder = 0;
                long income = DeliveryPay.Compute(5, 100, distance, _curve, ref remainder);

                Assert.That(income, Is.GreaterThanOrEqualTo(previous), distance.ToString(CultureInfo.InvariantCulture));
                previous = income;
            }
        }

        [Test]
        public void Compute_FractionSavedUnderAnOlderReferenceDistance_StillPays()
        {
            int remainder = 25;

            long income = DeliveryPay.Compute(1, 1, 30, _curve, ref remainder);

            Assert.That(income, Is.EqualTo(2L));
            Assert.That(remainder, Is.EqualTo(15));
        }

        [Test]
        public void Compute_Flat_IgnoresDistanceAndRemainder()
        {
            int remainder = 7;

            long income = DeliveryPay.Compute(8, 10, 60, DeliveryPaySettings.Flat, ref remainder);

            Assert.That(income, Is.EqualTo(80L));
            Assert.That(remainder, Is.EqualTo(7));
        }
    }
}
