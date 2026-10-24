#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class VehiclePurchaseUiStateTests
    {
        [Test]
        public void CanAffordSelected_RoadWithEnoughCash_IsTrue()
        {
            var state = new VehiclePurchaseUiState(
                8000L,
                VehiclePurchaseSelection.Road,
                8000L,
                20000L,
                4000L);

            Assert.That(state.CanAffordSelected, Is.True);
            Assert.That(state.CanAffordWagon, Is.True);
        }

        [Test]
        public void CanAffordSelected_RailWithTooLittleCash_IsFalse()
        {
            var state = new VehiclePurchaseUiState(
                19999L,
                VehiclePurchaseSelection.Rail,
                8000L,
                20000L,
                4000L);

            Assert.That(state.CanAffordSelected, Is.False);
            Assert.That(state.CanAffordWagon, Is.True);
        }

        [Test]
        public void CanAffordSelected_None_IsFalse()
        {
            var state = new VehiclePurchaseUiState(
                100000L,
                VehiclePurchaseSelection.None,
                8000L,
                20000L,
                4000L);

            Assert.That(state.CanAffordSelected, Is.False);
        }
    }
}
