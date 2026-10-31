#nullable enable
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class PanelBetweenToolbarsTests
    {
        [Test]
        public void ComputeInsets_Toolbars_LeavesTheSameGapAboveAndBelow()
        {
            // 1080 high root, top bar ending at 84, bottom toolbar starting at 980 (100 pixels high, 16 from the edge).
            (float top, float bottom) = PanelBetweenToolbars.ComputeInsets(1080f, 84f, 980f);

            Assert.That(top, Is.EqualTo(84f + PanelBetweenToolbars.Gap));
            Assert.That(bottom, Is.EqualTo(100f + PanelBetweenToolbars.Gap));
            Assert.That(top - 84f, Is.EqualTo(980f - (1080f - bottom)));
        }
    }
}
