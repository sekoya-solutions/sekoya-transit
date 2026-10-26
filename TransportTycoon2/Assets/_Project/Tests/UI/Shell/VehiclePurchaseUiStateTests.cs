#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class VehiclePurchaseUiStateTests
    {
        [Test]
        public void CanAffordWagon_WithEnoughCash_IsTrue()
        {
            var options = new[]
            {
                new VehiclePurchaseOption("truck_mk1", "vehicle.truck_mk1", 8000L, true),
            };
            var state = new VehiclePurchaseUiState(
                8000L,
                VehiclePurchaseSelection.Road,
                options,
                4000L);

            Assert.That(state.CanAffordWagon, Is.True);
            Assert.That(state.Options.Count, Is.EqualTo(1));
            Assert.That(state.Options[0].CanAfford, Is.True);
        }

        [Test]
        public void CanAffordWagon_WithTooLittleCash_IsFalse()
        {
            var options = new[]
            {
                new VehiclePurchaseOption("loco_mk1", "vehicle.loco_mk1", 20000L, false),
            };
            var state = new VehiclePurchaseUiState(
                3999L,
                VehiclePurchaseSelection.Rail,
                options,
                4000L);

            Assert.That(state.CanAffordWagon, Is.False);
            Assert.That(state.Options[0].CanAfford, Is.False);
        }

        [Test]
        public void Options_NoneSelection_AreEmpty()
        {
            var state = new VehiclePurchaseUiState(
                100000L,
                VehiclePurchaseSelection.None,
                System.Array.Empty<VehiclePurchaseOption>(),
                4000L);

            Assert.That(state.Options, Is.Empty);
            Assert.That(state.Selection, Is.EqualTo(VehiclePurchaseSelection.None));
        }

        [Test]
        public void Equals_SameOptions_AreEqual()
        {
            var options = new[]
            {
                new VehiclePurchaseOption("truck_mk1", "vehicle.truck_mk1", 8000L, true),
            };
            var left = new VehiclePurchaseUiState(10000L, VehiclePurchaseSelection.Road, options, 4000L);
            var right = new VehiclePurchaseUiState(10000L, VehiclePurchaseSelection.Road, options, 4000L);

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }
    }
}
