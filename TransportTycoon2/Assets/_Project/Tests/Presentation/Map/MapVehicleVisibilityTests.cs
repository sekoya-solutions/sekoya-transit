#nullable enable
using NUnit.Framework;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class MapVehicleVisibilityTests
    {
        [Test]
        public void ShouldHideParkedVehicle_IdleWithoutRoute_Hides()
        {
            var vehicle = new RoadVehicle(0, typeId: 0, stationNodeId: 1);
            Assert.That(
                MapVehicleView.ShouldHideParkedVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.True);
        }

        [Test]
        public void ShouldHideParkedVehicle_ParkedMidRoute_Shows()
        {
            var orders = new[]
            {
                new RoadVehicleOrder(0, waitForFull: false),
                new RoadVehicleOrder(1, waitForFull: false),
            };
            var vehicle = new RoadVehicle(
                id: 0,
                typeId: 0,
                orders: orders,
                orderIndex: 0,
                isStuck: false,
                isOnEdge: false,
                edgeFromNodeId: 1,
                edgeToNodeId: 1,
                progressAlongEdge: FixedPoint.Zero);
            Assert.That(
                MapVehicleView.ShouldHideParkedVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.False);
        }

        [Test]
        public void ShouldHideParkedVehicle_Moving_Shows()
        {
            var vehicle = new RoadVehicle(0, typeId: 0, stationNodeId: 1);
            Assert.That(
                MapVehicleView.ShouldHideParkedVehicle(vehicle, VehicleListStatusKind.Moving),
                Is.False);
        }

        [Test]
        public void ShouldHideParkedRailVehicle_IdleWithoutRoute_Hides()
        {
            var vehicle = new RailVehicle(0, typeId: 0, stationNodeId: 1);
            Assert.That(
                MapVehicleView.ShouldHideParkedRailVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.True);
        }

        [Test]
        public void ShouldHideParkedRailVehicle_ParkedMidRoute_Shows()
        {
            var orders = new[]
            {
                new RailVehicleOrder(0, waitForFull: false),
                new RailVehicleOrder(1, waitForFull: false),
            };
            var vehicle = new RailVehicle(
                id: 0,
                typeId: 0,
                orders: orders,
                orderIndex: 0,
                isStuck: false,
                isOnEdge: false,
                edgeFromNodeId: 1,
                edgeToNodeId: 1,
                progressAlongEdge: FixedPoint.Zero);
            Assert.That(
                MapVehicleView.ShouldHideParkedRailVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.False);
        }
    }
}
