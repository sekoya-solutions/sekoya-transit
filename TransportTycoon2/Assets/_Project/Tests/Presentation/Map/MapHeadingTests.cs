#nullable enable
using NUnit.Framework;
using TransportTycoon.Presentation.Map;
using UnityEngine;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class MapHeadingTests
    {
        [TestCase(0f, 1f, MapHeading.North)]
        [TestCase(1f, 0f, MapHeading.East)]
        [TestCase(0f, -1f, MapHeading.South)]
        [TestCase(-1f, 0f, MapHeading.West)]
        [TestCase(0.9f, -0.2f, MapHeading.East)]
        [TestCase(0.5f, 0.5f, MapHeading.East)]
        public void FromTileDelta_Movement_FacesTheDominantAxis(float deltaX, float deltaY, MapHeading expected)
        {
            Assert.That(MapHeadings.FromTileDelta(deltaX, deltaY, MapHeading.South), Is.EqualTo(expected));
        }

        [Test]
        public void FromTileDelta_NoMovement_KeepsTheFallback()
        {
            Assert.That(MapHeadings.FromTileDelta(0f, 0f, MapHeading.West), Is.EqualTo(MapHeading.West));
        }

        [TestCase(0f, 1f, MapHeading.North)]
        [TestCase(1f, 0f, MapHeading.East)]
        [TestCase(0f, -1f, MapHeading.South)]
        [TestCase(-1f, 0f, MapHeading.West)]
        public void FromWorldDelta_ProjectedTileStep_RecoversTheHeading(float tileX, float tileY, MapHeading expected)
        {
            Vector3 world = IsometricProjection.TileToWorld(tileX, tileY);

            Assert.That(MapHeadings.FromWorldDelta(world.x, world.y, MapHeading.South), Is.EqualTo(expected));
        }
    }
}
