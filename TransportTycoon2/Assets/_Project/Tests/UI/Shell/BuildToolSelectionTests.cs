#nullable enable
using System;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class BuildToolSelectionTests
    {
        [Test]
        public void FromModes_NoFlags_ReturnsSelect()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, false, false, false, false, false, false, false),
                Is.EqualTo(BuildToolId.Select));
        }

        [Test]
        public void FromModes_RoadBuild_ReturnsRoad()
        {
            Assert.That(
                BuildToolSelection.FromModes(true, false, false, false, false, false, false, false),
                Is.EqualTo(BuildToolId.Road));
        }

        [Test]
        public void FromModes_RailBuild_ReturnsRail()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, false, true, false, false, false, false, false),
                Is.EqualTo(BuildToolId.Rail));
        }

        [Test]
        public void FromModes_RailStationBuild_ReturnsRailStation()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, false, false, false, false, false, true, false),
                Is.EqualTo(BuildToolId.RailStation));
        }

        [Test]
        public void ToModes_Station_SetsOnlyStationBuild()
        {
            BuildToolSelection.ToModes(
                BuildToolId.Station,
                out bool roadBuild,
                out bool roadDemolish,
                out bool railBuild,
                out bool railDemolish,
                out bool stationBuild,
                out bool stationDemolish,
                out bool railStationBuild,
                out bool railStationDemolish);

            Assert.That(roadBuild, Is.False);
            Assert.That(roadDemolish, Is.False);
            Assert.That(railBuild, Is.False);
            Assert.That(railDemolish, Is.False);
            Assert.That(stationBuild, Is.True);
            Assert.That(stationDemolish, Is.False);
            Assert.That(railStationBuild, Is.False);
            Assert.That(railStationDemolish, Is.False);
        }

        [Test]
        public void ToModes_RailStation_SetsOnlyRailStationBuild()
        {
            BuildToolSelection.ToModes(
                BuildToolId.RailStation,
                out bool roadBuild,
                out bool roadDemolish,
                out bool railBuild,
                out bool railDemolish,
                out bool stationBuild,
                out bool stationDemolish,
                out bool railStationBuild,
                out bool railStationDemolish);

            Assert.That(roadBuild, Is.False);
            Assert.That(roadDemolish, Is.False);
            Assert.That(railBuild, Is.False);
            Assert.That(railDemolish, Is.False);
            Assert.That(stationBuild, Is.False);
            Assert.That(stationDemolish, Is.False);
            Assert.That(railStationBuild, Is.True);
            Assert.That(railStationDemolish, Is.False);
        }

        [Test]
        public void ToModes_DemolishRailStation_SetsOnlyRailStationDemolish()
        {
            BuildToolSelection.ToModes(
                BuildToolId.DemolishRailStation,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out bool railStationBuild,
                out bool railStationDemolish);

            Assert.That(railStationBuild, Is.False);
            Assert.That(railStationDemolish, Is.True);
        }

        [Test]
        public void Toggle_SameTool_ClearsToSelect()
        {
            Assert.That(
                BuildToolSelection.Toggle(BuildToolId.Road, BuildToolId.Road),
                Is.EqualTo(BuildToolId.Select));
        }

        [Test]
        public void Toggle_DifferentTool_SelectsPressed()
        {
            Assert.That(
                BuildToolSelection.Toggle(BuildToolId.Road, BuildToolId.Station),
                Is.EqualTo(BuildToolId.Station));
        }

        [Test]
        public void Toggle_RailStation_SelectsPressed()
        {
            Assert.That(
                BuildToolSelection.Toggle(BuildToolId.Select, BuildToolId.RailStation),
                Is.EqualTo(BuildToolId.RailStation));
        }

        [Test]
        public void Toggle_Select_AlwaysSelect()
        {
            Assert.That(
                BuildToolSelection.Toggle(BuildToolId.DemolishRoad, BuildToolId.Select),
                Is.EqualTo(BuildToolId.Select));
        }

        [Test]
        public void Toggle_UndefinedTool_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BuildToolSelection.Toggle(BuildToolId.Select, (BuildToolId)99));
        }
    }
}
