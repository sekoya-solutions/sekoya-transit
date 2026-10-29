#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class MapVehicleVisibilityTests
    {
        [Test]
        public void ShouldHideParkedVehicle_IdleWithoutRoute_Hides()
        {
            var vehicle = new RoadVehicle(0, typeId: RoadVehicle.StarterTypeId, stationNodeId: 1);
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
                typeId: RoadVehicle.StarterTypeId,
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
            var vehicle = new RoadVehicle(0, typeId: RoadVehicle.StarterTypeId, stationNodeId: 1);
            Assert.That(
                MapVehicleView.ShouldHideParkedVehicle(vehicle, VehicleListStatusKind.Moving),
                Is.False);
        }

        [Test]
        public void ShouldHideRailVehicle_IdleWithoutRoute_Hides()
        {
            var vehicle = new RailVehicle(0, typeId: RailVehicle.StarterTypeId, stationNodeId: 1);
            Assert.That(
                MapVehicleView.ShouldHideRailVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.True);
        }

        [Test]
        public void ShouldHideRailVehicle_ZeroWagonsWithRoute_HidesEvenWhenParkedMidRoute()
        {
            var orders = new[]
            {
                new RailVehicleOrder(0, waitForFull: false),
                new RailVehicleOrder(1, waitForFull: false),
            };
            var vehicle = new RailVehicle(
                id: 0,
                typeId: RailVehicle.StarterTypeId,
                orders: orders,
                orderIndex: 0,
                isStuck: false,
                isOnEdge: false,
                edgeFromNodeId: 1,
                edgeToNodeId: 1,
                progressAlongEdge: FixedPoint.Zero,
                wagonCount: 0);
            Assert.That(vehicle.WagonCount, Is.EqualTo(0));
            Assert.That(
                MapVehicleView.ShouldHideRailVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.True);
            Assert.That(
                MapVehicleView.ShouldHideRailVehicle(vehicle, VehicleListStatusKind.Moving),
                Is.True);
        }

        [Test]
        public void ShouldHideRailVehicle_ParkedMidRouteWithWagon_Shows()
        {
            var orders = new[]
            {
                new RailVehicleOrder(0, waitForFull: false),
                new RailVehicleOrder(1, waitForFull: false),
            };
            var vehicle = new RailVehicle(
                id: 0,
                typeId: RailVehicle.StarterTypeId,
                orders: orders,
                orderIndex: 0,
                isStuck: false,
                isOnEdge: false,
                edgeFromNodeId: 1,
                edgeToNodeId: 1,
                progressAlongEdge: FixedPoint.Zero,
                wagonCount: 1);
            Assert.That(
                MapVehicleView.ShouldHideRailVehicle(vehicle, VehicleListStatusKind.Parked),
                Is.False);
        }

        [Test]
        public void ShouldHideRailVehicle_MovingWithWagon_Shows()
        {
            var orders = new[]
            {
                new RailVehicleOrder(0, waitForFull: false),
                new RailVehicleOrder(1, waitForFull: false),
            };
            var vehicle = new RailVehicle(
                id: 0,
                typeId: RailVehicle.StarterTypeId,
                orders: orders,
                orderIndex: 0,
                isStuck: false,
                isOnEdge: true,
                edgeFromNodeId: 1,
                edgeToNodeId: 2,
                progressAlongEdge: FixedPoint.FromInt64(1),
                wagonCount: 1);
            Assert.That(
                MapVehicleView.ShouldHideRailVehicle(vehicle, VehicleListStatusKind.Moving),
                Is.False);
        }
    }
}
