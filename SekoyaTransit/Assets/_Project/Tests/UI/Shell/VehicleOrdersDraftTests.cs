#nullable enable
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class VehicleOrdersDraftTests
    {
        [Test]
        public void SelectVehicle_LoadsStops_AndAppendRemoveReorder()
        {
            var draft = new VehicleOrdersDraft();
            draft.SelectVehicle(VehicleListKind.Road, 3, new[] { 10, 20 });

            Assert.That(draft.Kind, Is.EqualTo(VehicleListKind.Road));
            Assert.That(draft.VehicleId, Is.EqualTo(3));
            Assert.That(draft.Stops, Is.EqualTo(new[] { 10, 20 }));

            Assert.That(draft.TryAppend(30), Is.True);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 10, 20, 30 }));

            Assert.That(draft.TryMoveUp(2), Is.True);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 10, 30, 20 }));

            Assert.That(draft.TryMoveDown(0), Is.True);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 30, 10, 20 }));

            Assert.That(draft.TryRemoveAt(1), Is.True);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 30, 20 }));
            Assert.That(draft.ToStationIdArray(), Is.EqualTo(new[] { 30, 20 }));
        }

        [Test]
        public void TryAppend_WithoutSelection_ReturnsFalse()
        {
            var draft = new VehicleOrdersDraft();
            Assert.That(draft.TryAppend(1), Is.False);
            Assert.That(draft.Stops, Is.Empty);
        }

        [Test]
        public void TryAppend_AllowsDuplicateStationIds()
        {
            var draft = new VehicleOrdersDraft();
            draft.SelectVehicle(VehicleListKind.Rail, 0, System.Array.Empty<int>());
            Assert.That(draft.Kind, Is.EqualTo(VehicleListKind.Rail));
            Assert.That(draft.TryAppend(7), Is.True);
            Assert.That(draft.TryAppend(7), Is.True);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 7, 7 }));
        }

        [Test]
        public void ClearSelection_ClearsVehicleAndStops()
        {
            var draft = new VehicleOrdersDraft();
            draft.SelectVehicle(VehicleListKind.Road, 1, new[] { 2 });
            draft.ClearSelection();
            Assert.That(draft.HasSelection, Is.False);
            Assert.That(draft.Kind, Is.Null);
            Assert.That(draft.VehicleId, Is.Null);
            Assert.That(draft.Stops, Is.Empty);
        }

        [Test]
        public void MoveUp_AtFirstIndex_ReturnsFalse()
        {
            var draft = new VehicleOrdersDraft();
            draft.SelectVehicle(VehicleListKind.Road, 0, new[] { 1, 2 });
            Assert.That(draft.TryMoveUp(0), Is.False);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void MoveDown_AtLastIndex_ReturnsFalse()
        {
            var draft = new VehicleOrdersDraft();
            draft.SelectVehicle(VehicleListKind.Road, 0, new[] { 1, 2 });
            Assert.That(draft.TryMoveDown(1), Is.False);
            Assert.That(draft.Stops, Is.EqualTo(new[] { 1, 2 }));
        }
    }
}
