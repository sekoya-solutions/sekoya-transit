#nullable enable
using NUnit.Framework;
using SekoyaTransit.Presentation.Map;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class MapSortingOrderTests
    {
        [Test]
        public void ForWorldY_NearerTile_DrawsOverFartherTile()
        {
            Vector3 near = IsometricProjection.TileToWorld(10, 10);
            Vector3 behind = IsometricProjection.TileToWorld(11, 10);

            Assert.That(MapSortingOrder.ForWorldY(near.y), Is.GreaterThan(MapSortingOrder.ForWorldY(behind.y)));
        }

        [Test]
        public void ForWorldY_LargestMap_StaysBetweenTracksAndHighlights()
        {
            int nearest = MapSortingOrder.ForWorldY(IsometricProjection.TileToWorld(0, 0).y);
            int farthest = MapSortingOrder.ForWorldY(IsometricProjection.TileToWorld(511, 511).y);

            Assert.That(farthest, Is.GreaterThan(MapSortingOrder.Tracks));
            Assert.That(nearest, Is.LessThan(MapSortingOrder.Highlights));
            Assert.That(nearest, Is.GreaterThan(farthest));
        }
    }
}
