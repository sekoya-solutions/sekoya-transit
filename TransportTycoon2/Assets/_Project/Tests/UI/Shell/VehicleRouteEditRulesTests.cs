#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class VehicleRouteEditRulesTests
    {
        [Test]
        public void CanEditRoute_NoStationSelected_IsFalse()
        {
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Road,
                    Array.Empty<int>(),
                    VehiclePurchaseSelection.None,
                    null),
                Is.False);
        }

        [Test]
        public void CanEditRoute_ParkedVehicleOfStationKind_IsTrue()
        {
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Road,
                    Array.Empty<int>(),
                    VehiclePurchaseSelection.Road,
                    4),
                Is.True);
        }

        [Test]
        public void CanEditRoute_SingleStopRouteIsNotFunctional_IsTrue()
        {
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Rail,
                    new[] { 9 },
                    VehiclePurchaseSelection.Rail,
                    4),
                Is.True);
        }

        [Test]
        public void CanEditRoute_WorkingRouteUsingStation_IsTrue()
        {
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Road,
                    new[] { 2, 4 },
                    VehiclePurchaseSelection.Road,
                    4),
                Is.True);
        }

        [Test]
        public void CanEditRoute_WorkingRouteWithoutStation_IsFalse()
        {
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Road,
                    new[] { 2, 3 },
                    VehiclePurchaseSelection.Road,
                    4),
                Is.False);
        }

        [Test]
        public void CanEditRoute_VehicleKindDiffersFromStationKind_IsFalse()
        {
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Rail,
                    Array.Empty<int>(),
                    VehiclePurchaseSelection.Road,
                    4),
                Is.False);
            Assert.That(
                VehicleRouteEditRules.CanEditRoute(
                    VehicleListKind.Road,
                    new[] { 4, 5 },
                    VehiclePurchaseSelection.Rail,
                    4),
                Is.False);
        }

        [Test]
        public void IsListed_NoStationSelected_ListsEveryVehicle()
        {
            Assert.That(VehicleRouteEditRules.IsListed(VehiclePurchaseSelection.None, false), Is.True);
            Assert.That(VehicleRouteEditRules.IsListed(VehiclePurchaseSelection.None, true), Is.True);
        }

        [Test]
        public void IsListed_StationSelected_ListsOnlyRoutableVehicles()
        {
            Assert.That(VehicleRouteEditRules.IsListed(VehiclePurchaseSelection.Road, true), Is.True);
            Assert.That(VehicleRouteEditRules.IsListed(VehiclePurchaseSelection.Road, false), Is.False);
            Assert.That(VehicleRouteEditRules.IsListed(VehiclePurchaseSelection.Rail, false), Is.False);
        }
    }
}
