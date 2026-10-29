#nullable enable
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class VehicleUnlockListItemTests
    {
        [Test]
        public void Constructor_ValidUnlockable_StoresFields()
        {
            var item = new VehicleUnlockListItem(
                "truck_mk2",
                "vehicle.truck_mk2",
                VehicleListKind.Road,
                1910,
                50000L,
                VehicleUnlockRowStatus.Unlockable,
                canAffordUnlock: true);

            Assert.That(item.TypeId, Is.EqualTo("truck_mk2"));
            Assert.That(item.NameKey, Is.EqualTo("vehicle.truck_mk2"));
            Assert.That(item.Kind, Is.EqualTo(VehicleListKind.Road));
            Assert.That(item.AvailableYear, Is.EqualTo(1910));
            Assert.That(item.UnlockCost, Is.EqualTo(50000L));
            Assert.That(item.Status, Is.EqualTo(VehicleUnlockRowStatus.Unlockable));
            Assert.That(item.CanAffordUnlock, Is.True);
        }

        [Test]
        public void Equals_SameValues_AreEqual()
        {
            var left = new VehicleUnlockListItem(
                "loco_mk2",
                "vehicle.loco_mk2",
                VehicleListKind.Rail,
                1915,
                80000L,
                VehicleUnlockRowStatus.Future,
                canAffordUnlock: false);
            var right = new VehicleUnlockListItem(
                "loco_mk2",
                "vehicle.loco_mk2",
                VehicleListKind.Rail,
                1915,
                80000L,
                VehicleUnlockRowStatus.Future,
                canAffordUnlock: false);

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void VehicleUnlockRowStatus_Values_AreStable()
        {
            Assert.That((int)VehicleUnlockRowStatus.Future, Is.EqualTo(0));
            Assert.That((int)VehicleUnlockRowStatus.Unlockable, Is.EqualTo(1));
            Assert.That((int)VehicleUnlockRowStatus.Unlocked, Is.EqualTo(2));
        }
    }
}
