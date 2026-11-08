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
        private static readonly DeliveryPaySettings _pay = new DeliveryPaySettings(125, 4);

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

        [TestCase(4, 840L)]
        [TestCase(29, 1090L)]
        [TestCase(30, 1100L)]
        [TestCase(31, 1110L)]
        [TestCase(178, 2580L)]
        public void Compute_PaysThePricePlusTheBonusPerTile(int distance, long expected)
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(8, 100, distance, _pay, ref remainder);

            Assert.That(income, Is.EqualTo(expected));
            Assert.That(remainder, Is.Zero);
        }

        [Test]
        public void Compute_RateComesFromTheSettings()
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(8, 100, 30, new DeliveryPaySettings(250, 4), ref remainder);

            Assert.That(income, Is.EqualTo(1400L));
        }

        [Test]
        public void Compute_EveryTile_AddsTheSameAmount()
        {
            int remainder = 0;
            long previous = DeliveryPay.Compute(8, 100, 4, _pay, ref remainder);
            for (int distance = 5; distance <= 1024; distance++)
            {
                long income = DeliveryPay.Compute(8, 100, distance, _pay, ref remainder);

                Assert.That(income - previous, Is.EqualTo(10L), distance.ToString(CultureInfo.InvariantCulture));
                previous = income;
            }
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

            // 6 x 10 x 1.2125 = 72.75: the fraction carries from unit to unit instead of being lost.
            Assert.That(whole, Is.EqualTo(72L));
            Assert.That(wholeRemainder, Is.EqualTo(7500));
            Assert.That(unitByUnit, Is.EqualTo(whole));
            Assert.That(remainder, Is.EqualTo(wholeRemainder));
        }

        [Test]
        public void Compute_Flat_IgnoresDistance()
        {
            int remainder = 0;

            long income = DeliveryPay.Compute(8, 10, 60, DeliveryPaySettings.Flat, ref remainder);

            Assert.That(income, Is.EqualTo(80L));
            Assert.That(remainder, Is.Zero);
        }

        [Test]
        public void Compute_FractionFromAnOlderSave_IsCarriedIntoTheNextDelivery()
        {
            int remainder = 29;

            long income = DeliveryPay.Compute(1, 1, 30, _pay, ref remainder);

            Assert.That(income, Is.EqualTo(1L));
            Assert.That(remainder, Is.EqualTo(3779));
        }
    }
}
