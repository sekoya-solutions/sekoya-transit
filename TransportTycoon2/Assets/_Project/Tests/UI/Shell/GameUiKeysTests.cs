#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class GameUiKeysTests
    {
        [Test]
        public void ToolLabelKey_Road_ReturnsToolbarKey()
        {
            Assert.That(GameUiKeys.ToolLabelKey(BuildToolId.Road), Is.EqualTo(GameUiKeys.ToolRoad));
        }

        [Test]
        public void ToolLabelKey_Rail_ReturnsToolbarKey()
        {
            Assert.That(GameUiKeys.ToolLabelKey(BuildToolId.Rail), Is.EqualTo(GameUiKeys.ToolRail));
        }

        [Test]
        public void HintKey_Select_ReturnsGeneralHint()
        {
            Assert.That(GameUiKeys.HintKey(BuildToolId.Select), Is.EqualTo(GameUiKeys.Hint));
        }

        [Test]
        public void HintKey_DemolishStation_ReturnsToolHint()
        {
            Assert.That(
                GameUiKeys.HintKey(BuildToolId.DemolishStation),
                Is.EqualTo(GameUiKeys.HintToolDemolishStation));
        }

        [Test]
        public void ToolLabelKey_RailStation_ReturnsToolbarKey()
        {
            Assert.That(
                GameUiKeys.ToolLabelKey(BuildToolId.RailStation),
                Is.EqualTo(GameUiKeys.ToolRailStation));
        }

        [Test]
        public void ToolLabelKey_DemolishRailStation_ReturnsToolbarKey()
        {
            Assert.That(
                GameUiKeys.ToolLabelKey(BuildToolId.DemolishRailStation),
                Is.EqualTo(GameUiKeys.ToolDemolishRailStation));
        }

        [Test]
        public void HintKey_RailStation_ReturnsToolHint()
        {
            Assert.That(
                GameUiKeys.HintKey(BuildToolId.RailStation),
                Is.EqualTo(GameUiKeys.HintToolRailStation));
        }

        [Test]
        public void HintKey_DemolishRailStation_ReturnsToolHint()
        {
            Assert.That(
                GameUiKeys.HintKey(BuildToolId.DemolishRailStation),
                Is.EqualTo(GameUiKeys.HintToolDemolishRailStation));
        }
    }
}
