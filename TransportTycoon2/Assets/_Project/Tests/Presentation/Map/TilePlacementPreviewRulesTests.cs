#nullable enable
using NUnit.Framework;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class TilePlacementPreviewRulesTests
    {
        [Test]
        public void IsPlaceholderPlacementValid_EmptyTile_ReturnsTrue()
        {
            Assert.That(TilePlacementPreviewRules.IsPlaceholderPlacementValid(TileMap.NoObjectId), Is.True);
        }

        [Test]
        public void IsPlaceholderPlacementValid_OccupiedTile_ReturnsFalse()
        {
            Assert.That(TilePlacementPreviewRules.IsPlaceholderPlacementValid(0), Is.False);
            Assert.That(TilePlacementPreviewRules.IsPlaceholderPlacementValid(3), Is.False);
        }
    }
}
