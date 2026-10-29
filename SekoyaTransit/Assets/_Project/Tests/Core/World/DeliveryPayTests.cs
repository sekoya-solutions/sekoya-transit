#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class DeliveryPayTests
    {
        private static readonly DeliveryPaySettings _pay = new DeliveryPaySettings(30, 4);

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
