#nullable enable
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
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
        public void ToolLabelKey_RailStation_ReturnsToolbarKey()
        {
            Assert.That(
                GameUiKeys.ToolLabelKey(BuildToolId.RailStation),
                Is.EqualTo(GameUiKeys.ToolRailStation));
        }

        [Test]
        public void ToolLabelKey_Demolish_ReturnsToolbarKey()
        {
            Assert.That(
                GameUiKeys.ToolLabelKey(BuildToolId.Demolish),
                Is.EqualTo(GameUiKeys.ToolDemolish));
        }
    }
}
