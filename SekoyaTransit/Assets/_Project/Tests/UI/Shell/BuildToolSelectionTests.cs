#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class BuildToolSelectionTests
    {
        [Test]
        public void FromModes_NoFlags_ReturnsSelect()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, false, false, false, false),
                Is.EqualTo(BuildToolId.Select));
        }

        [Test]
        public void FromModes_RoadBuild_ReturnsRoad()
        {
            Assert.That(
                BuildToolSelection.FromModes(true, false, false, false, false),
                Is.EqualTo(BuildToolId.Road));
        }

        [Test]
        public void FromModes_RailBuild_ReturnsRail()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, true, false, false, false),
                Is.EqualTo(BuildToolId.Rail));
        }

        [Test]
        public void FromModes_RailStationBuild_ReturnsRailStation()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, false, false, true, false),
                Is.EqualTo(BuildToolId.RailStation));
        }

        [Test]
        public void FromModes_Demolish_ReturnsDemolish()
        {
            Assert.That(
                BuildToolSelection.FromModes(false, false, false, false, true),
                Is.EqualTo(BuildToolId.Demolish));
        }

        [Test]
        public void ToModes_Station_SetsOnlyStationBuild()
        {
            BuildToolSelection.ToModes(
                BuildToolId.Station,
                out bool roadBuild,
                out bool railBuild,
                out bool stationBuild,
                out bool railStationBuild,
                out bool demolish);

            Assert.That(roadBuild, Is.False);
            Assert.That(railBuild, Is.False);
            Assert.That(stationBuild, Is.True);
            Assert.That(railStationBuild, Is.False);
            Assert.That(demolish, Is.False);
        }

        [Test]
        public void ToModes_Demolish_SetsOnlyDemolish()
        {
            BuildToolSelection.ToModes(
                BuildToolId.Demolish,
                out bool roadBuild,
                out bool railBuild,
                out bool stationBuild,
                out bool railStationBuild,
                out bool demolish);

            Assert.That(roadBuild, Is.False);
            Assert.That(railBuild, Is.False);
            Assert.That(stationBuild, Is.False);
            Assert.That(railStationBuild, Is.False);
            Assert.That(demolish, Is.True);
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
        public void Toggle_Select_AlwaysSelect()
        {
            Assert.That(
                BuildToolSelection.Toggle(BuildToolId.Demolish, BuildToolId.Select),
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
