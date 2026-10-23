#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class VehicleListStatusTests
    {
        [Test]
        public void Resolve_Stuck_WinsOverOtherFlags()
        {
            Assert.That(
                VehicleListStatus.Resolve(isStuck: true, isOnEdge: true, isServicingStop: true),
                Is.EqualTo(VehicleListStatusKind.Stuck));
        }

        [Test]
        public void Resolve_Servicing_IsLoading()
        {
            Assert.That(
                VehicleListStatus.Resolve(isStuck: false, isOnEdge: false, isServicingStop: true),
                Is.EqualTo(VehicleListStatusKind.Loading));
        }

        [Test]
        public void Resolve_OnEdge_IsMoving()
        {
            Assert.That(
                VehicleListStatus.Resolve(isStuck: false, isOnEdge: true, isServicingStop: false),
                Is.EqualTo(VehicleListStatusKind.Moving));
        }

        [Test]
        public void Resolve_IdleNode_IsParked()
        {
            Assert.That(
                VehicleListStatus.Resolve(isStuck: false, isOnEdge: false, isServicingStop: false),
                Is.EqualTo(VehicleListStatusKind.Parked));
        }
    }
}
