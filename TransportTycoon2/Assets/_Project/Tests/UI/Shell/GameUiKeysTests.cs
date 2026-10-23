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
    }
}
